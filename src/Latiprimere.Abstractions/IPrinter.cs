using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// A printer that can accept print jobs.
/// </summary>
/// <remarks>
/// Safe to use from several threads at once, for example to submit several jobs together.
/// </remarks>
public interface IPrinter
{
	/// <summary>
	/// Gets information about the printer, as read when the printer was looked up.
	/// </summary>
	/// <remarks>
	/// It isn't updated afterwards: to see the printer's current state, use <see cref="GetStatusAsync"/>.
	/// </remarks>
	PrinterInfo Info { get; }

	/// <summary>
	/// Submits a document to the printer.
	/// </summary>
	/// <param name="content">The document to print.</param>
	/// <param name="options">How to print the document. Use <see cref="PrintOptions.Default"/> for printer
	/// defaults.</param>
	/// <param name="cancellationToken">A token to cancel the submission. Cancelling does not cancel a job once it has
	/// been accepted.</param>
	/// <returns>The submitted job, which can be used to follow its progress.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="options"/> is <see
	/// langword="null"/>.</exception>
	/// <exception cref="NotSupportedException">The printer cannot accept documents in the content's format.</exception>
	/// <exception cref="PrintException">The print system rejected or failed to accept the job.</exception>
	Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Finds a job submitted to this printer earlier, by its <see cref="IPrintJob.Id"/>, so it can be followed or
	/// cancelled after the application has restarted.
	/// </summary>
	/// <param name="jobId">The job's identifier, as <see cref="IPrintJob.Id"/> gave it.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>
	/// The job, or <see langword="null"/> if the print system no longer knows it, or it was never submitted to this
	/// printer. Print systems forget finished jobs: printers and CUPS after a while, and Windows as soon as the job has
	/// printed, unless the printer is set to keep printed documents. So a job that isn't found has most likely
	/// finished, but whether it printed can't be told.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="jobId"/> is <see langword="null"/>.</exception>
	/// <exception cref="PrintException">The print system or printer couldn't be asked.</exception>
	Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads what the printer can do: paper sizes, duplex, colour, copies, orientation and paper sources.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The capabilities. Anything the print system doesn't report is left empty.</returns>
	/// <exception cref="PrintException">The print system or printer couldn't be asked.</exception>
	Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads the printer's current state from the print system.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The printer's current status.</returns>
	/// <exception cref="PrintException">The print system failed to report the printer's state, or no longer has the
	/// printer.</exception>
	/// <remarks>
	/// The default implementation gives the state in <see cref="Info"/>, as read when the printer was looked up.
	/// </remarks>
	Task<PrinterStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		PrinterStatus status = new PrinterStatus
		{
			PrinterId = this.Info.Id,
			State     = this.Info.State,
			Timestamp = DateTimeOffset.UtcNow
		};

		return Task.FromResult(status);
	}

	/// <summary>
	/// Follows the printer's state, reporting each change as it happens, until cancelled.
	/// </summary>
	/// <param name="pollInterval">How long to wait between reads of the state when the print system can't say when
	/// it changes, so it is polled.</param>
	/// <param name="cancellationToken">A token to stop following the printer.</param>
	/// <returns>
	/// The printer's status, then each change of its state, message, reasons or whether it accepts jobs, never twice
	/// the same. A printer has no final state, so this goes on until <paramref name="cancellationToken"/> is
	/// cancelled, which ends it with <see cref="OperationCanceledException"/>.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is zero or negative.</exception>
	/// <remarks>
	/// The Windows spooler signals each change to a printer, so its state is read when one comes, and at least every
	/// 30 seconds. CUPS and network printers are read every <paramref name="pollInterval"/>. A state that can't be
	/// read, as when the printer is off or the network drops, is reported as <see cref="PrinterState.Unknown"/>
	/// with why as its message, and the watch goes on, reporting the printer's state again once it can be read. The
	/// default implementation reads <see cref="GetStatusAsync"/> every <paramref name="pollInterval"/>.
	/// </remarks>
	IAsyncEnumerable<PrinterStatus> WatchStatusAsync(TimeSpan pollInterval, CancellationToken cancellationToken = default)
	{
		return StatusPolling.WatchPrinterAsync(this, pollInterval, cancellationToken);
	}

	/// <summary>
	/// Reads what the printer reports about itself beyond printing: its web pages, pictures of it, its firmware, and
	/// how it can be identified.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The details. Anything the print system doesn't report is left empty.</returns>
	/// <exception cref="PrintException">The print system or printer couldn't be asked.</exception>
	/// <remarks>
	/// CUPS queues and network printers report them over IPP. A Windows queue reports them only through its IPP
	/// printer, with <c>WindowsOptions.PrintPdfOverIpp</c> on; otherwise, and when that printer can't be reached, its
	/// details are empty. The default implementation gives details with the printer's ID and nothing else.
	/// </remarks>
	Task<PrinterDetails> GetDetailsAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new PrinterDetails { PrinterId = this.Info.Id });
	}

	/// <summary>
	/// Asks the printer to make itself known, so it can be told from others nearby: it flashes, beeps, shows a message
	/// or speaks, as it does by default.
	/// </summary>
	/// <param name="message">A message to show on the printer's display, if it has one, such as "This is Office
	/// Laser"; or <see langword="null"/> for none. At most 127 bytes in UTF-8.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>A task that completes once the printer has been asked.</returns>
	/// <exception cref="ArgumentException"><paramref name="message"/> is longer than 127 bytes in UTF-8.</exception>
	/// <exception cref="NotSupportedException">The printer, or its print system, can't be identified.</exception>
	/// <exception cref="PrintException">The print system or printer couldn't be asked.</exception>
	/// <remarks>
	/// <see cref="PrinterDetails.IdentifyActions"/> says how the printer can make itself known. CUPS queues and network
	/// printers are asked with IPP's Identify-Printer, which IPP Everywhere printers take. A Windows queue can be
	/// identified only through its IPP printer, with <c>WindowsOptions.PrintPdfOverIpp</c> on. The default
	/// implementation throws <see cref="NotSupportedException"/>.
	/// </remarks>
	Task IdentifyAsync(string? message = null, CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException(ExceptionMessages.FormatIdentifyNotSupported(this.Info.Id));
	}

	/// <summary>
	/// Prints a test page Latiprimere draws: the printer's name and address, what its capabilities say, a line a
	/// quarter of an inch in from each edge, two rulers, and colour and grey patches.
	/// </summary>
	/// <param name="options">How to print the page, as for <see cref="SubmitAsync"/>. Use <see cref="PrintOptions.Default"/>
	/// for printer defaults. The page goes on the job's paper, else the printer's default.</param>
	/// <param name="cancellationToken">A token to cancel the submission. Cancelling does not cancel the job once it has
	/// been accepted.</param>
	/// <returns>The page's job, which can be used to follow its progress.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
	/// <exception cref="NotSupportedException">The printer can't print the page, as for a PDF it can't print, such as
	/// a Windows queue without a PDF rasteriser.</exception>
	/// <exception cref="PrintException">The print system rejected or failed to accept the job, or the printer's
	/// capabilities couldn't be read.</exception>
	/// <remarks>
	/// <para>
	/// The page is a PDF, printed as any PDF is. A side of the edge line that doesn't print is outside what the printer
	/// can print, and rulers shorter than they say mean the page was scaled. On paper too small for it, such as a label,
	/// the page is shrunk to fit and says so.
	/// </para>
	/// <para>
	/// The default implementation throws <see cref="NotSupportedException"/>.
	/// </para>
	/// </remarks>
	Task<IPrintJob> PrintTestPageAsync(PrintOptions options, CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException(ExceptionMessages.FormatTestPageNotSupported(this.Info.Id));
	}

	/// <summary>
	/// Gets the settings of <see cref="PrintOptions"/> this printer applies to a job.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A printer from <see cref="IPrinterCatalog"/> refuses a job that sets anything else to other than its default,
	/// with <see cref="NotSupportedException"/>, before anything is sent, rather than printing as if it weren't set.
	/// That matters for settings a later version of Latiprimere adds, which a print system written for this one
	/// wouldn't know to apply. A setting the printer applies may still be refused for a job, as when the printer
	/// lacks the paper size asked for.
	/// </para>
	/// <para>
	/// The default implementation gives every setting this version of <see cref="PrintOptions"/> has. A print system
	/// that applies a setting added later says so by overriding this.
	/// </para>
	/// </remarks>
	PrintOptionKinds AppliedOptions
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
					| PrintOptionKinds.Finishings
					| PrintOptionKinds.OutputBin
					| PrintOptionKinds.MediaType;
		}
	}

	/// <summary>
	/// Gets what a printer of one print system offers beyond <see cref="IPrinter"/>, such as
	/// <c>Latiprimere.Windows.IWindowsPrinter</c>, or <see langword="null"/> if it offers none of that kind.
	/// </summary>
	/// <typeparam name="T">The interface asked for.</typeparam>
	/// <returns>The printer as <typeparamref name="T"/>, or <see langword="null"/>.</returns>
	/// <remarks>
	/// Use this rather than a cast: printers from <see cref="IPrinterCatalog"/> are Latiprimere's own, around the
	/// print system's, so a cast to a print system's interface fails where this finds it. The default implementation
	/// casts this printer.
	/// </remarks>
	T? GetFeature<T>()
		where T : class
	{
		return this as T;
	}
}
