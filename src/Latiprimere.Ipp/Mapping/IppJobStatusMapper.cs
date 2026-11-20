using Latiprimere.Ipp.Resources;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Maps IPP job attributes to <see cref="PrintJobStatus"/>.
/// </summary>
internal static class IppJobStatusMapper
{
	/// <summary>
	/// Creates a job status from the attributes returned by Get-Job-Attributes.
	/// </summary>
	/// <param name="attributes">The job's IPP attributes, or <see langword="null"/> if none were returned.</param>
	/// <param name="jobId">The Latiprimere job identifier.</param>
	/// <param name="timestamp">The time the status was read.</param>
	/// <returns>The job status.</returns>
	/// <remarks>
	/// The printer's own <c>job-state-message</c> is kept when it gives one. Otherwise the message says what the most
	/// telling of its <c>job-state-reasons</c> means, as CUPS, which cancels a job its printer refuses, gives no
	/// message for it.
	/// </remarks>
	internal static PrintJobStatus ToPrintJobStatus(JobDescriptionAttributes? attributes, string jobId, DateTimeOffset timestamp)
	{
		IReadOnlyList<string> reasons = IppJobStateReasons.Read(attributes);
		string? message = IppValueReader.ReadString(attributes?.JobStateMessage);

		return new PrintJobStatus
		{
			JobId          = jobId,
			State          = IppJobStatusMapper.ToPrintJobState(IppValueReader.ReadValue(attributes?.JobState)),
			StateMessage   = string.IsNullOrWhiteSpace(message) ? IppJobStateReasons.Explain(reasons) : message,
			StateReasons   = reasons,
			PagesCompleted = IppValueReader.ReadValue(attributes?.JobImpressionsCompleted),
			Timestamp      = timestamp
		};
	}

	/// <summary>
	/// Creates the status of a job the printer no longer lists.
	/// </summary>
	/// <param name="jobId">The Latiprimere job identifier.</param>
	/// <param name="isCancelRequested">Whether Latiprimere cancelled the job.</param>
	/// <param name="timestamp">The time the status was read.</param>
	/// <returns>The job status.</returns>
	/// <remarks>
	/// Printers with little or no job history forget a job soon after it finishes, and then answer not-found for
	/// it. As with the Windows spooler, such a job is reported as cancelled if Latiprimere cancelled it, and
	/// otherwise as completed.
	/// </remarks>
	internal static PrintJobStatus ToNoLongerListedStatus(string jobId, bool isCancelRequested, DateTimeOffset timestamp)
	{
		return new PrintJobStatus
		{
			JobId        = jobId,
			State        = isCancelRequested ? PrintJobState.Canceled : PrintJobState.Completed,
			StateMessage = isCancelRequested ? null : DisplayStrings.JobNoLongerListed,
			Timestamp    = timestamp
		};
	}

	/// <summary>
	/// Creates the status of a job whose printer or queue no longer exists either.
	/// </summary>
	/// <param name="jobId">The Latiprimere job identifier.</param>
	/// <param name="isCancelRequested">Whether Latiprimere cancelled the job.</param>
	/// <param name="timestamp">The time the status was read.</param>
	/// <returns>The job status: cancelled if Latiprimere cancelled it, otherwise aborted.</returns>
	/// <remarks>
	/// Deleting a CUPS queue cancels its jobs, so reporting the job completed would be wrong. Aborted is final, so
	/// following the job ends; its message says that how the job ended isn't known.
	/// </remarks>
	internal static PrintJobStatus ToPrinterGoneStatus(string jobId, bool isCancelRequested, DateTimeOffset timestamp)
	{
		return new PrintJobStatus
		{
			JobId        = jobId,
			State        = isCancelRequested ? PrintJobState.Canceled : PrintJobState.Aborted,
			StateMessage = isCancelRequested ? null : DisplayStrings.JobPrinterGone,
			Timestamp    = timestamp
		};
	}

	/// <summary>
	/// Maps an IPP <c>job-state</c> value.
	/// </summary>
	/// <param name="state">The IPP job state, or <see langword="null"/> if not reported.</param>
	/// <returns>The Latiprimere job state.</returns>
	internal static PrintJobState ToPrintJobState(JobState? state)
	{
		return state switch
		{
			JobState.Pending           => PrintJobState.Pending,
			JobState.PendingHeld       => PrintJobState.Held,
			JobState.Processing        => PrintJobState.Processing,
			JobState.ProcessingStopped => PrintJobState.Stopped,
			JobState.Canceled          => PrintJobState.Canceled,
			JobState.Aborted           => PrintJobState.Aborted,
			JobState.Completed         => PrintJobState.Completed,
			_                          => PrintJobState.Unknown,
		};
	}
}
