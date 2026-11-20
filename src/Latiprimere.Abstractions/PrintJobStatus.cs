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
	/// <remarks>
	/// The printer's or server's own message when it gives one. For a printer reached over IPP or a CUPS queue that
	/// gives none, it says what <see cref="StateReasons"/> means, when Latiprimere knows the reason, such as a job
	/// cancelled at the printer, or one it can't print with the options chosen.
	/// </remarks>
	public string? StateMessage { get; init; }

	/// <summary>
	/// Gets why the job is in its state, as IPP <c>job-state-reasons</c> keywords, such as
	/// <c>job-canceled-at-device</c>, <c>aborted-by-system</c> or <c>printer-stopped</c>; or none, when the print system
	/// gives no reason.
	/// </summary>
	/// <remarks>
	/// Reported by printers reached over IPP and by CUPS, in the order they give them, without <c>none</c>. The Windows
	/// spooler gives none, except for jobs it sends to a queue's IPP printer. The keywords are defined by RFC 8011,
	/// section 5.3.8, and the PWG; printers may add their own.
	/// </remarks>
	public IReadOnlyList<string> StateReasons { get; init; } = Array.Empty<string>();

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
			return this.State is PrintJobState.Completed or PrintJobState.Canceled or PrintJobState.Aborted;
		}
	}
}
