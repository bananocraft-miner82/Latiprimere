using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Resources;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows.Mapping;

/// <summary>
/// Maps Windows spooler job information to <see cref="PrintJobStatus"/>.
/// </summary>
internal static class WindowsJobStatusMapper
{
	/// <summary>Flags meaning the job is being or has been deleted.</summary>
	private static readonly JobStatusFlags CancelledFlags = JobStatusFlags.Deleting | JobStatusFlags.Deleted;

	/// <summary>Flags meaning the spooler has finished sending the job to the printer.</summary>
	private static readonly JobStatusFlags CompletedFlags = JobStatusFlags.Printed | JobStatusFlags.Complete;

	/// <summary>Flags meaning the job is stuck until something is fixed.</summary>
	private static readonly JobStatusFlags StoppedFlags = JobStatusFlags.Error
															| JobStatusFlags.Offline
															| JobStatusFlags.PaperOut
															| JobStatusFlags.BlockedDeviceQueue
															| JobStatusFlags.UserIntervention;

	/// <summary>Flags meaning the job is being spooled, rendered or printed.</summary>
	private static readonly JobStatusFlags ProcessingFlags = JobStatusFlags.Printing
																| JobStatusFlags.Spooling
																| JobStatusFlags.RenderingLocally
																| JobStatusFlags.Restart;

	/// <summary>
	/// Creates a job status from the spooler's job information.
	/// </summary>
	/// <param name="job">The job, or <see langword="null"/> if the spooler no longer lists it.</param>
	/// <param name="jobId">The Latiprimere job identifier.</param>
	/// <param name="isCancelRequested">Whether Latiprimere asked for the job to be cancelled.</param>
	/// <param name="timestamp">The time the status was read.</param>
	/// <returns>The job status.</returns>
	/// <remarks>
	/// Windows removes finished jobs from the queue without recording how they finished. A job that has left the
	/// queue is reported as cancelled if Latiprimere cancelled it, and otherwise as completed.
	/// </remarks>
	internal static PrintJobStatus ToPrintJobStatus(WindowsJobRecord? job, string jobId, bool isCancelRequested, DateTimeOffset timestamp)
	{
		if (job is null)
		{
			return new PrintJobStatus
			{
				JobId        = jobId,
				State        = isCancelRequested ? PrintJobState.Canceled : PrintJobState.Completed,
				StateMessage = isCancelRequested ? null : DisplayStrings.JobLeftQueue,
				Timestamp    = timestamp
			};
		}

		return new PrintJobStatus
		{
			JobId          = jobId,
			State          = WindowsJobStatusMapper.ToPrintJobState(job.Status),
			StateMessage   = string.IsNullOrWhiteSpace(job.StatusText) ? null : job.StatusText,
			PagesCompleted = job.PagesPrinted,
			Timestamp      = timestamp
		};
	}

	/// <summary>
	/// Maps spooler job status flags to a job state. When several flags are set, the most decisive wins.
	/// </summary>
	/// <param name="status">The JOB_STATUS_* flags.</param>
	/// <returns>The job state.</returns>
	internal static PrintJobState ToPrintJobState(JobStatusFlags status)
	{
		return status switch
		{
			_ when (status & WindowsJobStatusMapper.CancelledFlags) != 0  => PrintJobState.Canceled,
			_ when (status & WindowsJobStatusMapper.CompletedFlags) != 0  => PrintJobState.Completed,
			_ when (status & JobStatusFlags.Paused) != 0                  => PrintJobState.Held,
			_ when (status & WindowsJobStatusMapper.StoppedFlags) != 0    => PrintJobState.Stopped,
			_ when (status & WindowsJobStatusMapper.ProcessingFlags) != 0 => PrintJobState.Processing,
			_ when (status & JobStatusFlags.Retained) != 0                => PrintJobState.Completed,
			_                                                             => PrintJobState.Pending,
		};
	}
}
