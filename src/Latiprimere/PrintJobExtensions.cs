using System.ComponentModel;

namespace Latiprimere;

/// <summary>
/// Provides helper methods for following print jobs.
/// </summary>
public static class PrintJobExtensions
{
	/// <summary>
	/// The interval between status checks used when none is specified, for print systems that can't say when a job's
	/// status changes.
	/// </summary>
	public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Follows a job until it reaches a final state, as <see cref="IPrintJob.WatchStatusAsync"/> does, checking its
	/// status every <see cref="DefaultPollInterval"/> when the print system can't say when it changes.
	/// </summary>
	/// <param name="job">The job to follow.</param>
	/// <returns>
	/// Each distinct status the job reports, ending with its final status (completed, canceled or aborted).
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="job"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Overloads are used rather than optional parameters, so that more can be added later without breaking compiled
	/// callers (RS0026 and RS0027 of the public API analyzer).
	/// </remarks>
	public static IAsyncEnumerable<PrintJobStatus> WatchStatusAsync(this IPrintJob job)
	{
		ArgumentNullException.ThrowIfNull(job);

		return job.WatchStatusAsync(PrintJobExtensions.DefaultPollInterval, CancellationToken.None);
	}

	/// <summary>
	/// Follows a job until it reaches a final state, as <see cref="IPrintJob.WatchStatusAsync"/> does, checking its
	/// status every <see cref="DefaultPollInterval"/> when the print system can't say when it changes.
	/// </summary>
	/// <param name="job">The job to follow.</param>
	/// <param name="cancellationToken">A token to stop following the job. This does not cancel the job itself.</param>
	/// <returns>
	/// Each distinct status the job reports, ending with its final status (completed, canceled or aborted).
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="job"/> is <see langword="null"/>.</exception>
	public static IAsyncEnumerable<PrintJobStatus> WatchStatusAsync(this IPrintJob job, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(job);

		return job.WatchStatusAsync(PrintJobExtensions.DefaultPollInterval, cancellationToken);
	}

	/// <summary>
	/// Follows a job until it reaches a final state. Kept so code compiled against earlier versions still runs: code
	/// compiled now calls <see cref="IPrintJob.WatchStatusAsync"/> for the same arguments.
	/// </summary>
	/// <param name="job">The job to follow.</param>
	/// <param name="pollInterval">How long to wait between status checks when the print system can't say when the
	/// status changes.</param>
	/// <param name="cancellationToken">A token to stop following the job. This does not cancel the job itself.</param>
	/// <returns>Each distinct status the job reports, ending with its final status.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="job"/> is <see langword="null"/>.</exception>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static IAsyncEnumerable<PrintJobStatus> WatchStatusAsync(this IPrintJob job, TimeSpan pollInterval, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(job);

		return job.WatchStatusAsync(pollInterval, cancellationToken);
	}
}
