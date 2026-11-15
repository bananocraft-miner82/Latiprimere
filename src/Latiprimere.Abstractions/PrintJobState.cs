namespace Latiprimere;

/// <summary>
/// Enumerates the states a print job can be in. Modelled on the IPP job states.
/// </summary>
public enum PrintJobState
{
	/// <summary>
	/// The print system did not report a state, or reported one Latiprimere does not recognise.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// The job is queued and waiting to be processed.
	/// </summary>
	Pending = 1,

	/// <summary>
	/// The job is held and will not be processed until it is released.
	/// </summary>
	Held = 2,

	/// <summary>
	/// The job is being processed or printed.
	/// </summary>
	Processing = 3,

	/// <summary>
	/// The job has stopped while processing, for example because the printer needs attention.
	/// </summary>
	Stopped = 4,

	/// <summary>
	/// The job was cancelled. This state is final.
	/// </summary>
	Cancelled = 5,

	/// <summary>
	/// The job was aborted by the print system because of an error. This state is final.
	/// </summary>
	Aborted = 6,

	/// <summary>
	/// The job finished successfully. This state is final.
	/// </summary>
	Completed = 7,
}
