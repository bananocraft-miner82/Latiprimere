namespace Latiprimere.Testing.Fixtures;

/// <summary>
/// Waits for print jobs on virtual printers to finish.
/// </summary>
public static class PrintJobWaiter
{
	/// <summary>
	/// The longest a job may take. Jobs on the virtual printers finish in a few seconds, but a queue in front of a busy
	/// printer waits between retries, and CI runners can be slow.
	/// </summary>
	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250); // How often the job is checked

	/// <summary>
	/// Follows a job until it reaches a final state.
	/// </summary>
	/// <param name="job">The job to follow.</param>
	/// <param name="cancellationToken">The test's cancellation token.</param>
	/// <returns>The job's final status.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="job"/> is <see langword="null"/>.</exception>
	/// <exception cref="TimeoutException">The job did not finish within <see cref="Timeout"/>.</exception>
	public static async Task<PrintJobStatus> WaitUntilFinishedAsync(IPrintJob job, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(job);

		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(PrintJobWaiter.Timeout);
		PrintJobStatus? lastStatus = null;

		try
		{
			await foreach (PrintJobStatus status in job.WatchStatusAsync(PrintJobWaiter.PollInterval, timeout.Token).ConfigureAwait(false))
			{
				lastStatus = status;
			}
		}
		catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
		{
			string lastState = lastStatus?.State.ToString() ?? "unknown";

			throw new TimeoutException
			(
				$"Job {job.Id} on {job.PrinterId} did not finish within {PrintJobWaiter.Timeout.TotalSeconds} s; it was {lastState}.",
				exception
			);
		}

		// The watch reports the first status it reads, so there is always one
		return lastStatus ?? throw new InvalidOperationException($"Job {job.Id} reported no status.");
	}
}
