using Latiprimere.Tests.Fakes;
using Xunit;

namespace Latiprimere.Tests;

/// <summary>
/// Following a printer: the default <see cref="IPrinter.GetStatusAsync"/> and <see cref="IPrinter.WatchStatusAsync"/>,
/// and <see cref="PrinterExtensions"/> (ADR 0049).
/// </summary>
public sealed class PrinterWatchTests
{
	[Fact]
	public async Task GivenAPrinterWithoutItsOwnStatusWhenReadThenTheDefaultGivesItsInfosState()
	{
		// Arrange — a printer that implements only what it must
		IPrinter printer = new FakePrinter(new PrinterInfo { Id = "fake:Office", Name = "Office", State = PrinterState.Processing });

		// Act
		PrinterStatus status = await printer.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(("fake:Office", PrinterState.Processing, null, null), (status.PrinterId, status.State, status.StateMessage, status.IsAcceptingJobs));
		Assert.Empty(status.StateReasons);
	}

	[Fact]
	public async Task GivenStatusesWhenWatchedByDefaultThenEachChangeIsReportedOnceUntilCancelled()
	{
		// Arrange — repeats, a change of reasons alone, of whether it accepts jobs, then nothing more
		IPrinter printer = new SequencePrinter
		(
			PrinterWatchTests.Status(PrinterState.Idle),
			PrinterWatchTests.Status(PrinterState.Idle),
			PrinterWatchTests.Status(PrinterState.Processing),
			PrinterWatchTests.Status(PrinterState.Processing) with { StateReasons = ["toner-low-warning"] },
			PrinterWatchTests.Status(PrinterState.Processing) with { StateReasons = ["toner-low-warning"] },
			PrinterWatchTests.Status(PrinterState.Stopped) with { StateMessage = "Out of paper", IsAcceptingJobs = false }
		);

		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		List<PrinterStatus> reported = [];

		// Act — a printer has no final state, so it goes on until cancelled
		Func<Task> watch = async () =>
		{
			await foreach (PrinterStatus status in printer.WatchStatusAsync(TimeSpan.FromMilliseconds(1), stop.Token))
			{
				reported.Add(status);

				if (reported.Count == 4)
				{
					await stop.CancelAsync();
				}
			}
		};

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(watch);
		Assert.Equal([PrinterState.Idle, PrinterState.Processing, PrinterState.Processing, PrinterState.Stopped], reported.Select(status => status.State));
		Assert.Equal(("Out of paper", false), (reported[3].StateMessage, reported[3].IsAcceptingJobs));
	}

	[Fact]
	public async Task GivenAReadThatFailsWhenWatchedByDefaultThenItIsReportedAsUnknownAndTheWatchGoesOn()
	{
		// Arrange — idle, then unreachable once, then idle again
		IPrinter printer = new SequencePrinter
		(
			PrinterWatchTests.Status(PrinterState.Idle),
			null,
			PrinterWatchTests.Status(PrinterState.Idle)
		);

		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		List<PrinterStatus> reported = [];

		// Act
		Func<Task> watch = async () =>
		{
			await foreach (PrinterStatus status in printer.WatchStatusAsync(TimeSpan.FromMilliseconds(1), stop.Token))
			{
				reported.Add(status);

				if (reported.Count == 3)
				{
					await stop.CancelAsync();
				}
			}
		};

		// Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(watch);
		Assert.Equal([PrinterState.Idle, PrinterState.Unknown, PrinterState.Idle], reported.Select(status => status.State));
		Assert.Equal(("fake:Office", SequencePrinter.FailureMessage), (reported[1].PrinterId, reported[1].StateMessage));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public async Task GivenAPollIntervalNotPositiveWhenWatchedThenThrowsArgumentOutOfRangeExceptionWithoutReading(int milliseconds)
	{
		// Arrange (provided by InlineData above)
		SequencePrinter printer = new SequencePrinter(PrinterWatchTests.Status(PrinterState.Idle));

		// Act
		Func<Task> watch = async () =>
		{
			await foreach (PrinterStatus status in ((IPrinter)printer).WatchStatusAsync(TimeSpan.FromMilliseconds(milliseconds), TestContext.Current.CancellationToken))
			{
			}
		};

		// Assert
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(watch);
		Assert.Equal(0, printer.Reads);
	}

	[Fact]
	public async Task GivenTheExtensionWhenWatchedThenItReportsTheStatusAtTheDefaultInterval()
	{
		// Arrange
		IPrinter printer = new SequencePrinter(PrinterWatchTests.Status(PrinterState.Idle));
		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		// Act
		IAsyncEnumerator<PrinterStatus> watch = printer.WatchStatusAsync(stop.Token).GetAsyncEnumerator(stop.Token);
		bool moved = await watch.MoveNextAsync();
		PrinterStatus first = watch.Current;
		await stop.CancelAsync();
		await watch.DisposeAsync();

		// Assert
		Assert.True(moved);
		Assert.Equal(PrinterState.Idle, first.State);
		Assert.Equal(TimeSpan.FromSeconds(5), PrinterExtensions.DefaultPollInterval);
	}

	[Fact]
	public void GivenNoPrinterWhenTheExtensionIsCalledThenThrowsArgumentNullException()
	{
		// Arrange
		IPrinter? printer = null;

		// Act
		Action watch = () => printer!.WatchStatusAsync(TestContext.Current.CancellationToken); // Deliberately null

		// Assert
		Assert.Throws<ArgumentNullException>(watch);
	}

	/// <summary>
	/// Makes a status.
	/// </summary>
	/// <param name="state">Its state.</param>
	/// <returns>The status.</returns>
	private static PrinterStatus Status(PrinterState state)
	{
		return new PrinterStatus { PrinterId = "fake:Office", State = state, IsAcceptingJobs = true, Timestamp = DateTimeOffset.UtcNow };
	}
}
