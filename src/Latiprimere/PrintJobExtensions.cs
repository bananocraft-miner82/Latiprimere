using System.Runtime.CompilerServices;

namespace Latiprimere;

/// <summary>
/// Provides helper methods for following print jobs.
/// </summary>
public static class PrintJobExtensions
{
	/// <summary>
	/// The interval between status checks used when none is specified.
	/// </summary>
	public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// Follows a job until it reaches a final state, checking its status every
	/// <see cref="DefaultPollInterval"/>.
	/// </summary>
	/// <param name="job">The job to follow.</param>
	/// <returns>
	/// Each distinct status the job reports, ending with its final status (completed, cancelled or aborted).
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="job"/> is <see langword="null"/>.</exception>
	/// <remarks>
	/// Overloads are used rather than optional parameters, so that more can be added later without breaking compiled
	/// callers (RS0026 and RS0027 of the public API analyzer).
	/// </remarks>
	public static IAsyncEnumerable<PrintJobStatus> WatchStatusAsync(this IPrintJob job)
	{
		// Delegate to the main overload with the default interval and no cancellation
		return job.WatchStatusAsync(PrintJobExtensions.DefaultPollInterval, CancellationToken.None);
	}

	/// <summary>
	/// Follows a job until it reaches a final state, checking its status every
	/// <see cref="DefaultPollInterval"/>.
	/// </summary>
	/// <param name="job">The job to follow.</param>
	/// <param name="cancellationToken">A token to stop following the job. This does not cancel the job itself.</param>
	/// <returns>
	/// Each distinct status the job reports, ending with its final status (completed, cancelled or aborted).
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="job"/> is <see langword="null"/>.</exception>
	public static IAsyncEnumerable<PrintJobStatus> WatchStatusAsync(this IPrintJob job, CancellationToken cancellationToken)
	{
		// Delegate to the main overload with the default interval
		return job.WatchStatusAsync(PrintJobExtensions.DefaultPollInterval, cancellationToken);
	}

	/// <summary>
	/// Follows a job until it reaches a final state, checking its status at the given interval.
	/// </summary>
	/// <param name="job">The job to follow.</param>
	/// <param name="pollInterval">How long to wait between status checks.</param>
	/// <param name="cancellationToken">A token to stop following the job. This does not cancel the job itself.</param>
	/// <returns>
	/// Each distinct status the job reports, ending with its final status (completed, cancelled or aborted).
	/// A status is reported again only when its state, message or page count changes.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="job"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is zero or negative, which would
	/// ask the print system for the status again and again without pause.</exception>
	/// <remarks>
	/// Arguments are validated when enumeration starts. A job whose state the print system doesn't report is
	/// <see cref="PrintJobState.Unknown"/>, which isn't final, so following it continues until
	/// <paramref name="cancellationToken"/> is cancelled: pass one with a timeout when that matters.
	/// </remarks>
	public static async IAsyncEnumerable<PrintJobStatus> WatchStatusAsync
	(
		this IPrintJob                             job,
		TimeSpan                                   pollInterval,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		// Validate the arguments
		ArgumentNullException.ThrowIfNull(job);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

		// Poll until the job reaches a final state, reporting only changes
		PrintJobStatus? previousStatus = null;
		bool isFinished = false;

		while (!isFinished)
		{
			PrintJobStatus currentStatus = await job.GetStatusAsync(cancellationToken).ConfigureAwait(false);

			if (PrintJobExtensions.HasChanged(previousStatus, currentStatus))
			{
				yield return currentStatus;
			}

			previousStatus = currentStatus;
			isFinished = currentStatus.IsTerminal;

			// Wait before the next check, unless the job has finished
			if (!isFinished)
			{
				await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Determines whether a job's status has changed in a way worth reporting.
	/// </summary>
	/// <param name="previousStatus">The previously reported status, or <see langword="null"/> if none.</param>
	/// <param name="currentStatus">The status just read.</param>
	/// <returns><see langword="true"/> if the state, message or page count differs; otherwise <see
	/// langword="false"/>.</returns>
	private static bool HasChanged(PrintJobStatus? previousStatus, PrintJobStatus currentStatus)
	{
		// The timestamp always changes, so compare only the meaningful fields
		return previousStatus is null
			|| previousStatus.State != currentStatus.State
			|| previousStatus.StateMessage != currentStatus.StateMessage
			|| previousStatus.PagesCompleted != currentStatus.PagesCompleted;
	}
}
