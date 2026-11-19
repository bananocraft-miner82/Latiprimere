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

		// Ask the printer to check the job's attributes first, so a refusal comes before the upload, not after it
		await this.ValidateAsync(request, vendorAttributes, cancellationToken).ConfigureAwait(false);

		// Send the document with its job attributes
		PrintJobResponse response = await this
			.SendPrintJobAsync(request, document, vendorAttributes, cancellationToken)
			.ConfigureAwait(false);

		// The job ID is needed to follow or cancel the job, so a response without one is an error
		int jobNumber = response.JobAttributes?.JobId ?? 0;

		if (jobNumber <= 0)
		{
			string message = ExceptionMessages.FormatIppResponseMissingJob(this.printerUri);

			throw new PrintException(message);
		}

		return new IppPrintJob(this.context, this.printerUri, this.Info.Id, jobNumber);
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

		return capabilities with { VendorOptions = vendorOptions };
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
	/// Checks the printer accepts the document's format, when the supported formats are known, and plans a raster
	/// job for a PDF it can't take but can take as raster (ADR 0035).
	/// </summary>
	/// <param name="format">The document format.</param>
	/// <param name="options">The job's options.</param>
	/// <param name="cancellationToken">A token to cancel reading the printer's raster attributes.</param>
	/// <returns>The raster job's plan, or <see langword="null"/> to send the document as it is.</returns>
	/// <exception cref="NotSupportedException">The printer doesn't accept the format, nor PDF as raster; or it
	/// would, but there is no PDF rasteriser.</exception>
	/// <exception cref="PrintException">The printer's raster attributes couldn't be read.</exception>
	private async Task<RasterJobPlan?> PlanRasterAsync(Latiprimere.DocumentFormat format, PrintOptions options, CancellationToken cancellationToken)
	{
		string mimeType = this.context.DocumentFormats.GetMimeType(format);

		if (this.supportedFormats is null || this.supportedFormats.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
		{
			return null;
		}

		string accepted = string.Join(", ", this.supportedFormats);
		RasterFormat? rasterFormat = format == Latiprimere.DocumentFormat.Pdf ? RasterJobPlanner.ChooseFormat(this.supportedFormats) : null;

		if (rasterFormat is null)
		{
			throw new NotSupportedException(ExceptionMessages.FormatDocumentFormatNotSupportedByPrinter(mimeType, accepted));
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
}
