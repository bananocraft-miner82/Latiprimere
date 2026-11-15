namespace Latiprimere.Windows.Interop;

/// <summary>
/// The JOB_STATUS_* flags the spooler reports in <c>JOB_INFO_1.Status</c>.
/// </summary>
[Flags]
internal enum JobStatusFlags : uint
{
	/// <summary>No flags: the job is queued.</summary>
	None = 0,

	/// <summary>JOB_STATUS_PAUSED: the job is paused.</summary>
	Paused = 1 << 0,

	/// <summary>JOB_STATUS_ERROR: the job is in an error state.</summary>
	Error = 1 << 1,

	/// <summary>JOB_STATUS_DELETING: the job is being deleted.</summary>
	Deleting = 1 << 2,

	/// <summary>JOB_STATUS_SPOOLING: the job is being spooled.</summary>
	Spooling = 1 << 3,

	/// <summary>JOB_STATUS_PRINTING: the job is printing.</summary>
	Printing = 1 << 4,

	/// <summary>JOB_STATUS_OFFLINE: the printer is offline.</summary>
	Offline = 1 << 5,

	/// <summary>JOB_STATUS_PAPEROUT: the printer is out of paper.</summary>
	PaperOut = 1 << 6,

	/// <summary>JOB_STATUS_PRINTED: the job has printed.</summary>
	Printed = 1 << 7,

	/// <summary>JOB_STATUS_DELETED: the job has been deleted.</summary>
	Deleted = 1 << 8,

	/// <summary>JOB_STATUS_BLOCKED_DEVQ: the driver cannot print the job.</summary>
	BlockedDeviceQueue = 1 << 9,

	/// <summary>JOB_STATUS_USER_INTERVENTION: the printer needs attention.</summary>
	UserIntervention = 1 << 10,

	/// <summary>JOB_STATUS_RESTART: the job has been restarted.</summary>
	Restart = 1 << 11,

	/// <summary>JOB_STATUS_COMPLETE: the job has been sent to the printer.</summary>
	Complete = 1 << 12,

	/// <summary>JOB_STATUS_RETAINED: the job is kept in the queue after printing.</summary>
	Retained = 1 << 13,

	/// <summary>JOB_STATUS_RENDERING_LOCALLY: the job is being rendered on the client.</summary>
	RenderingLocally = 1 << 14,
}
