namespace Latiprimere;

/// <summary>
/// Enumerates the states a printer can report.
/// </summary>
public enum PrinterState
{
	/// <summary>
	/// The print system did not report a state, or reported one Latiprimere does not recognise.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// The printer is ready and has no jobs in progress.
	/// </summary>
	Idle = 1,

	/// <summary>
	/// The printer is processing one or more jobs.
	/// </summary>
	Processing = 2,

	/// <summary>
	/// The printer is stopped and needs attention, for example because it is paused, offline or out of paper.
	/// </summary>
	Stopped = 3,
}
