namespace Latiprimere.Tests.Builders;

/// <summary>
/// Builds <see cref="PrintJobStatus"/> instances with sensible defaults for tests.
/// Each built status gets the current time, as a real print system would report.
/// </summary>
internal sealed class PrintJobStatusBuilder
{
	private string jobId = "1";                          // Job identifier
	private PrintJobState state = PrintJobState.Pending; // Job state
	private string? stateMessage = null;                 // Detail about the state
	private int? pagesCompleted = null;                  // Pages printed so far

	/// <summary>
	/// Sets the state of the status being built.
	/// </summary>
	/// <param name="state">The job state.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrintJobStatusBuilder WithState(PrintJobState state)
	{
		this.state = state;

		return this;
	}

	/// <summary>
	/// Sets the state message of the status being built.
	/// </summary>
	/// <param name="stateMessage">The state message.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrintJobStatusBuilder WithStateMessage(string stateMessage)
	{
		this.stateMessage = stateMessage;

		return this;
	}

	/// <summary>
	/// Sets the number of pages completed in the status being built.
	/// </summary>
	/// <param name="pagesCompleted">The number of pages printed so far.</param>
	/// <returns>This builder, for chaining.</returns>
	public PrintJobStatusBuilder WithPagesCompleted(int pagesCompleted)
	{
		this.pagesCompleted = pagesCompleted;

		return this;
	}

	/// <summary>
	/// Builds and returns the configured <see cref="PrintJobStatus"/>, timestamped now.
	/// </summary>
	/// <returns>The job status.</returns>
	public PrintJobStatus Build()
	{
		return new PrintJobStatus
		{
			JobId          = this.jobId,
			State          = this.state,
			StateMessage   = this.stateMessage,
			PagesCompleted = this.pagesCompleted,
			Timestamp      = DateTimeOffset.UtcNow
		};
	}
}
