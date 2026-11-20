using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrintJobStatusTests
{
	[Fact]
	public void GivenStatusWithoutReasonsWhenStateReasonsReadThenIsEmpty()
	{
		// Arrange — as print systems that give no reasons, such as the Windows spooler, create it
		PrintJobStatus status = PrintJobStatusTests.CreateStatus(PrintJobState.Processing);

		// Act
		IReadOnlyList<string> reasons = status.StateReasons;

		// Assert
		Assert.NotNull(reasons);
		Assert.Empty(reasons);
	}

	[Theory]
	[InlineData(PrintJobState.Completed)]
	[InlineData(PrintJobState.Canceled)]
	[InlineData(PrintJobState.Aborted)]
	public void GivenFinalStateWhenIsTerminalThenReturnsTrue(PrintJobState state)
	{
		// Arrange — state is one of the final job states (provided by InlineData above)
		PrintJobStatus status = PrintJobStatusTests.CreateStatus(state);

		// Act
		bool isTerminal = status.IsTerminal;

		// Assert
		Assert.True(isTerminal);
	}

	[Theory]
	[InlineData(PrintJobState.Unknown)]
	[InlineData(PrintJobState.Pending)]
	[InlineData(PrintJobState.Held)]
	[InlineData(PrintJobState.Processing)]
	[InlineData(PrintJobState.Stopped)]
	public void GivenNonFinalStateWhenIsTerminalThenReturnsFalse(PrintJobState state)
	{
		// Arrange — state is one a job can still move on from (provided by InlineData above)
		PrintJobStatus status = PrintJobStatusTests.CreateStatus(state);

		// Act
		bool isTerminal = status.IsTerminal;

		// Assert
		Assert.False(isTerminal);
	}

	/// <summary>
	/// Creates a status with the given state and fixed values for everything else.
	/// </summary>
	/// <param name="state">The job state.</param>
	/// <returns>The status.</returns>
	private static PrintJobStatus CreateStatus(PrintJobState state)
	{
		return new PrintJobStatus
		{
			JobId     = "1",
			State     = state,
			Timestamp = DateTimeOffset.UnixEpoch
		};
	}
}
