using Latiprimere.Ipp;
using Latiprimere.Ipp.Mapping;
using Latiprimere.Rendering;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Mapping;
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
/// Raw data is refused for drivers known to drop it silently (see <see cref="DriverQuirks"/>).
/// With <see cref="WindowsOptions.PrintPdfOverIpp"/> on, a PDF job for a queue whose IPP printer is known and accepts
/// PDF goes straight to the printer instead (ADR 0027).
/// </remarks>
internal sealed class WindowsPrinter : IPrinter
{
	private readonly WindowsPrintContext context; // Spooler, GDI and rasteriser
	private readonly string printerName;          // The spooler's name for the printer
	private readonly string? driverName;          // The printer's driver, if the spooler reported it
	private readonly string? portName;            // The queue's port, which names the printer's URL for some queues
	private IppPrinter? ippPrinter;               // The queue's IPP printer, once known to accept PDF
	private bool ippPrinterRefusesPdf;            // Whether the queue's IPP printer said it doesn't accept PDF

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
	public async Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(options);

		// Spooler and GDI calls block, so run them off the caller's thread
		string documentName = options.JobName ?? DisplayStrings.DefaultJobName;
		string? outputFile = options.OutputFile is null ? null : Path.GetFullPath(options.OutputFile);

		// Checked for raw jobs too, which don't use a DEVMODE, so an unsupported key is never ignored
		byte[]? savedDevMode = WindowsVendorOptions.ReadDevMode(options.VendorOptions);

		// A PDF for a queue in front of an IPP printer may go straight to it (ADR 0027)
		if (content.Format == DocumentFormat.Pdf)
		{
			IPrintJob? ippJob = await this.TrySubmitOverIppAsync(content, options, cancellationToken).ConfigureAwait(false);

			if (ippJob is not null)
			{
				return ippJob;
			}
		}

		int jobNumber = content.Format switch
		{
			DocumentFormat.Raw => await this.SubmitRawAsync(content, options.Copies, documentName, outputFile, cancellationToken)
											.ConfigureAwait(false),
			DocumentFormat.Pdf => await this.SubmitPdfAsync(content, options, savedDevMode, documentName, outputFile, cancellationToken)
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
	public async Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		// DeviceCapabilities and DocumentProperties block, so run them off the caller's thread
		WindowsCapabilitiesRecord? record = await Task.Run(() => this.context.Spool.GetCapabilities(this.printerName), cancellationToken)
													  .ConfigureAwait(false);

		if (record is null)
		{
			string message = ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerName);

			throw new PrintException(message);
		}

		return WindowsCapabilitiesMapper.ToCapabilities(record);
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
		IppPrinter? printer = await this.FindIppPrinterAsync(options, cancellationToken).ConfigureAwait(false);

		if (printer is null)
		{
			return null;
		}

		Stream document = content.Content;
		long? documentStart = document.CanSeek ? document.Position : null;

		try
		{
			return await printer.SubmitAsync(content, options, cancellationToken).ConfigureAwait(false);
		}
		catch (PrintException exception) when (documentStart is not null && IppConnectionErrors.IsConnectFailure(exception))
		{
			// Nothing reached the printer, so the spooler can hold the job until it is back without printing it twice
			document.Position = documentStart.Value;

			return null;
		}
	}

	/// <summary>
	/// Finds the queue's IPP printer for a job, asking it once whether it accepts PDF.
	/// </summary>
	/// <param name="options">The job's options; some need the spooler.</param>
	/// <param name="cancellationToken">A token to cancel the question.</param>
	/// <returns>The printer, or <see langword="null"/> if this job goes through the spooler.</returns>
	private async Task<IppPrinter?> FindIppPrinterAsync(PrintOptions options, CancellationToken cancellationToken)
	{
		WindowsIppRoute? route = this.context.IppRoute;

		// Printing to a file, a Windows tray number and a driver's DEVMODE all need the spooler
		bool needsSpooler = options.OutputFile is not null
							|| options.InputTray is not null
							|| options.VendorOptions.Keys.Any(key => key.StartsWith(WindowsVendorOptions.Prefix, StringComparison.Ordinal));

		if (route is null || needsSpooler || this.ippPrinterRefusesPdf)
		{
			return null;
		}

		if (this.ippPrinter is not null)
		{
			return this.ippPrinter;
		}

		Uri? printerUri = route.FindPrinterUri(this.printerName, this.portName);

		if (printerUri is null)
		{
			return null;
		}

		IReadOnlyCollection<string>? formats;

		try
		{
			SharpIpp.Protocol.Models.PrinterDescriptionAttributes? attributes = await IppPrinterQueries
				.GetAttributesAsync(route.Context, printerUri, [IppRequestedAttributes.DocumentFormatSupported], cancellationToken)
				.ConfigureAwait(false);

			formats = attributes is null
				? null
				: IppValueReader.ReadReference(attributes.DocumentFormatSupported)?.Select(format => format.Value).ToList();
		}
		catch (PrintException)
		{
			// Unreachable, or wants a password: the spooler prints this job, and the next one asks again
			return null;
		}

		string pdf = route.Context.DocumentFormats.GetMimeType(DocumentFormat.Pdf);

		if (formats is null || !formats.Contains(pdf, StringComparer.OrdinalIgnoreCase))
		{
			// No printer at that URI, or one that doesn't take PDF: that won't change while this object lives
			this.ippPrinterRefusesPdf = true;

			return null;
		}

		this.ippPrinter = new IppPrinter(route.Context, printerUri, this.Info, formats);

		return this.ippPrinter;
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
	/// <param name="savedDevMode">A DEVMODE from <c>windows:devmode</c> to start from instead of the driver's
	/// defaults, or <see langword="null"/>.</param>
	/// <param name="documentName">The name shown in the print queue.</param>
	/// <param name="outputFile">A file to print to instead of the device, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">A token to abandon spooling.</param>
	/// <returns>The spooler's job ID.</returns>
	/// <exception cref="NotSupportedException">No PDF rasteriser is registered, or the driver doesn't list the
	/// chosen paper size or tray.</exception>
	private Task<int> SubmitPdfAsync
	(
		PrintContent      content,
		PrintOptions      options,
		byte[]?           savedDevMode,
		string            documentName,
		string?           outputFile,
		CancellationToken cancellationToken
	)
	{
		IPdfRasterizer rasterizer = this.context.PdfRasterizer
										?? throw new NotSupportedException(ExceptionMessages.PdfNeedsRasterizer);

		return Task.Run
		(
			() =>
			{
				// Before the document is opened, so a size or tray the driver lacks fails without rendering anything
				(short? paperId, short? binId) = this.FindPaperAndBin(options);

				using IRasterDocument document = rasterizer.Open(content.Content);

				GdiPrintRequest request = new GdiPrintRequest
				{
					PrinterName  = this.printerName,
					DocumentName = documentName,
					OutputFile   = outputFile,
					Document     = document,
					Options      = options,
					PaperId      = paperId,
					BinId        = binId,
					SavedDevMode = savedDevMode
				};

				return this.context.Gdi.PrintRasterDocument(request, cancellationToken);
			},
			cancellationToken
		);
	}

	/// <summary>
	/// Finds the driver's numbers for the chosen paper size and tray. The driver's lists are only read when one is
	/// chosen.
	/// </summary>
	/// <param name="options">The print options.</param>
	/// <returns>The DMPAPER_* and DMBIN_* numbers, each <see langword="null"/> when not chosen.</returns>
	/// <exception cref="NotSupportedException">The driver doesn't list the chosen size or tray.</exception>
	/// <exception cref="PrintException">The printer can no longer be found.</exception>
	private (short? PaperId, short? BinId) FindPaperAndBin(PrintOptions options)
	{
		if (options.MediaSize is null && options.InputTray is null)
		{
			return (null, null);
		}

		WindowsCapabilitiesRecord record = this.context.Spool.GetCapabilities(this.printerName)
			?? throw new PrintException(ExceptionMessages.FormatPrinterNoLongerAvailable(this.printerName));

		short? paperId = null;
		short? binId = null;

		if (options.MediaSize is not null)
		{
			paperId = WindowsCapabilitiesMapper.FindPaperId(record, options.MediaSize.Name)
				?? throw new NotSupportedException
				(
					ExceptionMessages.FormatMediaSizeNotListed(this.printerName, options.MediaSize.DisplayName, options.MediaSize.Name)
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

		return (paperId, binId);
	}
}
