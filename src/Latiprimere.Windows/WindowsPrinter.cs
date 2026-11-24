using System.Globalization;
using System.Xml;
using Latiprimere.Ipp;
using Latiprimere.Ipp.Discovery;
using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Raster;
using Latiprimere.Rendering;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Mapping;
using Latiprimere.Windows.PrintTickets;
using Latiprimere.Windows.Resources;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows;

/// <summary>
/// A printer managed by the Windows spooler.
/// </summary>
/// <remarks>
/// Raw data is sent to the printer unchanged, so <see cref="PrintOptions"/> settings such as duplex, colour and
/// paper size are not applied to it: the data itself controls the output. Copies are made by sending the data again.
/// PDF documents are rasterised (which needs an <see cref="IPdfRasterizer"/>, such as the
/// Latiprimere.Rendering.Pdfium package) and drawn through GDI, with the options applied through the printer driver.
/// A paper size or tray the driver doesn't list is refused, since the driver would print on its default instead.
/// Raw data is refused for drivers known to drop it silently, and PDFs for drivers that print text only (see
/// <see cref="DriverQuirks"/>), unless the PDF goes straight to the queue's IPP printer.
/// With <see cref="WindowsOptions.PrintPdfOverIpp"/> on, a PDF job for a queue whose IPP printer is known and accepts
/// PDF goes straight to the printer instead (ADR 0027).
/// </remarks>
internal sealed class WindowsPrinter : IWindowsPrinter
{
	/// <summary>
	/// Goes before the IPP <c>job-id</c> in the ID of a job sent straight to the queue's IPP printer, to tell it from
	/// the spooler's job IDs, which are numbers too.
	/// </summary>
	internal const string IppJobIdPrefix = "ipp:";

	private static readonly MediaSize A4 = PwgMediaNames.ToMediaSize("iso_a4_210x297mm")!;       // Images' paper in metric regions
	private static readonly MediaSize Letter = PwgMediaNames.ToMediaSize("na_letter_8.5x11in")!; // And elsewhere

	private readonly WindowsPrintContext context; // Spooler, GDI and rasteriser
	private readonly string printerName;          // The spooler's name for the printer
	private readonly string? driverName;          // The printer's driver, if the spooler reported it
	private readonly string? portName;            // The queue's port, which names the printer's URL for some queues
	private volatile WindowsIppMatch? ippMatch;   // The queue's IPP printer, once known to accept PDF or raster
	private volatile bool ippPrinterRefusesPdf;   // Whether the queue's IPP printer said it takes neither
	private volatile bool ippPrinterNeedsAuthentication; // Whether it asks for authentication it won't be given
	private volatile bool lacksPrintTickets;      // Whether the driver turned out to have no Print Ticket support

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsPrinter"/> class.
	/// </summary>
	/// <param name="context">The spooler, GDI and rasteriser.</param>
	/// <param name="printerName">The spooler's name for the printer.</param>
	/// <param name="driverName">The printer's driver name, or <see langword="null"/> if not known.</param>
	/// <param name="portName">The queue's port name, or <see langword="null"/> if not known.</param>
	/// <param name="info">Information about the printer.</param>
	public WindowsPrinter(WindowsPrintContext context, string printerName, string? driverName, string? portName, PrinterInfo info)
	{
		this.context = context;
		this.printerName = printerName;
		this.driverName = driverName;
		this.portName = portName;
		this.Info = info;
	}

	/// <inheritdoc/>
	public PrinterInfo Info { get; }

	/// <inheritdoc/>
	/// <remarks>
	/// Every setting but <see cref="PrintOptions.Finishings"/> and <see cref="PrintOptions.OutputBin"/>, which a
	/// driver reaches only through its Print Capabilities: set them with <c>windows:feature:</c> vendor options
	/// (ADR 0056).
	/// </remarks>
	public PrintOptionKinds AppliedOptions
	{
		get
		{
			return PrintOptionKinds.JobName
					| PrintOptionKinds.Copies
					| PrintOptionKinds.Duplex
					| PrintOptionKinds.Orientation
					| PrintOptionKinds.MediaSize
					| PrintOptionKinds.ColorMode
					| PrintOptionKinds.Scaling
					| PrintOptionKinds.InputTray
					| PrintOptionKinds.VendorOptions
					| PrintOptionKinds.OutputFile
					| PrintOptionKinds.PageRanges
					| PrintOptionKinds.Collate
					| PrintOptionKinds.Quality
					| PrintOptionKinds.MediaType;
		}
	}

	/// <inheritdoc/>
	public string QueueName
	{
		get
		{
			return this.printerName;
		}
	}

	/// <inheritdoc/>
	public Task<string> GetPrintCapabilitiesAsync(string? printTicket = null, CancellationToken cancellationToken = default)
	{
		// Checked here, so a bad ticket fails before any call to the driver
		byte[][] tickets = printTicket is null ? [] : [WindowsVendorOptions.EncodePrintTicket(printTicket)];

		// The Print Ticket API blocks, so run it off the caller's thread
		return Task.Run
		(
			() => PrintSchemaText.ToText(this.context.Spool.GetPrintCapabilities(this.printerName, baseDevMode: null, tickets)),
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public Task<string> GetDefaultPrintTicketAsync(CancellationToken cancellationToken = default)
	{
		return Task.Run
		(
			() => PrintSchemaText.ToText(this.context.Spool.GetDefaultPrintTicket(this.printerName)),
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public async Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(options);

		// Spooler and GDI calls block, so run them off the caller's thread
		string documentName = options.JobName ?? DisplayStrings.DefaultJobName;
		string? outputFile = options.OutputFile is null ? null : Path.GetFullPath(options.OutputFile);

		// Checked for raw jobs too, which don't use a DEVMODE, so an unsupported key is never ignored
		WindowsVendorSettings vendorSettings = WindowsVendorOptions.Read(options.VendorOptions);

		// A PDF, image or text for a queue in front of an IPP printer may go straight to it (ADR 0027, ADR 0047,
		// ADR 0048)
		if (content.Format is DocumentFormat.Pdf || DocumentConverter.CanConvert(content.Format))
		{
			IPrintJob? ippJob = await this.TrySubmitOverIppAsync(content, options, cancellationToken).ConfigureAwait(false);

			if (ippJob is not null)
			{
				return ippJob;
			}
		}

		int jobNumber = content.Format switch
		{
			DocumentFormat.Raw when options.PageRanges.Count > 0 => throw new NotSupportedException(ExceptionMessages.PageRangesNotForRawData),
			DocumentFormat.Raw => await this.SubmitRawAsync(content, options.Copies, documentName, outputFile, cancellationToken)
											.ConfigureAwait(false),
			DocumentFormat.Pdf => await this.SubmitPdfAsync(content, options, vendorSettings, documentName, outputFile, cancellationToken)
											.ConfigureAwait(false),
			DocumentFormat.Jpeg or DocumentFormat.Png or DocumentFormat.Text => await this.SubmitConvertedAsync(content, options, vendorSettings, documentName, outputFile, cancellationToken)
											.ConfigureAwait(false),
			_ => throw WindowsPrinter.CreateFormatNotSupportedException(content.Format),
		};

		return new WindowsPrintJob
		(
			this.context.Spool,
			this.printerName,
			this.Info.Id,
			jobNumber,
			this.context.TimeProvider
		);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The spooler removes a job once it has printed, unless the printer's "Keep printed documents" setting is on. A
	/// job sent straight to the queue's IPP printer, whose ID starts <c>ipp:</c>, is asked of that printer, which needs
	/// <see cref="WindowsOptions.PrintPdfOverIpp"/> on. A printer that can't be reached, or a WSD queue's printer that
	/// can't be found, throws a <see cref="PrintException"/>: the job may still be there.
	/// </remarks>
	public async Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(jobId);

		if (jobId.StartsWith(WindowsPrinter.IppJobIdPrefix, StringComparison.Ordinal))
		{
			IppPrinter? printer = await this.FindIppPrinterForJobAsync(jobId, cancellationToken).ConfigureAwait(false);

			return printer is null
				? null
				: await printer.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
		}

		if (!int.TryParse(jobId, NumberStyles.None, CultureInfo.InvariantCulture, out int jobNumber) || jobNumber <= 0)
		{
			return null;
		}

		// Spooler calls block, so run them off the caller's thread
		WindowsJobRecord? job = await Task.Run(() => this.context.Spool.GetJob(this.printerName, jobNumber), cancellationToken)
										  .ConfigureAwait(false);

		return job is null
			? null
			: new WindowsPrintJob(this.context.Spool, this.printerName, this.Info.Id, jobNumber, this.context.TimeProvider);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The spooler gives no message, so <see cref="PrinterStatus.StateMessage"/> is <see langword="null"/>; its
	/// status flags are given as IPP reasons, such as <c>paused</c> or <c>media-empty-error</c> (ADR 0049).
	/// </remarks>
	public async Task<PrinterStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		// Spooler calls block, so run them off the caller's thread
		WindowsPrinterRecord printer = await Task.Run(() => this.context.Spool.GetPrinter(this.printerName), cancellationToken).ConfigureAwait(false)
			?? throw new PrintException(ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerName));

		return WindowsPrinterStatusMapper.ToPrinterStatus(printer, this.Info.Id, this.context.TimeProvider.GetUtcNow());
	}

	/// <inheritdoc/>
	/// <remarks>
	/// The spooler signals each change to the printer, so its state is read when one comes, and at least every 30
	/// seconds, as a driver's own status, such as paper out, may not be signalled (ADR 0049). If the spooler can't
	/// signal, or stops being able to, the state is read every <paramref name="pollInterval"/>. A state that can't be
	/// read is reported as <see cref="PrinterState.Unknown"/>, with why as its message, and the watch goes on.
	/// </remarks>
	public IAsyncEnumerable<PrinterStatus> WatchStatusAsync(TimeSpan pollInterval, CancellationToken cancellationToken = default)
	{
		return WindowsStatusWatch.WatchAsync
		(
			() => this.context.Spool.OpenChangeNotification(this.printerName, WindowsChanges.Printer),
			this.GetStatusAsync,
			WindowsStatusWatch.HasChanged,
			static status => false,
			pollInterval,
			exception => PrinterStatusFailure.Create(this.Info.Id, exception, this.context.TimeProvider.GetUtcNow()),
			cancellationToken
		);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// With <see cref="WindowsOptions.PrintPdfOverIpp"/> on, <see cref="PrinterCapabilities.DocumentFormats"/> adds
	/// what the queue's IPP printer can be sent, which needs the printer to be found, as the first PDF job does.
	/// </remarks>
	public async Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		// DeviceCapabilities and DocumentProperties block, so run them off the caller's thread
		// A driver without Print Ticket support isn't asked again, so it costs one try and one log entry
		bool includePrintSchema = !this.lacksPrintTickets;
		WindowsCapabilitiesRecord? record = await Task.Run(() => this.context.Spool.GetCapabilities(this.printerName, includePrintSchema), cancellationToken)
													  .ConfigureAwait(false);

		if (record is null)
		{
			string message = ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerName);

			throw new PrintException(message);
		}

		List<DocumentFormat> formats = this.ListDocumentFormats();

		// What the queue's IPP printer takes can go to it straight, whatever the driver can do (ADR 0027)
		WindowsIppMatch? match = await this.FindIppPrinterAsync(PrintOptions.Default, cancellationToken).ConfigureAwait(false);

		if (match is not null)
		{
			formats = formats
				.Union(match.Printer.ListDocumentFormats().Where(format => format != DocumentFormat.Raw))
				.Order()
				.ToList();
		}

		return WindowsCapabilitiesMapper.ToCapabilities(record) with
		{
			DocumentFormats = formats,
			VendorOptions   = this.ReadVendorOptions(record)
		};
	}

	/// <summary>
	/// Lists the document formats the queue can be sent: raw data, unless its driver drops it, and PDFs, images and
	/// text, which need a PDF rasteriser and a driver that prints more than text, as they are drawn as pictures.
	/// </summary>
	/// <returns>The formats.</returns>
	private List<DocumentFormat> ListDocumentFormats()
	{
		List<DocumentFormat> formats = [];

		if (this.context.PdfRasterizer is not null && !DriverQuirks.PrintsTextOnly(this.driverName))
		{
			formats.Add(DocumentFormat.Pdf);
		}

		if (!DriverQuirks.DropsRawData(this.driverName))
		{
			formats.Add(DocumentFormat.Raw);
		}

		if (formats.Contains(DocumentFormat.Pdf))
		{
			formats.Add(DocumentFormat.Jpeg);
			formats.Add(DocumentFormat.Png);
			formats.Add(DocumentFormat.Text);
		}

		return formats;
	}

	/// <summary>
	/// Lists the driver's Print Capabilities as vendor options. They are an extra: a driver without Print Ticket
	/// support, or with a document that can't be read, lists fewer or none, and that is logged (ADR 0032).
	/// </summary>
	/// <param name="record">The driver's capabilities.</param>
	/// <returns>The vendor options.</returns>
	private IReadOnlyList<VendorOption> ReadVendorOptions(WindowsCapabilitiesRecord record)
	{
		if (record.PrintSchemaFailure is not null)
		{
			this.lacksPrintTickets = record.PrintSchemaFailure is PrintException;
			WindowsLog.PrintCapabilitiesUnavailable(this.context.Logger, this.printerName, record.PrintSchemaFailure);
		}

		try
		{
			return WindowsCapabilitiesMapper.ToVendorOptions(record, withDefaults: true);
		}
		catch (XmlException exception)
		{
			WindowsLog.PrintCapabilitiesUnreadable(this.context.Logger, this.printerName, exception);
		}

		// Perhaps only the default ticket was unreadable: the options can still be listed, without their defaults.
		// A Print Capabilities document that can't be read was logged just now.
		try
		{
			return WindowsCapabilitiesMapper.ToVendorOptions(record, withDefaults: false);
		}
		catch (XmlException)
		{
			return Array.Empty<VendorOption>();
		}
	}

	/// <summary>
	/// Sends a PDF straight to the queue's IPP printer, when that is on, known, and suits the job.
	/// </summary>
	/// <param name="content">The PDF.</param>
	/// <param name="options">How to print it.</param>
	/// <param name="cancellationToken">A token to cancel the submission.</param>
	/// <returns>The printer's job, or <see langword="null"/> to print through the spooler instead: the job can't
	/// go over IPP, or the printer couldn't be reached before anything was sent.</returns>
	/// <exception cref="PrintException">The printer refused the job, or the connection failed after it opened.</exception>
	private async Task<IPrintJob?> TrySubmitOverIppAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken)
	{
		WindowsIppMatch? match = await this.FindIppPrinterAsync(options, cancellationToken).ConfigureAwait(false);

		if (match is null)
		{
			return null;
		}

		Stream document = content.Content;
		long? documentStart = document.CanSeek ? document.Position : null;

		try
		{
			return await match.Printer.SubmitAsync(content, options, cancellationToken).ConfigureAwait(false);
		}
		catch (PrintException exception)
			when (IppConnectionErrors.IsConnectFailure(exception) || IppConnectionErrors.IsSecureConnectionFailure(exception))
		{
			// Moved or switched off since it was found, or its certificate changed: the next job asks afresh,
			// searching again by UUID, rather than waiting for the same address to fail each time
			this.LogIppPrinterUnavailable(match.Printer.PrinterUri, exception);
			this.ForgetIppPrinter(match);

			if (documentStart is null)
			{
				throw;
			}

			// Nothing reached the printer, so the spooler can hold the job until it is back without printing it twice
			document.Position = documentStart.Value;

			return null;
		}
		catch (NotSupportedException exception) when (documentStart is not null && exception is not TextCharacterNotSupportedException)
		{
			// A raster job the printer's attributes don't allow, such as no paper size: nothing was sent, and the
			// driver may still print it (ADR 0036). Text with a character no layout can show isn't one: the driver
			// would refuse it too, so that refusal is the caller's (ADR 0048)
			WindowsLog.RasterNotPossible(this.context.Logger, this.printerName, exception);
			document.Position = documentStart.Value;

			return null;
		}
	}

	/// <summary>
	/// Finds the queue's IPP printer for a job, asking it once whether it accepts PDF.
	/// </summary>
	/// <param name="options">The job's options; some need the spooler.</param>
	/// <param name="cancellationToken">A token to cancel the question.</param>
	/// <returns>The printer and the UUID it was found by, or <see langword="null"/> if this job goes through the
	/// spooler.</returns>
	private async Task<WindowsIppMatch?> FindIppPrinterAsync(PrintOptions options, CancellationToken cancellationToken)
	{
		WindowsIppRoute? route = this.context.IppRoute;

		// Printing to a file, a Windows tray number and a driver's DEVMODE all need the spooler
		bool needsSpooler = options.OutputFile is not null
							|| options.InputTray is not null
							|| options.VendorOptions.Keys.Any(key => key.StartsWith(WindowsVendorOptions.Prefix, StringComparison.Ordinal));

		if (route is null || needsSpooler || this.ippPrinterRefusesPdf || this.ippPrinterNeedsAuthentication)
		{
			return null;
		}

		WindowsIppMatch? known = this.ippMatch;

		if (known is not null)
		{
			return known;
		}

		// An exact URI, or a WSD queue's printer found by the UUID its port records (ADR 0036)
		WindowsIppTarget? target = await route
			.FindTargetAsync(this.printerName, this.portName, this.context.Spool, cancellationToken)
			.ConfigureAwait(false);

		if (target is null)
		{
			return null;
		}

		Uri printerUri;
		SharpIpp.Protocol.Models.PrinterDescriptionAttributes? attributes;

		try
		{
			(printerUri, attributes) = await this.ReadIppPrinterAsync(route, target, cancellationToken).ConfigureAwait(false);
		}
		catch (PrintException exception)
		{
			// Unreachable, untrusted or wants a password: the spooler prints this job, and the next one asks again,
			// searching afresh for a printer found by UUID, which may have moved
			this.LogIppPrinterUnavailable(target.PrinterUri, exception);

			if (target.Uuid is not null)
			{
				route.ForgetMatch(target.Uuid);
			}

			return null;
		}

		if (target.Uuid is not null && !WindowsPrinter.IsSamePrinter(target.Uuid, attributes))
		{
			// Gone from where it was found, or another printer answers there now: search again at the next job
			WindowsLog.IppPrinterNotMatched(this.context.Logger, this.printerName, printerUri);
			route.ForgetMatch(target.Uuid);

			return null;
		}

		// A printer that says it wants a password it won't be sent, or authentication Latiprimere can't give, would
		// refuse the job; the driver prints it instead (ADR 0042). That won't change while this object lives
		PrinterUriRequirements? requirements = attributes is null ? null : PrinterUriRequirementsReader.Read(attributes, printerUri);

		if (requirements is not null
			&& (requirements.AsksForUnsupportedAuthentication || (requirements.AsksForPassword && route.Security.FindCredentialsToSend(printerUri) is null)))
		{
			WindowsLog.IppPrinterNeedsAuthentication(this.context.Logger, this.printerName, printerUri, requirements.Authentication);
			this.ippPrinterNeedsAuthentication = true;

			return null;
		}

		IReadOnlyCollection<string>? formats = attributes is null
			? null
			: IppValueReader.ReadReference(attributes.DocumentFormatSupported)?.Select(format => format.Value).ToList();

		// Whether it can check a job before its upload (ADR 0033)
		IReadOnlyCollection<SharpIpp.Protocol.Models.IppOperation>? operations = attributes is null
			? null
			: IppValueReader.ReadReference(attributes.OperationsSupported);

		string pdf = route.Context.DocumentFormats.GetMimeType(DocumentFormat.Pdf);

		// PDF as it is, or rendered to a raster format the printer takes when there is a rasteriser (ADR 0036)
		bool takesPdf = formats?.Contains(pdf, StringComparer.OrdinalIgnoreCase) == true;
		bool takesRaster = formats is not null
						   && route.Context.PdfRasterizer is not null
						   && RasterJobPlanner.ChooseFormat(formats, route.Context.RasterFormats) is not null;

		if (!takesPdf && !takesRaster)
		{
			// No printer at that URI, or one that takes neither: that won't change while this object lives
			this.ippPrinterRefusesPdf = true;

			return null;
		}

		if (target.Uuid is not null)
		{
			WindowsLog.IppPrinterMatched(this.context.Logger, this.printerName, printerUri);
		}

		WindowsIppMatch match = new WindowsIppMatch
		{
			Printer = new IppPrinter(route.Context, printerUri, this.Info, formats, operations)
			{
				JobIdPrefix     = WindowsPrinter.IppJobIdPrefix,
				UriRequirements = requirements
			},
			Uuid = target.Uuid
		};

		this.ippMatch = match;

		return match;
	}

	/// <summary>
	/// Finds the queue's IPP printer to ask about a job sent to it earlier, whether or not it takes PDF now.
	/// </summary>
	/// <param name="jobId">The job's ID, for the message if the printer can't be found.</param>
	/// <param name="cancellationToken">A token to cancel the search.</param>
	/// <returns>The printer, or <see langword="null"/> if no job of this queue can have gone over IPP: the route is
	/// off, or the queue has no IPP printer to send to.</returns>
	/// <exception cref="PrintException">The queue's printer is found by UUID, and the search didn't find it.</exception>
	private async Task<IppPrinter?> FindIppPrinterForJobAsync(string jobId, CancellationToken cancellationToken)
	{
		WindowsIppRoute? route = this.context.IppRoute;

		if (route is null || !route.MayHaveTarget(this.printerName, this.portName, this.context.Spool))
		{
			return null;
		}

		WindowsIppMatch? known = this.ippMatch;

		if (known is not null)
		{
			return known.Printer;
		}

		WindowsIppTarget? target = await route
			.FindTargetAsync(this.printerName, this.portName, this.context.Spool, cancellationToken)
			.ConfigureAwait(false);

		if (target is null)
		{
			throw new PrintException(ExceptionMessages.FormatIppJobPrinterNotFound(jobId, this.printerName));
		}

		// Only read from, so it isn't kept for PDF jobs, which check first what the printer takes
		return new IppPrinter(route.Context, target.PrinterUri, this.Info)
		{
			JobIdPrefix = WindowsPrinter.IppJobIdPrefix
		};
	}

	/// <summary>
	/// Forgets the queue's IPP printer, and the route's match for its UUID, so the next PDF job finds it again; unless
	/// another job has found it again already.
	/// </summary>
	/// <param name="failed">The match that couldn't be reached.</param>
	private void ForgetIppPrinter(WindowsIppMatch failed)
	{
		if (Interlocked.CompareExchange(ref this.ippMatch, null, failed) == failed && failed.Uuid is not null)
		{
			this.context.IppRoute?.ForgetMatch(failed.Uuid);
		}
	}

	/// <summary>
	/// Asks the queue's IPP printer what it takes and asks for: at its IPPS address first when the route prefers it,
	/// and at its plain IPP address if the encrypted connection can't be opened (ADR 0037, ADR 0042).
	/// </summary>
	/// <param name="route">The route.</param>
	/// <param name="target">Where the printer is.</param>
	/// <param name="cancellationToken">A token to cancel the question.</param>
	/// <returns>The address that answered, and what the printer reported, or <see langword="null"/> attributes if there
	/// was no printer there.</returns>
	/// <exception cref="PrintException">The printer couldn't be reached, its certificate isn't trusted, or it refused
	/// the question.</exception>
	private async Task<(Uri PrinterUri, SharpIpp.Protocol.Models.PrinterDescriptionAttributes? Attributes)> ReadIppPrinterAsync
	(
		WindowsIppRoute   route,
		WindowsIppTarget  target,
		CancellationToken cancellationToken
	)
	{
		if (target.FallbackUri is not null)
		{
			try
			{
				return (target.PrinterUri, await WindowsPrinter.QueryIppPrinterAsync(route, target.PrinterUri, cancellationToken).ConfigureAwait(false));
			}
			catch (PrintException exception)
				when (IppConnectionErrors.IsSecureConnectionFailure(exception) || IppConnectionErrors.IsConnectFailure(exception))
			{
				// Nothing was sent, so plain IPP may still answer; a refusal by the printer itself is the answer
				WindowsLog.IppSecureAddressUnusable(this.context.Logger, this.printerName, target.PrinterUri, target.FallbackUri, exception);
			}

			return (target.FallbackUri, await WindowsPrinter.QueryIppPrinterAsync(route, target.FallbackUri, cancellationToken).ConfigureAwait(false));
		}

		return (target.PrinterUri, await WindowsPrinter.QueryIppPrinterAsync(route, target.PrinterUri, cancellationToken).ConfigureAwait(false));
	}

	/// <summary>
	/// Asks an IPP printer for what routing a queue's jobs to it needs: the formats and operations it takes, its UUID,
	/// and what it asks for at each of its addresses.
	/// </summary>
	/// <param name="route">The route.</param>
	/// <param name="printerUri">The printer's address.</param>
	/// <param name="cancellationToken">A token to cancel the question.</param>
	/// <returns>Its attributes, or <see langword="null"/> if there was no printer there.</returns>
	/// <exception cref="PrintException">The printer couldn't be reached, or refused the question.</exception>
	private static Task<SharpIpp.Protocol.Models.PrinterDescriptionAttributes?> QueryIppPrinterAsync
	(
		WindowsIppRoute   route,
		Uri               printerUri,
		CancellationToken cancellationToken
	)
	{
		return IppPrinterQueries.GetAttributesAsync
		(
			route.Context,
			printerUri,
			[
				IppRequestedAttributes.DocumentFormatSupported,
				IppRequestedAttributes.OperationsSupported,
				IppRequestedAttributes.PrinterUuid,
				IppRequestedAttributes.PrinterUriSupported,
				IppRequestedAttributes.UriSecuritySupported,
				IppRequestedAttributes.UriAuthenticationSupported
			],
			cancellationToken
		);
	}

	/// <summary>
	/// Logs why the queue's IPP printer can't take its jobs, which go through the queue instead: an untrusted
	/// certificate at Warning, with the fingerprint to pin, as it won't trust itself; anything else at Information.
	/// </summary>
	/// <param name="printerUri">The printer's address.</param>
	/// <param name="exception">Why.</param>
	private void LogIppPrinterUnavailable(Uri printerUri, PrintException exception)
	{
		if (exception is PrintCertificateException { CertificateFingerprint: { } fingerprint })
		{
			WindowsLog.IppCertificateNotTrusted(this.context.Logger, this.printerName, printerUri, fingerprint, exception);

			return;
		}

		WindowsLog.IppPrinterUnavailable(this.context.Logger, this.printerName, printerUri, exception);
	}

	/// <summary>
	/// Checks that the printer found by a WSD queue's UUID reports that UUID itself as its <c>printer-uuid</c>.
	/// </summary>
	/// <param name="uuid">The normalised UUID the queue's port records.</param>
	/// <param name="attributes">What the printer reported, or <see langword="null"/> if there was no printer there.</param>
	/// <returns>Whether it is the queue's printer; one that doesn't report a UUID isn't trusted.</returns>
	private static bool IsSamePrinter(string uuid, SharpIpp.Protocol.Models.PrinterDescriptionAttributes? attributes)
	{
		string? reported = attributes is null ? null : DnsSdServiceMapper.NormalizeUuid(IppValueReader.ReadReference(attributes.PrinterUUID));

		return reported == uuid;
	}

	/// <summary>
	/// Creates the exception for a document format this printer can't take.
	/// </summary>
	/// <param name="format">The format.</param>
	/// <returns>The exception.</returns>
	private static NotSupportedException CreateFormatNotSupportedException(DocumentFormat format)
	{
		string message = ExceptionMessages.FormatDocumentFormatNotSupported(format);

		return new NotSupportedException(message);
	}

	/// <summary>
	/// Sends raw data to the spooler unchanged, unless the printer's driver is known to drop it.
	/// </summary>
	/// <param name="content">The raw data.</param>
	/// <param name="copies">The number of copies; the data is sent once for each.</param>
	/// <param name="documentName">The name shown in the print queue.</param>
	/// <param name="outputFile">A file to print to instead of the device, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">A token to abandon the submission; while data is being sent, the job is
	/// then removed from the queue.</param>
	/// <returns>The spooler's job ID.</returns>
	/// <exception cref="NotSupportedException">The printer's driver drops raw data without reporting an error, or
	/// more than one copy was asked for from a stream that can't seek.</exception>
	private Task<int> SubmitRawAsync
	(
		PrintContent      content,
		int               copies,
		string            documentName,
		string?           outputFile,
		CancellationToken cancellationToken
	)
	{
		// Such a driver would report the job finished with nothing printed, so refuse before anything is queued
		if (DriverQuirks.DropsRawData(this.driverName))
		{
			string message = ExceptionMessages.FormatDriverDropsRawData(this.printerName, this.driverName);

			throw new NotSupportedException(message);
		}

		// RAW data bypasses the driver, so each copy is sent again from the start of the data
		if (copies > 1 && !content.Content.CanSeek)
		{
			string message = ExceptionMessages.FormatRawCopiesNeedSeekableStream(copies);

			throw new NotSupportedException(message);
		}

		// Spooler calls block, so run them off the caller's thread
		return Task.Run
		(
			() => this.context.Spool.SubmitRawJob
			(
				this.printerName,
				documentName,
				content.Content,
				copies,
				outputFile,
				cancellationToken
			),
			cancellationToken
		);
	}

	/// <summary>
	/// Rasterises a PDF and prints it through GDI.
	/// </summary>
	/// <param name="content">The PDF.</param>
	/// <param name="options">How to print it.</param>
	/// <param name="vendorSettings">A DEVMODE from <c>windows:devmode</c> to start from instead of the driver's
	/// defaults, and a Print Ticket from <c>windows:print-ticket</c> to merge onto it; either may be
	/// absent.</param>
	/// <param name="documentName">The name shown in the print queue.</param>
	/// <param name="outputFile">A file to print to instead of the device, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">A token to abandon spooling.</param>
	/// <returns>The spooler's job ID.</returns>
	/// <exception cref="NotSupportedException">The driver prints text only, no PDF rasteriser is registered, or the
	/// driver doesn't list the chosen paper size or tray.</exception>
	private Task<int> SubmitPdfAsync
	(
		PrintContent          content,
		PrintOptions          options,
		WindowsVendorSettings vendorSettings,
		string                documentName,
		string?               outputFile,
		CancellationToken     cancellationToken
	)
	{
		// The driver would take the page images and then stop the queue with an error, so refuse before rendering
		if (DriverQuirks.PrintsTextOnly(this.driverName))
		{
			string message = ExceptionMessages.FormatDriverPrintsTextOnly(this.printerName, this.driverName);

			throw new NotSupportedException(message);
		}

		IPdfRasterizer rasterizer = this.context.PdfRasterizer
										?? throw new NotSupportedException(ExceptionMessages.PdfNeedsRasterizer);

		return Task.Run
		(
			() =>
			{
				// Before the document is opened, so a size or tray the driver lacks fails without rendering anything
				PaperChoice paper = this.FindPaperAndBin(options);

				// Also before, so a feature or option the printer lacks fails without rendering anything
				List<byte[]> tickets = this.CreatePrintTickets(options, vendorSettings);

				using IRasterDocument document = rasterizer.Open(content.Content, new PdfOpenOptions { Password = content.Password });

				// Only the pages chosen; ranges that leave none fail before anything is spooled (ADR 0056)
				IReadOnlyList<int> pages = Latiprimere.Ipp.Documents.PageSelection.Select(options.PageRanges, document.PageCount);

				GdiPrintRequest request = new GdiPrintRequest
				{
					PrinterName  = this.printerName,
					DocumentName = documentName,
					OutputFile   = outputFile,
					Document     = document,
					Options      = options,
					PaperId      = paper.PaperId,
					BinId        = paper.BinId,
					CustomPaper  = paper.CustomPaper,
					MediaTypeId  = paper.MediaTypeId,
					Pages        = pages,
					SavedDevMode = vendorSettings.DevMode,
					PrintTickets = tickets
				};

				return this.context.Gdi.PrintRasterDocument(request, cancellationToken);
			},
			cancellationToken
		);
	}

	/// <summary>
	/// Lays an image or text out on the job's paper as a PDF, then prints that as PDFs are (ADR 0047, ADR 0048). The
	/// paper is the job's, or the driver's default. An image's page is landscape when it is wider than tall, unless
	/// the job says otherwise; text's pages are landscape when the job says so. The job is printed in the pages'
	/// orientation, so the driver's paper matches them.
	/// </summary>
	/// <param name="content">The JPEG, PNG or text.</param>
	/// <param name="options">How to print it.</param>
	/// <param name="vendorSettings">The job's <c>windows:</c> vendor options.</param>
	/// <param name="documentName">The name shown in the print queue.</param>
	/// <param name="outputFile">A file to print to instead of the device, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">A token to abandon spooling.</param>
	/// <returns>The spooler's job ID.</returns>
	/// <exception cref="NotSupportedException">No PDF rasteriser is registered, text has a character that can't be
	/// shown, or the job can't be printed as a PDF.</exception>
	/// <exception cref="PrintException">An image can't be read.</exception>
	private async Task<int> SubmitConvertedAsync
	(
		PrintContent          content,
		PrintOptions          options,
		WindowsVendorSettings vendorSettings,
		string                documentName,
		string?               outputFile,
		CancellationToken     cancellationToken
	)
	{
		// Refused as a PDF would be, before anything is read or laid out
		if (DriverQuirks.PrintsTextOnly(this.driverName))
		{
			string message = ExceptionMessages.FormatDriverPrintsTextOnly(this.printerName, this.driverName);

			throw new NotSupportedException(message);
		}

		if (this.context.PdfRasterizer is null)
		{
			throw new NotSupportedException(ExceptionMessages.ImageNeedsRasterizer);
		}

		// Reading the driver's default paper and the document both block, so run them off the caller's thread
		(MemoryStream pdf, bool landscape) = await Task.Run
		(
			() => DocumentConverter.ToPdf(content, options.MediaSize ?? this.FindDefaultPaper(), options),
			cancellationToken
		).ConfigureAwait(false);

		using (pdf)
		{
			PrintOptions pdfOptions = options with
			{
				Orientation = landscape ? PageOrientation.Landscape : PageOrientation.Portrait,
				Scaling     = ScalingMode.PrinterDefault
			};

			return await this
				.SubmitPdfAsync(PrintContent.FromPdf(pdf), pdfOptions, vendorSettings, documentName, outputFile, cancellationToken)
				.ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Finds the paper an image or text goes on when the job chooses none: the driver's default, or, if it gives none, A4 or
	/// US Letter as the region uses.
	/// </summary>
	/// <returns>The paper.</returns>
	/// <exception cref="PrintException">The printer can no longer be found.</exception>
	private MediaSize FindDefaultPaper()
	{
		WindowsCapabilitiesRecord record = this.context.Spool.GetCapabilities(this.printerName, includePrintSchema: false)
			?? throw new PrintException(ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerName));

		return WindowsCapabilitiesMapper.ToCapabilities(record).DefaultMediaSize
			?? (RegionInfo.CurrentRegion.IsMetric ? WindowsPrinter.A4 : WindowsPrinter.Letter);
	}

	/// <summary>
	/// Lists the Print Tickets a PDF job merges onto its DEVMODE: the one from <c>windows:print-ticket</c>, then one
	/// made from the <c>windows:feature:</c> and <c>windows:parameter:</c> keys, checked against the driver's Print
	/// Capabilities for the job's starting DEVMODE and the settings the first ticket chooses (ADR 0032).
	/// </summary>
	/// <param name="options">The job's options.</param>
	/// <param name="vendorSettings">The job's <c>windows:</c> vendor options.</param>
	/// <returns>The tickets, in the order they are merged; empty if there are none.</returns>
	/// <exception cref="ArgumentException">A feature, parameter or option isn't one the printer offers, or a typed
	/// option sets it.</exception>
	/// <exception cref="PrintException">The driver's Print Capabilities couldn't be read.</exception>
	private List<byte[]> CreatePrintTickets(PrintOptions options, WindowsVendorSettings vendorSettings)
	{
		List<byte[]> tickets = [];

		if (vendorSettings.PrintTicket is not null)
		{
			tickets.Add(vendorSettings.PrintTicket);
		}

		if (vendorSettings.Features.Count == 0)
		{
			return tickets;
		}

		// For the settings the job starts from, so an option a saved DEVMODE rules in or out is judged as printed
		byte[] capabilities = this.context.Spool.GetPrintCapabilities(this.printerName, vendorSettings.DevMode, tickets.ToArray());
		PrintCapabilitiesDocument document;

		try
		{
			document = PrintCapabilitiesDocument.Parse(capabilities);
		}
		catch (XmlException exception)
		{
			string message = ExceptionMessages.FormatPrintCapabilitiesUnreadable(this.printerName, exception.Message);

			throw new PrintException(message, exception);
		}

		byte[]? featureTicket = document.CreateTicket(this.printerName, vendorSettings.Features, options);

		if (featureTicket is not null)
		{
			tickets.Add(featureTicket);
		}

		return tickets;
	}

	/// <summary>
	/// Finds the driver's numbers for the chosen paper size, tray and paper type. The driver's lists are only read
	/// when one is chosen.
	/// </summary>
	/// <param name="options">The print options.</param>
	/// <returns>The driver's numbers, each <see langword="null"/> when not chosen; a size the driver doesn't list but
	/// takes as a custom size comes as its dimensions instead.</returns>
	/// <exception cref="NotSupportedException">The driver doesn't list the chosen size, tray or type, nor take the
	/// size as a custom one.</exception>
	/// <exception cref="PrintException">The printer can no longer be found.</exception>
	private PaperChoice FindPaperAndBin(PrintOptions options)
	{
		if (options.MediaSize is null && options.InputTray is null && options.MediaType is null)
		{
			return new PaperChoice(null, null, null, null);
		}

		// Only the papers and trays are needed, so no Print Ticket session
		WindowsCapabilitiesRecord record = this.context.Spool.GetCapabilities(this.printerName, includePrintSchema: false)
			?? throw new PrintException(ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerName));

		short? paperId = null;
		short? binId = null;
		(short Width, short Length)? customPaper = null;
		uint? mediaTypeId = null;

		if (options.MediaSize is not null)
		{
			paperId = WindowsCapabilitiesMapper.FindPaperId(record, options.MediaSize.Name);

			// A size the driver doesn't list may still be one it takes as a custom size (ADR 0056)
			if (paperId is null && WindowsCapabilitiesMapper.ToCustomRange(record)?.Contains(options.MediaSize) == true)
			{
				customPaper = ((short)Math.Round(options.MediaSize.WidthMillimeters * 10), (short)Math.Round(options.MediaSize.HeightMillimeters * 10));
			}
			else if (paperId is null)
			{
				throw new NotSupportedException
				(
					ExceptionMessages.FormatMediaSizeNotListed(this.printerName, options.MediaSize.DisplayName, options.MediaSize.Name)
				);
			}
		}

		if (options.MediaType is not null)
		{
			mediaTypeId = WindowsCapabilitiesMapper.FindMediaTypeId(record, options.MediaType.Id)
				?? throw new NotSupportedException
				(
					ExceptionMessages.FormatMediaTypeNotListed(this.printerName, options.MediaType.Name, options.MediaType.Id)
				);
		}

		if (options.InputTray is not null)
		{
			binId = WindowsCapabilitiesMapper.FindBinId(record, options.InputTray.Id)
				?? throw new NotSupportedException
				(
					ExceptionMessages.FormatInputTrayNotListed(this.printerName, options.InputTray.Name, options.InputTray.Id)
				);
		}

		return new PaperChoice(paperId, binId, customPaper, mediaTypeId);
	}

	/// <summary>
	/// The driver's numbers for a job's paper.
	/// </summary>
	/// <param name="PaperId">The DMPAPER_* size, or <see langword="null"/>.</param>
	/// <param name="BinId">The DMBIN_* tray, or <see langword="null"/>.</param>
	/// <param name="CustomPaper">A custom size in tenths of a millimetre, or <see langword="null"/>.</param>
	/// <param name="MediaTypeId">The DMMEDIA_* type, or <see langword="null"/>.</param>
	private readonly record struct PaperChoice(short? PaperId, short? BinId, (short Width, short Length)? CustomPaper, uint? MediaTypeId);
}
