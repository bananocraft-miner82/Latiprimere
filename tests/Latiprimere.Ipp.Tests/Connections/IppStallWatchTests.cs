using Latiprimere.Ipp.Connections;
using Xunit;

namespace Latiprimere.Ipp.Tests.Connections;

public sealed class IppStallWatchTests
{
	private static readonly TimeSpan ShortStall = TimeSpan.FromMilliseconds(50); // Runs out quickly

	[Fact]
	public async Task GivenWriteNotEndedWhenTimeoutRunsOutThenHasStalled()
	{
		// Arrange
		using IppStallWatch watch = new IppStallWatch(IppStallWatchTests.ShortStall, TestContext.Current.CancellationToken);

		// Act
		watch.StartWrite();
		await IppStallWatchTests.WaitForCancellationAsync(watch.Token);

		// Assert
		Assert.True(watch.HasStalled);
		Assert.False(watch.IsAwaitingResponse);
	}

	[Fact]
	public async Task GivenWriteEndedInTimeWhenTimeoutWouldHaveRunOutThenNotCancelled()
	{
		// Arrange
		using IppStallWatch watch = new IppStallWatch(IppStallWatchTests.ShortStall, TestContext.Current.CancellationToken);

		// Act — a write that finishes at once, then a pause well past the timeout, as while a page renders
		watch.StartWrite();
		watch.EndWrite();
		await Task.Delay(IppStallWatchTests.ShortStall * 6, TestContext.Current.CancellationToken);

		// Assert
		Assert.False(watch.Token.IsCancellationRequested);
		Assert.False(watch.HasStalled);
	}

	[Fact]
	public async Task GivenBodyEndedWhenNoAnswerComesThenHasStalledAwaitingResponse()
	{
		// Arrange
		using IppStallWatch watch = new IppStallWatch(IppStallWatchTests.ShortStall, TestContext.Current.CancellationToken);

		// Act
		watch.EndBody();
		await IppStallWatchTests.WaitForCancellationAsync(watch.Token);

		// Assert
		Assert.True(watch.HasStalled);
		Assert.True(watch.IsAwaitingResponse);
	}

	[Fact]
	public void GivenCallerCancelsWhenHasStalledReadThenFalse()
	{
		// Arrange
		using CancellationTokenSource caller = new CancellationTokenSource();
		using IppStallWatch watch = new IppStallWatch(TimeSpan.FromHours(1), caller.Token);

		// Act
		caller.Cancel();

		// Assert — the request is cancelled, but by the caller
		Assert.True(watch.Token.IsCancellationRequested);
		Assert.False(watch.HasStalled);
	}

	[Fact]
	public void GivenDisposedWatchWhenWriteEndsThenDoesNothing()
	{
		// Arrange — a write still finishing after the request ended, as when a printer answers early
		IppStallWatch watch = new IppStallWatch(IppStallWatchTests.ShortStall, TestContext.Current.CancellationToken);
		watch.StartWrite();
		watch.Dispose();

		// Act
		Action endWrite = () =>
		{
			watch.EndWrite();
			watch.EndBody();
			watch.Dispose();
		};

		// Assert
		Exception? exception = Record.Exception(endWrite);
		Assert.Null(exception);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void GivenTimeoutNotPositiveWhenValidatedThenThrowsArgumentOutOfRangeException(int milliseconds)
	{
		// Arrange — zero, or infinite (provided by InlineData above)
		TimeSpan stallTimeout = TimeSpan.FromMilliseconds(milliseconds);

		// Act
		Action validate = () => IppStallWatch.ValidateStallTimeout(stallTimeout);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(validate);
	}

	[Fact]
	public void GivenTimeoutLongerThanTimersAllowWhenValidatedThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		TimeSpan stallTimeout = TimeSpan.FromDays(30);

		// Act
		Action validate = () => IppStallWatch.ValidateStallTimeout(stallTimeout);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(validate);
	}

	/// <summary>
	/// Waits until a token is cancelled, or 30 seconds, so a missed stall fails the test rather than hanging it.
	/// </summary>
	/// <param name="token">The token.</param>
	/// <returns>A task that completes when the token is cancelled or the wait gives up.</returns>
	private static async Task WaitForCancellationAsync(CancellationToken token)
	{
		using CancellationTokenSource wait = CancellationTokenSource.CreateLinkedTokenSource(token, TestContext.Current.CancellationToken);

		try
		{
			await Task.Delay(TimeSpan.FromSeconds(30), wait.Token);
		}
		catch (OperationCanceledException)
		{
			// The token was cancelled, as expected
		}
	}
}
