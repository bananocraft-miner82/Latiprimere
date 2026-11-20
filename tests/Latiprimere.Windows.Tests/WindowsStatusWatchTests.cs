using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using Xunit;

namespace Latiprimere.Windows.Tests;

/// <summary>
/// Windows printers' and jobs' status, followed by the spooler's change notifications or, without them, by polling
/// (ADR 0049).
/// </summary>
public sealed class WindowsStatusWatchTests
{
	[Theory]
	[InlineData((int)PrinterStatusFlags.None, false, (int)PrinterState.Idle, new string[0], true)]
	[InlineData((int)(PrinterStatusFlags.Paused | PrinterStatusFlags.PaperOut), false, (int)PrinterState.Stopped, new[] { "paused", "media-empty-error" }, true)]
	[InlineData((int)(PrinterStatusFlags.Printing | PrinterStatusFlags.TonerLow), false, (int)PrinterState.Processing, new[] { "toner-low-warning" }, true)]
	[InlineData((int)PrinterStatusFlags.None, true, (int)PrinterState.Stopped, new[] { "offline-report" }, true)]
	[InlineData((int)(PrinterStatusFlags.Offline | PrinterStatusFlags.NotAvailable), false, (int)PrinterState.Stopped, new[] { "offline-report" }, true)]
	[InlineData((int)PrinterStatusFlags.PendingDeletion, false, (int)PrinterState.Stopped, new[] { "deactivated" }, false)]
	public async Task GivenStatusFlagsWhenStatusIsReadThenStateReasonsAndAcceptingFollow(int flags, bool offline, int state, string[] reasons, bool accepting)
	{
		// Arrange — working offline counts as offline; a reason two flags share is given once (provided by
		// InlineData above)
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsStatusWatchTests.CreatePrinter(spool, (PrinterStatusFlags)flags, offline);

		// Act
		PrinterStatus status = await printer.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(("windows:Office", (PrinterState)state, accepting, null), (status.PrinterId, status.State, status.IsAcceptingJobs, status.StateMessage));
		Assert.Equal(reasons, status.StateReasons);
	}

	[Fact]
	public async Task GivenAPrinterThatIsGoneWhenStatusIsReadThenThrowsPrintException()
	{
		// Arrange
		WindowsPrinter printer = new WindowsPrinter(new WindowsPrintContextBuilder().WithSpool(new FakeWinSpool()).Build(), "Gone", null, null, new PrinterInfo { Id = "windows:Gone", Name = "Gone" });

		// Act
		Func<Task> read = () => printer.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(read);
	}

	[Fact]
	public async Task GivenChangeNotificationsWhenAPrinterIsWatchedThenEachSignalledChangeIsReportedOnceAndTheWatchWaitsForTheNext()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool { SignalsChanges = true };
		WindowsPrinter printer = WindowsStatusWatchTests.CreatePrinter(spool, PrinterStatusFlags.None, false);
		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		IAsyncEnumerator<PrinterStatus> watch = printer.WatchStatusAsync(TimeSpan.FromHours(1), stop.Token).GetAsyncEnumerator(stop.Token);

		// Act — idle; a signal with nothing changed; paused, then signalled
		Assert.True(await watch.MoveNextAsync());
		PrinterStatus idle = watch.Current;
		FakeChangeNotification notification = Assert.Single(spool.ChangeNotifications);
		Task<bool> next = watch.MoveNextAsync().AsTask();

		await notification.WaitUntilWaitingAsync(stop.Token);
		notification.Signal();
		await notification.WaitUntilWaitingAsync(stop.Token);
		spool.SetPrinter(new WindowsPrinterRecord { Name = "Office", Status = PrinterStatusFlags.Paused });
		notification.Signal();

		// Assert — the poll interval of an hour is never waited; the unchanged signal reports nothing
		Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(10), stop.Token));
		Assert.Equal(PrinterState.Idle, idle.State);
		Assert.Equal((PrinterState.Stopped, "paused"), (watch.Current.State, Assert.Single(watch.Current.StateReasons)));
		Assert.Equal((WindowsChanges.Printer, WindowsStatusWatch.SafetyInterval), (notification.Changes, notification.LastTimeout));

		// Stopping the watch closes the notification
		await stop.CancelAsync();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await watch.MoveNextAsync());
		await watch.DisposeAsync();
		Assert.True(notification.IsDisposed);
	}

	[Fact]
	public async Task GivenANotificationThatFailsWhenAPrinterIsWatchedThenItIsClosedAndThePrinterPolled()
	{
		// Arrange — a notification that can't be reset would signal for ever
		FakeWinSpool spool = new FakeWinSpool { SignalsChanges = true };
		WindowsPrinter printer = WindowsStatusWatchTests.CreatePrinter(spool, PrinterStatusFlags.None, false);
		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		IAsyncEnumerator<PrinterStatus> watch = printer.WatchStatusAsync(TimeSpan.FromMilliseconds(20), stop.Token).GetAsyncEnumerator(stop.Token);

		// Act — it fails, then the printer changes without any signal
		Assert.True(await watch.MoveNextAsync());
		FakeChangeNotification notification = Assert.Single(spool.ChangeNotifications);
		Task<bool> next = watch.MoveNextAsync().AsTask();
		await notification.WaitUntilWaitingAsync(stop.Token);
		notification.Fail();
		spool.SetPrinter(new WindowsPrinterRecord { Name = "Office", Status = PrinterStatusFlags.PaperOut });

		// Assert — seen by polling, waiting on the notification only the once
		Assert.True(await next.WaitAsync(TimeSpan.FromSeconds(10), stop.Token));
		Assert.Equal("media-empty-error", Assert.Single(watch.Current.StateReasons));
		Assert.True(notification.IsDisposed);
		Assert.Equal(1, notification.Waits);
		await stop.CancelAsync();
		await watch.DisposeAsync();
	}

	[Fact]
	public async Task GivenAPrinterThatGoesWhenWatchedThenItIsReportedAsUnknownAndTheWatchGoesOn()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsStatusWatchTests.CreatePrinter(spool, PrinterStatusFlags.None, false);
		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		IAsyncEnumerator<PrinterStatus> watch = printer.WatchStatusAsync(TimeSpan.FromMilliseconds(20), stop.Token).GetAsyncEnumerator(stop.Token);

		// Act — removed, then back
		Assert.True(await watch.MoveNextAsync());
		spool.RemovePrinter("Office");
		Assert.True(await watch.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), stop.Token));
		PrinterStatus gone = watch.Current;
		spool.AddPrinter(new WindowsPrinterRecord { Name = "Office" });
		Assert.True(await watch.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), stop.Token));

		// Assert
		Assert.Equal(PrinterState.Unknown, gone.State);
		Assert.Contains("Office", gone.StateMessage, StringComparison.Ordinal);
		Assert.Equal(PrinterState.Idle, watch.Current.State);
		await stop.CancelAsync();
		await watch.DisposeAsync();
	}

	[Fact]
	public async Task GivenNoChangeNotificationsWhenAPrinterIsWatchedThenItIsPolledAtTheInterval()
	{
		// Arrange — a spooler that can't signal
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsStatusWatchTests.CreatePrinter(spool, PrinterStatusFlags.None, false);
		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		IAsyncEnumerator<PrinterStatus> watch = printer.WatchStatusAsync(TimeSpan.FromMilliseconds(20), stop.Token).GetAsyncEnumerator(stop.Token);

		// Act
		Assert.True(await watch.MoveNextAsync());
		spool.SetPrinter(new WindowsPrinterRecord { Name = "Office", Status = PrinterStatusFlags.PaperJam });
		bool moved = await watch.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), stop.Token);

		// Assert
		Assert.True(moved);
		Assert.Equal("media-jam-error", Assert.Single(watch.Current.StateReasons));
		Assert.Empty(spool.ChangeNotifications);
		await stop.CancelAsync();
		await watch.DisposeAsync();
	}

	[Fact]
	public async Task GivenChangeNotificationsWhenAJobIsWatchedThenItEndsWhenTheJobLeavesTheQueue()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool { SignalsChanges = true };
		spool.SetJob(7, new WindowsJobRecord { JobId = 7, Status = JobStatusFlags.Printing });
		IPrintJob job = new WindowsPrintJob(spool, "Office", "windows:Office", 7, TimeProvider.System);
		List<PrintJobStatus> statuses = [];

		// Act — signalled once with nothing changed, then when the job has gone
		Task watching = Task.Run
		(
			async () =>
			{
				await foreach (PrintJobStatus status in job.WatchStatusAsync(TimeSpan.FromHours(1), TestContext.Current.CancellationToken))
				{
					statuses.Add(status);
				}
			},
			TestContext.Current.CancellationToken
		);

		FakeChangeNotification notification = await WindowsStatusWatchTests.WaitForNotificationAsync(spool);
		await notification.WaitUntilWaitingAsync(TestContext.Current.CancellationToken);
		notification.Signal();
		await notification.WaitUntilWaitingAsync(TestContext.Current.CancellationToken);
		spool.SetJob(7, null);
		notification.Signal();
		await watching.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

		// Assert — printing, then finished, each once; notified of the queue's jobs, and closed at the end
		Assert.Equal(WindowsChanges.Jobs, notification.Changes);
		Assert.Equal(2, statuses.Count);
		Assert.Equal(PrintJobState.Processing, statuses[0].State);
		Assert.True(statuses[1].IsTerminal);
		Assert.True(notification.IsDisposed);
	}

	/// <summary>
	/// Waits for the watch, running on another thread, to open its notification.
	/// </summary>
	/// <param name="spool">The spooler.</param>
	/// <returns>The notification.</returns>
	private static async Task<FakeChangeNotification> WaitForNotificationAsync(FakeWinSpool spool)
	{
		for (int attempt = 0; attempt < 500; attempt++)
		{
			lock (spool.ChangeNotifications)
			{
				if (spool.ChangeNotifications.Count > 0)
				{
					return spool.ChangeNotifications[0];
				}
			}

			await Task.Delay(10, TestContext.Current.CancellationToken);
		}

		throw new TimeoutException("The watch didn't open a change notification.");
	}

	/// <summary>
	/// Creates the queue "Office" with a status.
	/// </summary>
	/// <param name="spool">The spooler.</param>
	/// <param name="flags">Its status flags.</param>
	/// <param name="offline">Whether it is set to work offline.</param>
	/// <returns>The printer.</returns>
	private static WindowsPrinter CreatePrinter(FakeWinSpool spool, PrinterStatusFlags flags, bool offline)
	{
		spool.AddPrinter(new WindowsPrinterRecord { Name = "Office", Status = flags, IsWorkingOffline = offline });

		return new WindowsPrinter(new WindowsPrintContextBuilder().WithSpool(spool).Build(), "Office", null, null, new PrinterInfo { Id = "windows:Office", Name = "Office" });
	}
}
