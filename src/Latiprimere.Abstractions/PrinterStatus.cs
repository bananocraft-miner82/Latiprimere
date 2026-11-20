namespace Latiprimere;

/// <summary>
/// Describes a printer's state at a point in time.
/// </summary>
public sealed record PrinterStatus
{
	/// <summary>
	/// Gets the printer's identifier, as <see cref="PrinterInfo.Id"/> gives it.
	/// </summary>
	public required string PrinterId { get; init; }

	/// <summary>
	/// Gets the printer's state.
	/// </summary>
	public required PrinterState State { get; init; }

	/// <summary>
	/// Gets the printer's own message about its state, such as "Out of paper", or <see langword="null"/> when it gives
	/// none.
	/// </summary>
	public string? StateMessage { get; init; }

	/// <summary>
	/// Gets why the printer is in its state, as IPP <c>printer-state-reasons</c> keywords, such as
	/// <c>media-empty-error</c>, <c>toner-low-warning</c> or <c>paused</c>; or none, when the print system gives no
	/// reason.
	/// </summary>
	/// <remarks>
	/// Reported by printers reached over IPP and by CUPS, in the order they give them, without <c>none</c>. For a
	/// Windows printer, Latiprimere gives the keywords that match the spooler's status flags, such as
	/// <c>media-empty-error</c> for "paper out" and <c>paused</c> for a paused queue. The keywords are defined by
	/// RFC 8011, section 5.4.12, and the PWG; printers may add their own, and add <c>-error</c>, <c>-warning</c> or
	/// <c>-report</c> to say how serious each is.
	/// </remarks>
	public IReadOnlyList<string> StateReasons { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Gets a value indicating whether the printer is taking new jobs, or <see langword="null"/> when the print system
	/// doesn't say. A CUPS queue can be stopped yet accept jobs, which wait until it starts again, or refuse them.
	/// </summary>
	public bool? IsAcceptingJobs { get; init; }

	/// <summary>
	/// Gets the time at which this status was read.
	/// </summary>
	public required DateTimeOffset Timestamp { get; init; }
}
