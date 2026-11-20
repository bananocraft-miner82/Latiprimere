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
}
