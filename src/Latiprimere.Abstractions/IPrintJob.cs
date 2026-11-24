namespace Latiprimere;

/// <summary>
/// A job that has been submitted to a printer.
/// </summary>
/// <remarks>
/// Safe to use from several threads at once, for example to read its status on one while cancelling it on another.
/// </remarks>
public interface IPrintJob
{
	/// <summary>
	/// Gets the identifier of the job, as assigned by the print system. Unique only within its printer.
	/// </summary>
	string Id { get; }

	/// <summary>
	/// Gets the identifier of the printer the job was submitted to.
	/// </summary>
	string PrinterId { get; }

	/// <summary>
	/// Reads the job's current status from the print system.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The job's current status.</returns>
	/// <exception cref="PrintException">The print system failed to report the job's status.</exception>
	Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Asks the print system to cancel the job.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>
	/// <see langword="true"/> if the print system accepted the cancellation; the job then reports
	/// <see cref="PrintJobState.Canceled"/> once it has stopped. <see langword="false"/> if the job had already
	/// finished, which can happen between reading its status and asking to cancel it.
	/// </returns>
	/// <exception cref="NotSupportedException">The print system can't cancel jobs.</exception>
	/// <exception cref="PrintException">The print system refused or failed to cancel the job.</exception>
	Task<bool> CancelAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Follows the job until it reaches a final state, reporting each change of its status as it happens.
	/// </summary>
	/// <param name="pollInterval">How long to wait between reads of the status when the print system can't say when
	/// it changes, so it is polled (see the remarks).</param>
	/// <param name="cancellationToken">A token to stop following the job. This doesn't cancel the job itself.</param>
	/// <returns>
	/// Each distinct status the job reports, ending with its final status (completed, canceled or aborted). A status
	/// is reported again only when its state, message, reasons or page count change, never twice the same.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is zero or negative, which would
	/// ask the print system for the status again and again without pause.</exception>
	/// <exception cref="PrintException">The print system failed to report the job's status.</exception>
	/// <remarks>
	/// <para>
	/// The Windows spooler signals each change to its queue, so a job there is read when one comes, rather than every
	/// <paramref name="pollInterval"/>, and at least every 30 seconds in case a change goes unsignalled. CUPS and
	/// network printers are read every <paramref name="pollInterval"/>.
	/// </para>
	/// <para>
	/// Arguments are checked when enumeration starts. A job whose state the print system doesn't report is
	/// <see cref="PrintJobState.Unknown"/>, which isn't final, so following it goes on until
	/// <paramref name="cancellationToken"/> is cancelled: pass one with a timeout when that matters. The default
	/// implementation reads <see cref="GetStatusAsync"/> every <paramref name="pollInterval"/>.
	/// </para>
	/// </remarks>
	IAsyncEnumerable<PrintJobStatus> WatchStatusAsync(TimeSpan pollInterval, CancellationToken cancellationToken = default)
	{
		return StatusPolling.WatchJobAsync(this, pollInterval, cancellationToken);
	}

	/// <summary>
	/// Gets what a job of one print system offers beyond <see cref="IPrintJob"/>, or <see langword="null"/> if it
	/// offers none of that kind.
	/// </summary>
	/// <typeparam name="T">The interface asked for.</typeparam>
	/// <returns>The job as <typeparamref name="T"/>, or <see langword="null"/>.</returns>
	/// <remarks>
	/// Use this rather than a cast: jobs from printers the <see cref="IPrinterCatalog"/> gives are Latiprimere's own,
	/// around the print system's. The default implementation casts this job.
	/// </remarks>
	T? GetFeature<T>()
		where T : class
	{
		return this as T;
	}
}
