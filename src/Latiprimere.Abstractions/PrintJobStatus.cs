namespace Latiprimere;

/// <summary>
/// Describes the status of a print job at a point in time.
/// </summary>
public sealed record PrintJobStatus
{
	/// <summary>
	/// Gets the identifier of the job, as assigned by the print system.
	/// </summary>
	public required string JobId { get; init; }

	/// <summary>
	/// Gets the job's state.
	/// </summary>
	public required PrintJobState State { get; init; }

	/// <summary>
	/// Gets additional detail about the state from the print system, if any.
	/// </summary>
	public string? StateMessage { get; init; }

	/// <summary>
	/// Gets the number of pages printed so far, if the print system reports it.
	/// </summary>
	public int? PagesCompleted { get; init; }

	/// <summary>
	/// Gets the time at which this status was read.
	/// </summary>
	public required DateTimeOffset Timestamp { get; init; }

	/// <summary>
	/// Gets a value indicating whether the job has reached a final state and will not change again.
	/// </summary>
	public bool IsTerminal
	{
		get
		{
			// Completed, cancelled and aborted jobs never change state again
			return this.State is PrintJobState.Completed or PrintJobState.Cancelled or PrintJobState.Aborted;
		}
	}
}
