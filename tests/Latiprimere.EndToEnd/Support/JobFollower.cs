namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Follows print jobs to the end through <c>WatchStatusAsync</c>, as applications do, keeping each change of state.
/// </summary>
public static class JobFollower
{
	/// <summary>
	/// The longest a job may take to finish.
	/// </summary>
	public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

	private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250); // How often the job is read

	/// <summary>
	/// Follows a job until it reaches a final state, and notes each state it passes through.
	/// </summary>
	/// <param name="context">The scenario, for its notes and cancellation token.</param>
	/// <param name="job">The job.</param>
	/// <returns>Each status whose state differs from the one before, ending with the final status.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="job"/> is
	/// <see langword="null"/>.</exception>
	/// <exception cref="TimeoutException">The job didn't finish within <see cref="Timeout"/>.</exception>
	public static Task<IReadOnlyList<PrintJobStatus>> FollowAsync(ScenarioContext context, IPrintJob job)
	{
		return JobFollower.FollowAsync(context, job, JobFollower.Timeout);
	}

	/// <summary>
	/// Follows a job until it reaches a final state, waiting as long as given: real printers can take minutes.
	/// </summary>
	/// <param name="context">The scenario, for its notes and cancellation token.</param>
	/// <param name="job">The job.</param>
	/// <param name="limit">The longest the job may take.</param>
	/// <returns>Each status whose state differs from the one before, ending with the final status.</returns>
	/// <exception cref="TimeoutException">The job didn't finish in time.</exception>
	public static async Task<IReadOnlyList<PrintJobStatus>> FollowAsync(ScenarioContext context, IPrintJob job, TimeSpan limit)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(job);

		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
		timeout.CancelAfter(limit);
		List<PrintJobStatus> changes = new List<PrintJobStatus>();

		try
		{
			await foreach (PrintJobStatus status in job.WatchStatusAsync(JobFollower.PollInterval, timeout.Token))
			{
				if (changes.Count == 0 || changes[^1].State != status.State)
				{
					changes.Add(status);
					context.Note($"Job {job.Id} on {job.PrinterId}: {status.State}" + (status.StateMessage is null ? string.Empty : $" ({status.StateMessage})"));
				}
			}
		}
		catch (OperationCanceledException exception) when (!context.CancellationToken.IsCancellationRequested)
		{
			string lastState = changes.Count > 0 ? changes[^1].State.ToString() : "unknown";

			throw new TimeoutException($"Job {job.Id} on {job.PrinterId} didn't finish within {limit.TotalMinutes} minutes; it was {lastState}.", exception);
		}

		return changes;
	}

	/// <summary>
	/// Follows a job to the end and returns only its final status.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="job">The job.</param>
	/// <returns>The final status.</returns>
	/// <exception cref="TimeoutException">The job didn't finish within <see cref="Timeout"/>.</exception>
	public static async Task<PrintJobStatus> WaitUntilFinishedAsync(ScenarioContext context, IPrintJob job)
	{
		IReadOnlyList<PrintJobStatus> changes = await JobFollower.FollowAsync(context, job);

		return changes.Count > 0 ? changes[^1] : throw new InvalidOperationException($"Job {job.Id} reported no status.");
	}
}
