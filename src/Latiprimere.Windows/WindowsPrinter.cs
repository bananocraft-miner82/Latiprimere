using Latiprimere.Rendering;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Mapping;
using Latiprimere.Windows.Resources;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows;

/// <summary>
/// A printer managed by the Windows spooler.
/// </summary>
/// <remarks>
/// Raw data is sent to the printer unchanged, so <see cref="PrintOptions"/> settings such as copies and duplex are
/// not applied to it: the data itself controls the output. PDF documents are rasterised (which needs an
/// <see cref="IPdfRasterizer"/>, such as the Latiprimere.Rendering.Pdfium package) and drawn through GDI, with the
/// options applied through the printer driver. Raw data is refused for drivers known to drop it silently (see
/// <see cref="DriverQuirks"/>).
/// </remarks>
internal sealed class WindowsPrinter : IPrinter
{
	private readonly WindowsPrintContext context; // Spooler, GDI and rasteriser
	private readonly string printerName;          // The spooler's name for the printer
	private readonly string? driverName;          // The printer's driver, if the spooler reported it

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsPrinter"/> class.
	/// </summary>
	/// <param name="context">The spooler, GDI and rasteriser.</param>
	/// <param name="printerName">The spooler's name for the printer.</param>
	/// <param name="driverName">The printer's driver name, or <see langword="null"/> if not known.</param>
	/// <param name="info">Information about the printer.</param>
	public WindowsPrinter(WindowsPrintContext context, string printerName, string? driverName, PrinterInfo info)
	{
		this.context = context;
		this.printerName = printerName;
		this.driverName = driverName;
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

		int jobNumber = content.Format switch
		{
			DocumentFormat.Raw => await this.SubmitRawAsync(content, options.Copies, documentName, outputFile, cancellationToken)
											.ConfigureAwait(false),
			DocumentFormat.Pdf => await this.SubmitPdfAsync(content, options, documentName, outputFile, cancellationToken)
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
	/// <param name="documentName">The name shown in the print queue.</param>
	/// <param name="outputFile">A file to print to instead of the device, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">A token to abandon spooling.</param>
	/// <returns>The spooler's job ID.</returns>
	/// <exception cref="NotSupportedException">No PDF rasteriser is registered.</exception>
	private Task<int> SubmitPdfAsync
	(
		PrintContent      content,
		PrintOptions      options,
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
				using IRasterDocument document = rasterizer.Open(content.Content);

				GdiPrintRequest request = new GdiPrintRequest
				{
					PrinterName  = this.printerName,
					DocumentName = documentName,
					OutputFile   = outputFile,
					Document     = document,
					Options      = options
				};

				return this.context.Gdi.PrintRasterDocument(request, cancellationToken);
			},
			cancellationToken
		);
	}
}
