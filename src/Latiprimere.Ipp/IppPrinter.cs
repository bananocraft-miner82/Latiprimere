using System.Globalization;
using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Raster;
using Latiprimere.Ipp.Resources;
using Latiprimere.Rendering;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol.Models;
using IppDocumentFormat = SharpIpp.Protocol.Models.DocumentFormat;

namespace Latiprimere.Ipp;

/// <summary>
/// A printer reached over IPP, either a CUPS queue or a network printer.
/// </summary>
internal sealed class IppPrinter : IPrinter
{
	private static readonly string[] MediaDefaultAttributes = ["media-default"]; // Asked for an image's paper
	private static readonly TimeSpan DefaultPaperLifetime = TimeSpan.FromMinutes(5); // How long it is kept

	private readonly Lock defaultPaperLock = new Lock();          // Guards the two fields below
	private MediaSize? defaultPaper;                              // The printer's default paper, once read
	private long defaultPaperReadAt;                              // When, from the context's clock

	private readonly IppPrintContext context;                     // Client and settings shared with the print system
	private readonly Uri printerUri;                              // The printer's IPP URI
	private readonly IReadOnlyCollection<string>? supportedFormats; // MIME types it accepts; null when not checked
	private readonly bool supportsValidateJob;                    // Whether it lists Validate-Job among its operations
	private bool refusesVendorOptions;                            // Whether it refused the vendor option request

	/// <summary>
	/// Initialises a new instance of the <see cref="IppPrinter"/> class.
	/// </summary>
	/// <param name="context">The client and settings shared with the print system.</param>
	/// <param name="printerUri">The printer's IPP URI.</param>
	/// <param name="info">Information about the printer.</param>
	/// <param name="supportedFormats">
	/// The MIME types the printer accepts, to reject unsupported documents before sending them; or
	/// <see langword="null"/> to leave that to the print system, as CUPS converts most formats itself.
	/// </param>
	/// <param name="supportedOperations">The operations the printer lists, or <see langword="null"/> when not read;
	/// Validate-Job is sent only to a printer that lists it.</param>
	public IppPrinter
	(
		IppPrintContext                    context,
		Uri                                printerUri,
		PrinterInfo                        info,
		IReadOnlyCollection<string>?       supportedFormats = null,
		IReadOnlyCollection<IppOperation>? supportedOperations = null
	)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(printerUri);
		ArgumentNullException.ThrowIfNull(info);

		this.context = context;
		this.printerUri = printerUri;
		this.Info = info;
		this.supportedFormats = supportedFormats;
		this.supportsValidateJob = supportedOperations?.Contains(IppOperation.ValidateJob) == true;
	}

	/// <inheritdoc/>
	public PrinterInfo Info { get; }

	/// <summary>
	/// Gets the printer's IPP URI.
	/// </summary>
	internal Uri PrinterUri
	{
		get
		{
			return this.printerUri;
		}
	}

	/// <summary>
	/// Gets what goes before the IPP <c>job-id</c> in this printer's job IDs: empty, unless the printer is reached
	/// on behalf of another print system whose own job IDs are numbers too, as a Windows queue's is (ADR 0040).
	/// </summary>
	internal string JobIdPrefix { get; init; } = string.Empty;

	/// <summary>
	/// Gets what the printer says it asks for at <c>printerUri</c>, read with its attributes, so a refusal for
	/// authentication can say what it wants; or <see langword="null"/> when not known.
	/// </summary>
	internal PrinterUriRequirements? UriRequirements { get; init; }

	/// <inheritdoc/>
	public async Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(options);

		// IPP has no way to print to a client-side file, and silently printing on paper instead would be worse
		if (options.OutputFile is not null)
		{
			throw new NotSupportedException(ExceptionMessages.OutputFileNotSupported);
		}

		// An image or text the printer can't take itself goes as a PDF on its paper, then as any PDF does (ADR 0047,
		// ADR 0048)
		if (!this.TakesAsItIs(content.Format))
		{
			MediaSize paper = await this.ChooseConvertedPaperAsync(content.Format, options, cancellationToken).ConfigureAwait(false);
			(MemoryStream pdf, bool landscape) = DocumentConverter.ToPdf(content, paper, options);

			using (pdf)
			{
				IppLog.PrintingConvertedAsPdf(this.context.Logger, this.printerUri, content.Format, landscape);

				// The page is laid out already: the printer, or the raster renderer, turns it to fit the paper
				PrintOptions pdfOptions = options with { Orientation = PageOrientation.PrinterDefault, Scaling = ScalingMode.PrinterDefault };

				return await this.SubmitAsync(PrintContent.FromPdf(pdf), pdfOptions, cancellationToken).ConfigureAwait(false);
			}
		}

		// CUPS reads text as UTF-8 without a byte order mark, whatever it came in, so it is re-encoded as it uploads
		if (content.Format == Latiprimere.DocumentFormat.Text && content.Content is not Utf8TextStream)
		{
			using Utf8TextStream utf8 = new Utf8TextStream(content.Content, content.TextEncoding ?? System.Text.Encoding.UTF8);

			return await this.SubmitAsync(PrintContent.FromText(utf8, Utf8TextStream.Encoding), options, cancellationToken).ConfigureAwait(false);
		}

		// A PDF to a printer that can't take PDF goes as raster, when it takes a raster format (ADR 0035)
		RasterJobPlan? rasterPlan = await this.PlanRasterAsync(content.Format, options, cancellationToken).ConfigureAwait(false);

		// Vendor options are checked against the typed request before the job is sent
		PrintJobRequest request = this.CreatePrintJobRequest(content, options, rasterPlan);

		IppVendorAttributes vendorAttributes = IppVendorAttributeMapper.ToAttributes
		(
			options.VendorOptions,
			this.context.VendorOptionPrefixes,
			request
		);

		// Opened before anything is sent, so a PDF that can't be read fails first; rendered as it is uploaded
		using IRasterDocument? rasterDocument = rasterPlan is null ? null : this.context.PdfRasterizer!.Open(content.Content);

		IppJobDocument document = rasterDocument is null
			? new StreamJobDocument(content.Content)
			: new RasterJobDocument(rasterDocument, rasterPlan!);

		PrintJobResponse response;

		try
		{
			// Ask the printer to check the job's attributes first, so a refusal comes before the upload, not after it
			await this.ValidateAsync(request, vendorAttributes, cancellationToken).ConfigureAwait(false);

			// Send the document with its job attributes
			response = await this
				.SendPrintJobAsync(request, document, vendorAttributes, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (PrintAuthenticationException exception) when (this.CreateAuthenticationHint() is { } hint)
		{
			throw new PrintAuthenticationException(exception.Message + " " + hint, exception)
			{
				NativeErrorCode = exception.NativeErrorCode
			};
		}

		// The job ID is needed to follow or cancel the job, so a response without one is an error
		int jobNumber = response.JobAttributes?.JobId ?? 0;

		if (jobNumber <= 0)
		{
			string message = ExceptionMessages.FormatIppResponseMissingJob(this.printerUri);

			throw new PrintException(message);
		}

		return new IppPrintJob(this.context, this.printerUri, this.Info.Id, jobNumber, this.JobIdPrefix);
	}

	/// <inheritdoc/>
	public async Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(jobId);

		// An ID this printer couldn't have given is one it doesn't know
		if (!this.TryParseJobId(jobId, out int jobNumber))
		{
			return null;
		}

		GetJobAttributesRequest request = new GetJobAttributesRequest
		{
			OperationAttributes = new GetJobAttributesOperationAttributes
			{
				PrinterUri          = new IppValue<Uri>(this.printerUri),
				JobId               = new IppValue<int>(jobNumber),
				RequestingUserName  = new StringWithLanguage(this.context.RequestingUserName),
				RequestedAttributes = new IppValue<string[]>(IppRequestedAttributes.JobLookupAttributes)
			}
		};

		GetJobAttributesResponse response;

		try
		{
			response = await this.context.Client.GetJobAttributesAsync(request, cancellationToken).ConfigureAwait(false);
		}
		catch (PrintException exception) when (exception.NativeErrorCode == (int)IppStatusCode.ClientErrorNotFound)
		{
			// Forgotten, or never there
			return null;
		}

		if (!this.IsOwnJob(response.JobAttributes))
		{
			return null;
		}

		return new IppPrintJob(this.context, this.printerUri, this.Info.Id, jobNumber, this.JobIdPrefix);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Read with Get-Printer-Attributes. Following the printer with <see cref="IPrinter.WatchStatusAsync"/> polls it:
	/// CUPS doesn't wait for events when asked for notifications, and the network printers tested offer none
	/// (ADR 0049).
	/// </remarks>
	public async Task<PrinterStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		PrinterDescriptionAttributes attributes = await IppPrinterQueries
			.GetAttributesAsync(this.context, this.printerUri, IppPrinterStatusMapper.Attributes, cancellationToken)
			.ConfigureAwait(false)
			?? throw new PrintException(ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerUri));

		try
		{
			return IppPrinterStatusMapper.ToPrinterStatus(attributes, this.Info.Id, this.context.TimeProvider.GetUtcNow());
		}
		catch (Exception exception) when (IppResponseErrors.IsUnreadableData(exception))
		{
			throw IppResponseErrors.CreateUnreadable(this.printerUri, exception);
		}
	}

	/// <inheritdoc/>
	public async Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		PrinterDescriptionAttributes? attributes = await IppPrinterQueries
															.GetAttributesAsync
															(
																this.context,
																this.printerUri,
																IppRequestedAttributes.CapabilityAttributes,
																cancellationToken
															)
															.ConfigureAwait(false);

		if (attributes is null)
		{
			string message = ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerUri);

			throw new PrintException(message);
		}

		PrinterCapabilities capabilities;

		try
		{
			capabilities = IppCapabilitiesMapper.ToCapabilities(attributes);
		}
		catch (Exception exception) when (IppResponseErrors.IsUnreadableData(exception))
		{
			throw IppResponseErrors.CreateUnreadable(this.printerUri, exception);
		}

		// Afterwards, not at the same time: some printers take one connection at a time (ADR 0032)
		IReadOnlyList<VendorOption> vendorOptions = await this.ReadVendorOptionsAsync(cancellationToken).ConfigureAwait(false);

		return capabilities with { DocumentFormats = this.ListDocumentFormats(), VendorOptions = vendorOptions };
	}

	/// <summary>
	/// Lists the document formats the printer can be sent, itself or converted. Every format is listed for CUPS, which
	/// converts documents and lays out text itself, and for a printer that doesn't say what it takes.
	/// </summary>
	/// <returns>The formats.</returns>
	internal List<Latiprimere.DocumentFormat> ListDocumentFormats()
	{
		if (this.supportedFormats is null || this.context.DocumentFormats.ConvertsDocuments)
		{
			return [Latiprimere.DocumentFormat.Pdf, Latiprimere.DocumentFormat.Raw, Latiprimere.DocumentFormat.Jpeg, Latiprimere.DocumentFormat.Png, Latiprimere.DocumentFormat.Text];
		}

		bool takesPdf = this.TakesPdfOrRaster();
		bool takesRaw = this.supportedFormats.Contains(this.context.DocumentFormats.Raw, StringComparer.OrdinalIgnoreCase);
		bool takesJpeg = this.supportedFormats.Contains(IppDocumentFormats.Jpeg, StringComparer.OrdinalIgnoreCase);
		List<Latiprimere.DocumentFormat> formats = [];

		if (takesPdf)
		{
			formats.Add(Latiprimere.DocumentFormat.Pdf);
		}

		if (takesRaw)
		{
			formats.Add(Latiprimere.DocumentFormat.Raw);
		}

		if (takesPdf || takesJpeg)
		{
			formats.Add(Latiprimere.DocumentFormat.Jpeg);
		}

		if (takesPdf)
		{
			formats.Add(Latiprimere.DocumentFormat.Png);
			formats.Add(Latiprimere.DocumentFormat.Text);
		}

		return formats;
	}

	/// <summary>
	/// Reads every job attribute the printer accepts, with its supported values, as vendor options. They are an extra:
	/// a printer that refuses the request, or answers it with something unreadable, lists none, and that is logged.
	/// A printer that refuses it, with an IPP client error or an unreadable answer, isn't asked again, so it costs
	/// one round trip and one log entry, not one per read. A failure to connect may pass, so it is tried again.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The vendor options; empty if they couldn't be read.</returns>
	/// <exception cref="OperationCanceledException">The request was cancelled.</exception>
	private async Task<IReadOnlyList<VendorOption>> ReadVendorOptionsAsync(CancellationToken cancellationToken)
	{
		if (this.refusesVendorOptions)
		{
			return Array.Empty<VendorOption>();
		}

		try
		{
			IReadOnlyList<IppAttribute>? attributeList = await IppPrinterQueries
				.GetAttributeListAsync(this.context, this.printerUri, IppRequestedAttributes.VendorOptionAttributes, cancellationToken)
				.ConfigureAwait(false);

			return attributeList is null ? Array.Empty<VendorOption>() : IppVendorOptionMapper.ToVendorOptions(attributeList);
		}
		catch (Exception exception) when (exception is PrintException || IppResponseErrors.IsUnreadableData(exception))
		{
			this.refusesVendorOptions = IppPrinter.IsRefusal(exception);
			IppLog.VendorOptionsUnreadable(this.context.Logger, this.printerUri, exception);

			return Array.Empty<VendorOption>();
		}
	}

	/// <summary>
	/// Says what the printer asks for, from its <c>uri-authentication-supported</c>, to add to a refusal for
	/// authentication: an encrypted address to print to instead, or authentication Latiprimere can't give.
	/// </summary>
	/// <returns>The hint, or <see langword="null"/> when the printer's attributes add nothing.</returns>
	private string? CreateAuthenticationHint()
	{
		PrinterUriRequirements? requirements = this.UriRequirements;

		if (requirements is null)
		{
			return null;
		}

		if (requirements.AsksForUnsupportedAuthentication)
		{
			return ExceptionMessages.FormatIppAuthenticationMethodNotSupported(requirements.Authentication);
		}

		return requirements.SecureAlternative is null
			? null
			: ExceptionMessages.FormatIppAuthenticationSecureAddress(requirements.SecureAlternative);
	}

	/// <summary>
	/// Checks whether a failure is the printer's answer, which asking again would only repeat: an IPP client error,
	/// such as client-error-attributes-or-values-not-supported, or an answer that can't be read.
	/// </summary>
	/// <param name="exception">The failure.</param>
	/// <returns><see langword="true"/> for a refusal; <see langword="false"/> for one that may pass, such as a
	/// failure to connect.</returns>
	private static bool IsRefusal(Exception exception)
	{
		return IppResponseErrors.IsUnreadableData(exception)
			   || exception is PrintException { NativeErrorCode: >= (int)IppStatusCode.ClientErrorBadRequest and < (int)IppStatusCode.ServerErrorInternalError };
	}

	/// <summary>
	/// Checks whether a document goes to the printer in its own format: anything but an image or text does, and so
	/// does everything to CUPS, which converts images and lays out text itself. A printer reached directly takes a
	/// JPEG as it is when it lists <c>image/jpeg</c>, or lists no formats at all, as IPP Everywhere and AirPrint
	/// printers must take JPEG; a PNG or text is laid out as a PDF for it.
	/// </summary>
	/// <param name="format">The document's format.</param>
	/// <returns>Whether it goes as it is.</returns>
	private bool TakesAsItIs(Latiprimere.DocumentFormat format)
	{
		if (!DocumentConverter.CanConvert(format) || this.context.DocumentFormats.ConvertsDocuments)
		{
			return true;
		}

		return format == Latiprimere.DocumentFormat.Jpeg
			&& (this.supportedFormats is null || this.supportedFormats.Contains(IppDocumentFormats.Jpeg, StringComparer.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Checks whether the printer can be sent a PDF: it takes PDF, or a raster format the options allow and there is
	/// a rasteriser to render it.
	/// </summary>
	/// <returns>Whether it can, when its formats are known.</returns>
	private bool TakesPdfOrRaster()
	{
		if (this.supportedFormats is null)
		{
			return true;
		}

		string pdf = this.context.DocumentFormats.GetMimeType(Latiprimere.DocumentFormat.Pdf);

		return this.supportedFormats.Contains(pdf, StringComparer.OrdinalIgnoreCase)
			|| (this.context.PdfRasterizer is not null && RasterJobPlanner.ChooseFormat(this.supportedFormats, this.context.RasterFormats) is not null);
	}

	/// <summary>
	/// Chooses the paper an image or text converted to PDF goes on: the job's, or the printer's default, after
	/// checking the PDF can be sent at all. The default is read once every few minutes at most.
	/// </summary>
	/// <param name="format">The document's format, for the message if it can't.</param>
	/// <param name="options">The job's options.</param>
	/// <param name="cancellationToken">A token to cancel reading the printer's default.</param>
	/// <returns>The paper.</returns>
	/// <exception cref="NotSupportedException">The printer takes neither PDF nor a raster format Latiprimere can
	/// render it in, or its default paper has no known size.</exception>
	/// <exception cref="PrintException">The printer's default paper couldn't be read.</exception>
	private async Task<MediaSize> ChooseConvertedPaperAsync(Latiprimere.DocumentFormat format, PrintOptions options, CancellationToken cancellationToken)
	{
		if (!this.TakesPdfOrRaster())
		{
			string message = ExceptionMessages.FormatConvertedDocumentNotSupportedByPrinter
			(
				this.context.DocumentFormats.GetMimeType(format),
				string.Join(", ", this.supportedFormats ?? [])
			);

			throw new NotSupportedException(message);
		}

		if (options.MediaSize is not null)
		{
			return options.MediaSize;
		}

		// Kept a few minutes, so a run of jobs asks once, yet paper changed at the printer is seen soon
		lock (this.defaultPaperLock)
		{
			if (this.defaultPaper is not null
				&& this.context.TimeProvider.GetElapsedTime(this.defaultPaperReadAt) < IppPrinter.DefaultPaperLifetime)
			{
				return this.defaultPaper;
			}
		}

		IReadOnlyList<IppAttribute> attributes = await IppPrinterQueries
			.GetAttributeListAsync(this.context, this.printerUri, IppPrinter.MediaDefaultAttributes, cancellationToken)
			.ConfigureAwait(false)
			?? throw new PrintException(ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerUri));

		string? name = attributes
			.Where(attribute => attribute.Name == "media-default")
			.Select(attribute => attribute.Value as string)
			.FirstOrDefault();

		MediaSize paper = (name is null ? null : PwgMediaNames.ToMediaSize(name))
			?? throw new NotSupportedException(ExceptionMessages.FormatRasterMediaUnknown(name ?? string.Empty));

		lock (this.defaultPaperLock)
		{
			this.defaultPaper = paper;
			this.defaultPaperReadAt = this.context.TimeProvider.GetTimestamp();
		}

		return paper;
	}

	/// <summary>
	/// Checks the printer accepts the document's format, when the supported formats are known, and plans a raster
	/// job for a PDF it can't take but can take as raster (ADR 0035).
	/// </summary>
	/// <param name="format">The document format.</param>
	/// <param name="options">The job's options.</param>
	/// <param name="cancellationToken">A token to cancel reading the printer's raster attributes.</param>
	/// <returns>The raster job's plan, or <see langword="null"/> to send the document as it is.</returns>
	/// <exception cref="NotSupportedException">The printer doesn't accept the format, nor PDF as raster in a format
	/// the options allow; or it would, but there is no PDF rasteriser.</exception>
	/// <exception cref="PrintException">The printer's raster attributes couldn't be read.</exception>
	private async Task<RasterJobPlan?> PlanRasterAsync(Latiprimere.DocumentFormat format, PrintOptions options, CancellationToken cancellationToken)
	{
		string mimeType = this.context.DocumentFormats.GetMimeType(format);

		if (this.supportedFormats is null || this.supportedFormats.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
		{
			return null;
		}

		string accepted = string.Join(", ", this.supportedFormats);
		RasterFormat? rasterFormat = format == Latiprimere.DocumentFormat.Pdf
			? RasterJobPlanner.ChooseFormat(this.supportedFormats, this.context.RasterFormats)
			: null;

		if (rasterFormat is null)
		{
			// A raster format the printer takes, which the application left out, deserves saying so
			string[] excluded = format == Latiprimere.DocumentFormat.Pdf
				? RasterJobPlanner.DefaultFormatOrder
					.Select(RasterJobPlanner.GetMimeType)
					.Where(rasterMimeType => this.supportedFormats.Contains(rasterMimeType, StringComparer.OrdinalIgnoreCase))
					.ToArray()
				: [];

			throw new NotSupportedException
			(
				excluded.Length > 0
					? ExceptionMessages.FormatPdfRasterFormatsExcluded(string.Join(", ", excluded))
					: ExceptionMessages.FormatDocumentFormatNotSupportedByPrinter(mimeType, accepted)
			);
		}

		if (this.context.PdfRasterizer is null)
		{
			throw new NotSupportedException(ExceptionMessages.FormatPdfNeedsRasterizerForPrinter(accepted));
		}

		IReadOnlyList<IppAttribute> attributes = await IppPrinterQueries
			.GetAttributeListAsync(this.context, this.printerUri, RasterJobPlanner.Attributes, cancellationToken)
			.ConfigureAwait(false)
			?? throw new PrintException(ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerUri));

		RasterJobPlan plan = RasterJobPlanner.Plan(rasterFormat.Value, attributes, options, this.context.MaxRenderDpi);
		IppLog.PrintingAsRaster(this.context.Logger, this.printerUri, plan.MimeType, plan.Settings.ColorSpace, plan.Settings.Dpi, plan.Gzip);

		return plan;
	}

	/// <summary>
	/// Sends the Print-Job request, sending it again while the printer is busy with another job, for up to
	/// <see cref="IppPrintContext.BusyRetryDuration"/>.
	/// </summary>
	/// <param name="request">The request, without its document.</param>
	/// <param name="document">The document, sent from its start on each attempt.</param>
	/// <param name="vendorAttributes">Attributes from the vendor options.</param>
	/// <param name="cancellationToken">A token to cancel the request or the wait between attempts.</param>
	/// <returns>The printer's response.</returns>
	/// <exception cref="PrintException">The printer rejected the job, or was still busy when the time ran out.</exception>
	private async Task<PrintJobResponse> SendPrintJobAsync
	(
		PrintJobRequest     request,
		IppJobDocument      document,
		IppVendorAttributes vendorAttributes,
		CancellationToken   cancellationToken
	)
	{
		long startTimestamp = this.context.TimeProvider.GetTimestamp();
		TimeSpan delay = this.context.BusyRetryFirstDelay;

		while (true)
		{
			try
			{
				return await document
					.SendAsync(this.context.Client, request, vendorAttributes, cancellationToken)
					.ConfigureAwait(false);
			}
			catch (PrintException exception) when (document.CanResend && this.CanRetryBusy(exception, startTimestamp, delay))
			{
				// The printer is working on another job; wait, then send the document again from its start
				await Task.Delay(delay, this.context.TimeProvider, cancellationToken).ConfigureAwait(false);
				delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, this.context.BusyRetryMaxDelay.Ticks));
			}
		}
	}

	/// <summary>
	/// Sends Validate-Job with the job's attributes, when the context asks for it and the printer lists the operation
	/// (ADR 0033).
	/// </summary>
	/// <param name="request">The Print-Job request, whose attributes are checked.</param>
	/// <param name="vendorAttributes">The job's vendor attributes, checked too.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>A task that completes when the attributes are accepted, or when the printer couldn't check them
	/// now, as when it is busy: then Print-Job goes ahead and meets the same check.</returns>
	/// <exception cref="PrintException">The printer refused the attributes (an IPP client error), or couldn't be
	/// reached; nothing has been uploaded.</exception>
	private async Task ValidateAsync(PrintJobRequest request, IppVendorAttributes vendorAttributes, CancellationToken cancellationToken)
	{
		if (!this.context.ValidateJobs || !this.supportsValidateJob)
		{
			return;
		}

		ValidateJobRequest validateRequest = new ValidateJobRequest
		{
			OperationAttributes = new ValidateJobOperationAttributes
			{
				PrinterUri         = request.OperationAttributes?.PrinterUri,
				RequestingUserName = request.OperationAttributes?.RequestingUserName,
				DocumentFormat     = request.OperationAttributes?.DocumentFormat,
				JobName            = request.OperationAttributes?.JobName
			},
			JobTemplateAttributes = request.JobTemplateAttributes
		};

		try
		{
			await this.context.Client.ValidateJobAsync(validateRequest, vendorAttributes, cancellationToken).ConfigureAwait(false);
		}
		catch (PrintException exception) when (exception.NativeErrorCode >= (int)IppStatusCode.ServerErrorInternalError)
		{
			// A server error, such as busy, isn't a judgement of the job: Print-Job retries a busy printer, and would
			// meet any refusal itself
			IppLog.ValidationSkipped(this.context.Logger, this.printerUri, exception);
		}
	}

	/// <summary>
	/// Decides whether a failed Print-Job is worth sending again.
	/// </summary>
	/// <param name="exception">The failure.</param>
	/// <param name="startTimestamp">When the first attempt started, from the context's clock.</param>
	/// <param name="delay">The wait before the next attempt.</param>
	/// <returns>Whether the printer was busy and the next attempt still starts within the retry duration.</returns>
	private bool CanRetryBusy(PrintException exception, long startTimestamp, TimeSpan delay)
	{
		if (exception.NativeErrorCode != (int)IppStatusCode.ServerErrorBusy)
		{
			return false;
		}

		TimeSpan elapsed = this.context.TimeProvider.GetElapsedTime(startTimestamp);

		return elapsed + delay <= this.context.BusyRetryDuration;
	}

	/// <summary>
	/// Builds the Print-Job request for a document, without the document, which is added as it is sent.
	/// </summary>
	/// <param name="content">The document to print.</param>
	/// <param name="options">How to print it.</param>
	/// <param name="rasterPlan">The plan for a PDF sent as raster, or <see langword="null"/>.</param>
	/// <returns>The request.</returns>
	private PrintJobRequest CreatePrintJobRequest(PrintContent content, PrintOptions options, RasterJobPlan? rasterPlan)
	{
		string mimeType = rasterPlan?.MimeType ?? this.context.DocumentFormats.GetMimeType(content.Format);
		JobTemplateAttributes jobAttributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		if (rasterPlan is not null)
		{
			// The pages are turned as they are rendered, so the printer mustn't turn them again
			jobAttributes.OrientationRequested = null;
		}

		// An image sent as it is is sized by the printer
		PrintScaling? scaling = ImageDocumentConverter.IsImage(content.Format) ? IppJobTemplateMapper.ToPrintScaling(options.Scaling) : null;

		if (scaling is not null)
		{
			jobAttributes.PrintScaling = new IppValue<PrintScaling>(scaling.Value);
		}

		return new PrintJobRequest
		{
			OperationAttributes = new PrintJobOperationAttributes
			{
				PrinterUri         = new IppValue<Uri>(this.printerUri),
				RequestingUserName = new StringWithLanguage(this.context.RequestingUserName),
				DocumentFormat     = new IppValue<IppDocumentFormat>(new IppDocumentFormat(mimeType)),
				// The explicit cast matters: a bare null would convert through StringWithLanguage's implicit
				// string conversion and send an empty job-name instead of omitting the attribute
				JobName            = options.JobName is null
					? (StringWithLanguage?)null
					: new StringWithLanguage(options.JobName),
				Compression        = rasterPlan?.Gzip == true ? new IppValue<Compression>(Compression.Gzip) : null
			},
			JobTemplateAttributes = jobAttributes
		};
	}

	/// <summary>
	/// Reads the IPP <c>job-id</c> from one of this printer's job IDs.
	/// </summary>
	/// <param name="jobId">The job ID.</param>
	/// <param name="jobNumber">The <c>job-id</c>, if it is one.</param>
	/// <returns>Whether it is an ID this printer could have given: its prefix, then a positive whole number.</returns>
	private bool TryParseJobId(string jobId, out int jobNumber)
	{
		jobNumber = 0;

		if (!jobId.StartsWith(this.JobIdPrefix, StringComparison.Ordinal))
		{
			return false;
		}

		return int.TryParse(jobId.AsSpan(this.JobIdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out jobNumber)
			&& jobNumber > 0;
	}

	/// <summary>
	/// Checks that a job found by its ID is on this printer. CUPS numbers jobs across all its queues, so another
	/// queue's job answers too; a network printer has only its own.
	/// </summary>
	/// <param name="attributes">The job's attributes, as the printer answered.</param>
	/// <returns>
	/// <see langword="false"/> only for a CUPS queue's job that names another queue; <see langword="true"/> otherwise,
	/// including when the job doesn't say, as network printers don't always name themselves by the URI they were
	/// reached at.
	/// </returns>
	private bool IsOwnJob(JobDescriptionAttributes? attributes)
	{
		string queuePath = this.printerUri.AbsolutePath.TrimEnd('/');
		bool isCupsQueue = queuePath.StartsWith("/printers/", StringComparison.OrdinalIgnoreCase)
			|| queuePath.StartsWith("/classes/", StringComparison.OrdinalIgnoreCase);

		Uri? reportedUri = IppValueReader.ReadReference(attributes?.JobPrinterUri);

		if (!isCupsQueue || reportedUri is not { IsAbsoluteUri: true })
		{
			return true;
		}

		// The host and port vary with how CUPS was reached; the queue's path doesn't
		return string.Equals(reportedUri.AbsolutePath.TrimEnd('/'), queuePath, StringComparison.OrdinalIgnoreCase);
	}
}
