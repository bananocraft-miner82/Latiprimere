using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Resources;
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
	public IppPrinter(IppPrintContext context, Uri printerUri, PrinterInfo info, IReadOnlyCollection<string>? supportedFormats = null)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(printerUri);
		ArgumentNullException.ThrowIfNull(info);

		this.context = context;
		this.printerUri = printerUri;
		this.Info = info;
		this.supportedFormats = supportedFormats;
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

		this.EnsureFormatSupported(content.Format);

		// Vendor options are checked against the typed request before anything is sent
		PrintJobRequest request = this.CreatePrintJobRequest(content, options);

		IppVendorAttributes vendorAttributes = IppVendorAttributeMapper.ToAttributes
		(
			options.VendorOptions,
			this.context.VendorOptionPrefixes,
			request
		);

		// Send the document with its job attributes
		PrintJobResponse response = await this
			.SendPrintJobAsync(request, content.Content, vendorAttributes, cancellationToken)
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

		try
		{
			return IppCapabilitiesMapper.ToCapabilities(attributes);
		}
		catch (Exception exception) when (IppResponseErrors.IsUnreadableData(exception))
		{
			throw IppResponseErrors.CreateUnreadable(this.printerUri, exception);
		}
	}

	/// <summary>
	/// Checks the printer accepts the document's format, when the supported formats are known.
	/// </summary>
	/// <param name="format">The document format.</param>
	/// <exception cref="NotSupportedException">The printer does not accept the format.</exception>
	private void EnsureFormatSupported(Latiprimere.DocumentFormat format)
	{
		string mimeType = this.context.DocumentFormats.GetMimeType(format);

		if (this.supportedFormats is null || this.supportedFormats.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
		{
			return;
		}

		string message = ExceptionMessages.FormatDocumentFormatNotSupportedByPrinter(mimeType, string.Join(", ", this.supportedFormats));

		throw new NotSupportedException(message);
	}

	/// <summary>
	/// Sends the Print-Job request, sending it again while the printer is busy with another job, for up to
	/// <see cref="IppPrintContext.BusyRetryDuration"/>.
	/// </summary>
	/// <param name="request">The request, including the document.</param>
	/// <param name="document">The request's document, rewound before each resend.</param>
	/// <param name="vendorAttributes">Attributes from the vendor options.</param>
	/// <param name="cancellationToken">A token to cancel the request or the wait between attempts.</param>
	/// <returns>The printer's response.</returns>
	/// <exception cref="PrintException">The printer rejected the job, or was still busy when the time ran out.</exception>
	private async Task<PrintJobResponse> SendPrintJobAsync
	(
		PrintJobRequest     request,
		Stream              document,
		IppVendorAttributes vendorAttributes,
		CancellationToken   cancellationToken
	)
	{
		// The document can only be sent again from where it started, so only seekable streams are retried
		long? documentStart = document.CanSeek ? document.Position : null;
		long startTimestamp = this.context.TimeProvider.GetTimestamp();
		TimeSpan delay = this.context.BusyRetryFirstDelay;

		while (true)
		{
			try
			{
				return await this.context.Client
					.PrintJobAsync(request, vendorAttributes, cancellationToken)
					.ConfigureAwait(false);
			}
			catch (PrintException exception) when (documentStart is not null && this.CanRetryBusy(exception, startTimestamp, delay))
			{
				// The printer is working on another job; wait, then send the document again from its start
				await Task.Delay(delay, this.context.TimeProvider, cancellationToken).ConfigureAwait(false);
				document.Position = documentStart.Value;
				delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, this.context.BusyRetryMaxDelay.Ticks));
			}
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
	/// Builds the Print-Job request for a document.
	/// </summary>
	/// <param name="content">The document to print.</param>
	/// <param name="options">How to print it.</param>
	/// <returns>The request.</returns>
	private PrintJobRequest CreatePrintJobRequest(PrintContent content, PrintOptions options)
	{
		string mimeType = this.context.DocumentFormats.GetMimeType(content.Format);

		return new PrintJobRequest
		{
			Document = content.Content,
			OperationAttributes = new PrintJobOperationAttributes
			{
				PrinterUri         = new IppValue<Uri>(this.printerUri),
				RequestingUserName = new StringWithLanguage(this.context.RequestingUserName),
				DocumentFormat     = new IppValue<IppDocumentFormat>(new IppDocumentFormat(mimeType)),
				// The explicit cast matters: a bare null would convert through StringWithLanguage's implicit
				// string conversion and send an empty job-name instead of omitting the attribute
				JobName            = options.JobName is null
					? (StringWithLanguage?)null
					: new StringWithLanguage(options.JobName)
			},
			JobTemplateAttributes = IppJobTemplateMapper.ToJobTemplateAttributes(options)
		};
	}
}
