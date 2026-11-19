using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Fakes;
using Xunit;

namespace Latiprimere.Windows.Tests;

public sealed class WindowsPrintJobTests
{
	[Fact]
	public async Task GivenListedJobWhenGetStatusThenMapsSpoolerStatus()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetJob(5, new WindowsJobRecord { JobId = 5, Status = JobStatusFlags.Printing });
		WindowsPrintJob job = WindowsPrintJobTests.CreateJob(spool, 5);

		// Act
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("5", status.JobId);
		Assert.Equal(PrintJobState.Processing, status.State);
	}

	[Fact]
	public async Task GivenJobThatLeftQueueWhenGetStatusThenReportsCompleted()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrintJob job = WindowsPrintJobTests.CreateJob(spool, 5);

		// Act
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.True(status.IsTerminal);
	}

	[Fact]
	public async Task GivenListedJobWhenCancelThenDeletesItAndLaterReportsCancelled()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetJob(5, new WindowsJobRecord { JobId = 5, Status = JobStatusFlags.Paused });
		WindowsPrintJob job = WindowsPrintJobTests.CreateJob(spool, 5);

		// Act
		bool isCancelled = await job.CancelAsync(TestContext.Current.CancellationToken);
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.True(isCancelled);
		Assert.Equal(new[] { 5 }, spool.DeletedJobIds);
		Assert.Equal(PrintJobState.Canceled, status.State);
	}

	[Fact]
	public async Task GivenJobThatLeftQueueWhenCancelThenReturnsFalseAndStillReportsCompleted()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrintJob job = WindowsPrintJobTests.CreateJob(spool, 5);

		// Act
		bool isCancelled = await job.CancelAsync(TestContext.Current.CancellationToken);
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert — the job finished before the cancellation, so it isn't reported as cancelled
		Assert.False(isCancelled);
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	/// <summary>
	/// Creates a job on the fake spooler's Office printer.
	/// </summary>
	/// <param name="spool">The fake spooler.</param>
	/// <param name="jobNumber">The job ID.</param>
	/// <returns>The job.</returns>
	private static WindowsPrintJob CreateJob(FakeWinSpool spool, int jobNumber)
	{
		return new WindowsPrintJob(spool, "Office", "windows:Office", jobNumber, TimeProvider.System);
	}
}
