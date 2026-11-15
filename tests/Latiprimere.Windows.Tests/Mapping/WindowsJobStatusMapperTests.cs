using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Mapping;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Mapping;

public sealed class WindowsJobStatusMapperTests
{
	[Theory]
	[InlineData(0x0000u, PrintJobState.Pending)]           // no flags
	[InlineData(0x0008u, PrintJobState.Processing)]        // spooling
	[InlineData(0x0010u, PrintJobState.Processing)]        // printing
	[InlineData(0x4000u, PrintJobState.Processing)]        // rendering locally
	[InlineData(0x0001u, PrintJobState.Held)]              // paused
	[InlineData(0x0002u, PrintJobState.Stopped)]           // error
	[InlineData(0x0020u, PrintJobState.Stopped)]           // offline
	[InlineData(0x0040u, PrintJobState.Stopped)]           // paper out
	[InlineData(0x0400u, PrintJobState.Stopped)]           // user intervention
	[InlineData(0x0080u, PrintJobState.Completed)]         // printed
	[InlineData(0x1000u, PrintJobState.Completed)]         // complete (sent to printer)
	[InlineData(0x2080u, PrintJobState.Completed)]         // printed and retained
	[InlineData(0x2000u, PrintJobState.Completed)]         // retained alone
	[InlineData(0x0004u, PrintJobState.Cancelled)]         // deleting
	[InlineData(0x0100u, PrintJobState.Cancelled)]         // deleted
	[InlineData(0x0014u, PrintJobState.Cancelled)]         // deleting while printing
	[InlineData(0x0011u, PrintJobState.Held)]              // paused while printing
	public void GivenSpoolerStatusFlagsWhenToPrintJobStateThenReturnsMostDecisiveState
	(
		uint statusFlags,
		PrintJobState expectedState
	)
	{
		// Arrange — JOB_STATUS_* flag combinations and the expected state (provided by InlineData above)
		JobStatusFlags status = (JobStatusFlags)statusFlags;

		// Act
		PrintJobState state = WindowsJobStatusMapper.ToPrintJobState(status);

		// Assert
		Assert.Equal(expectedState, state);
	}

	[Fact]
	public void GivenListedJobWhenToPrintJobStatusThenMapsMessageAndPages()
	{
		// Arrange
		WindowsJobRecord job = new WindowsJobRecord
		{
			JobId        = 12,
			Status       = JobStatusFlags.Printing,
			StatusText   = "Printing page 2",
			PagesPrinted = 1
		};

		DateTimeOffset timestamp = new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

		// Act
		PrintJobStatus status = WindowsJobStatusMapper.ToPrintJobStatus(job, "12", false, timestamp);

		// Assert
		Assert.Equal("12", status.JobId);
		Assert.Equal(PrintJobState.Processing, status.State);
		Assert.Equal("Printing page 2", status.StateMessage);
		Assert.Equal(1, status.PagesCompleted);
		Assert.Equal(timestamp, status.Timestamp);
	}

	[Fact]
	public void GivenJobThatLeftQueueWhenToPrintJobStatusThenReportsCompletedWithExplanation()
	{
		// Arrange
		WindowsJobRecord? job = null;

		// Act
		PrintJobStatus status = WindowsJobStatusMapper.ToPrintJobStatus(job, "12", false, DateTimeOffset.UnixEpoch);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.False(string.IsNullOrWhiteSpace(status.StateMessage));
	}

	[Fact]
	public void GivenCancelledJobThatLeftQueueWhenToPrintJobStatusThenReportsCancelled()
	{
		// Arrange
		WindowsJobRecord? job = null;

		// Act
		PrintJobStatus status = WindowsJobStatusMapper.ToPrintJobStatus(job, "12", true, DateTimeOffset.UnixEpoch);

		// Assert
		Assert.Equal(PrintJobState.Cancelled, status.State);
		Assert.Null(status.StateMessage);
	}

	[Fact]
	public void GivenBlankStatusTextWhenToPrintJobStatusThenStateMessageIsNull()
	{
		// Arrange
		WindowsJobRecord job = new WindowsJobRecord { JobId = 12, StatusText = "  " };

		// Act
		PrintJobStatus status = WindowsJobStatusMapper.ToPrintJobStatus(job, "12", false, DateTimeOffset.UnixEpoch);

		// Assert
		Assert.Null(status.StateMessage);
	}
}
