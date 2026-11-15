using Latiprimere.Tests.Builders;
using Latiprimere.Tests.Fakes;
using Xunit;

namespace Latiprimere.Tests;

public sealed class PrintJobExtensionsTests
{
	[Fact]
	public async Task GivenJobAlreadyFinishedWhenWatchStatusWithNoArgumentsThenYieldsItsFinalStatus()
	{
		// Arrange — a finished job needs no waiting, so the default interval doesn't slow the test
		FakePrintJob job = new FakePrintJob(new PrintJobStatusBuilder().WithState(PrintJobState.Completed).Build());

		// Act — xUnit1051 asks for the test's token, but this test is about the overload that takes none
#pragma warning disable xUnit1051
		List<PrintJobStatus> statuses = await PrintJobExtensionsTests.CollectAsync(job.WatchStatusAsync());
#pragma warning restore xUnit1051

		// Assert
		PrintJobStatus status = Assert.Single(statuses);
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenJobThatCompletesWhenWatchStatusThenYieldsEachChangeEndingWithCompleted()
	{
		// Arrange
		FakePrintJob job = new FakePrintJob
		(
			new PrintJobStatusBuilder().WithState(PrintJobState.Pending).Build(),
			new PrintJobStatusBuilder().WithState(PrintJobState.Pending).Build(),
			new PrintJobStatusBuilder().WithState(PrintJobState.Processing).Build(),
			new PrintJobStatusBuilder().WithState(PrintJobState.Processing).WithPagesCompleted(1).Build(),
			new PrintJobStatusBuilder().WithState(PrintJobState.Completed).WithPagesCompleted(1).Build()
		);

		// Act
		List<PrintJobStatus> statuses = await PrintJobExtensionsTests.CollectAsync
		(
			job.WatchStatusAsync(TimeSpan.FromMilliseconds(1), TestContext.Current.CancellationToken)
		);

		// Assert
		List<PrintJobState> states = statuses
			.Select(status => status.State)
			.ToList();

		Assert.Equal
		(
			new[]
			{
				PrintJobState.Pending,
				PrintJobState.Processing,
				PrintJobState.Processing,
				PrintJobState.Completed,
			},
			states
		);

		Assert.Equal(1, statuses[2].PagesCompleted);
	}

	[Fact]
	public async Task GivenStateMessageChangeWhenWatchStatusThenYieldsTheChange()
	{
		// Arrange
		FakePrintJob job = new FakePrintJob
		(
			new PrintJobStatusBuilder().WithState(PrintJobState.Stopped).WithStateMessage("Out of paper").Build(),
			new PrintJobStatusBuilder().WithState(PrintJobState.Stopped).WithStateMessage("Door open").Build(),
			new PrintJobStatusBuilder().WithState(PrintJobState.Aborted).Build()
		);

		// Act
		List<PrintJobStatus> statuses = await PrintJobExtensionsTests.CollectAsync
		(
			job.WatchStatusAsync(TimeSpan.FromMilliseconds(1), TestContext.Current.CancellationToken)
		);

		// Assert
		Assert.Equal(3, statuses.Count);
		Assert.Equal("Door open", statuses[1].StateMessage);
	}

	[Fact]
	public async Task GivenAlreadyCompletedJobWhenWatchStatusThenYieldsOnceAndStopsPolling()
	{
		// Arrange
		FakePrintJob job = new FakePrintJob(new PrintJobStatusBuilder().WithState(PrintJobState.Completed).Build());

		// Act
		List<PrintJobStatus> statuses = await PrintJobExtensionsTests.CollectAsync
		(
			job.WatchStatusAsync(TestContext.Current.CancellationToken)
		);

		// Assert
		Assert.Single(statuses);
		Assert.Equal(1, job.StatusRequestCount);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(0)]
	public async Task GivenPollIntervalNotPositiveWhenWatchStatusThenThrowsArgumentOutOfRangeException(int milliseconds)
	{
		// Arrange — milliseconds is negative or zero (provided by InlineData above); zero would poll without pause
		FakePrintJob job = new FakePrintJob(new PrintJobStatusBuilder().Build());
		TimeSpan pollInterval = TimeSpan.FromMilliseconds(milliseconds);

		// Act
		Func<Task> watchStatus = () => PrintJobExtensionsTests.CollectAsync
		(
			job.WatchStatusAsync(pollInterval, TestContext.Current.CancellationToken)
		);

		// Assert
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(watchStatus);
	}

	[Fact]
	public async Task GivenNullJobWhenWatchStatusThenThrowsArgumentNullException()
	{
		// Arrange
		IPrintJob? job = null;

		// Act
		Func<Task> watchStatus = () => PrintJobExtensionsTests.CollectAsync
		(
			job!.WatchStatusAsync(TimeSpan.FromMilliseconds(1), TestContext.Current.CancellationToken)
		); // Deliberately null

		// Assert
		await Assert.ThrowsAsync<ArgumentNullException>(watchStatus);
	}

	[Fact]
	public async Task GivenCancellationWhileJobIsProcessingWhenWatchStatusThenThrowsOperationCanceledException()
	{
		// Arrange
		FakePrintJob job = new FakePrintJob(new PrintJobStatusBuilder().WithState(PrintJobState.Processing).Build());

		using CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource
		(
			TestContext.Current.CancellationToken
		);

		TimeSpan pollInterval = TimeSpan.FromMilliseconds(10);

		// Act
		Func<Task> watchStatus = async () =>
		{
			await foreach (PrintJobStatus status in job.WatchStatusAsync(pollInterval, cancellationSource.Token))
			{
				// Stop watching as soon as the first status arrives
				await cancellationSource.CancelAsync();
			}
		};

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(watchStatus);
		Assert.Equal(1, job.StatusRequestCount);
	}

	[Fact]
	public async Task GivenPrinterStopsAnsweringWhileWatchingWhenWatchStatusThenYieldsWhatItHadThenThrowsTheFailure()
	{
		// Arrange — two statuses, then the printer can't be reached
		PrintException failure = new PrintException("Could not communicate with the printer.");

		FakePrintJob job = new FakePrintJob
		(
			new PrintJobStatusBuilder().WithState(PrintJobState.Pending).Build(),
			new PrintJobStatusBuilder().WithState(PrintJobState.Processing).Build()
		)
		{
			FailureAfterScript = failure
		};

		List<PrintJobState> states = new List<PrintJobState>();

		// Act
		Func<Task> watchStatus = async () =>
		{
			await foreach (PrintJobStatus status in job.WatchStatusAsync(TimeSpan.FromMilliseconds(1), TestContext.Current.CancellationToken))
			{
				states.Add(status.State);
			}
		};

		// Assert — the failure ends the watch rather than being retried or swallowed, so the caller decides
		PrintException exception = await Assert.ThrowsAsync<PrintException>(watchStatus);
		Assert.Same(failure, exception);
		Assert.Equal(new[] { PrintJobState.Pending, PrintJobState.Processing }, states);
	}

	/// <summary>
	/// Reads every status from the sequence into a list.
	/// </summary>
	/// <param name="statuses">The status sequence.</param>
	/// <returns>The statuses, in order.</returns>
	private static async Task<List<PrintJobStatus>> CollectAsync(IAsyncEnumerable<PrintJobStatus> statuses)
	{
		List<PrintJobStatus> collectedStatuses = new List<PrintJobStatus>();

		await foreach (PrintJobStatus status in statuses)
		{
			collectedStatuses.Add(status);
		}

		return collectedStatuses;
	}
}
