using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrintJobStatusTests
{
	[Theory]
	[InlineData(PrintJobState.Completed)]
	[InlineData(PrintJobState.Cancelled)]
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
