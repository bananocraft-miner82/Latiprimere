namespace Latiprimere.EndToEnd;

/// <summary>
/// Ends a scenario that found it can't run here; the runner reports it as skipped, with the reason recorded by
/// <see cref="ScenarioContext.Skip"/>.
/// </summary>
public sealed class ScenarioSkippedException : Exception
{
	/// <summary>
	/// Initialises a new instance of the <see cref="ScenarioSkippedException"/> class.
	/// </summary>
	public ScenarioSkippedException()
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="ScenarioSkippedException"/> class.
	/// </summary>
	/// <param name="reason">Why the scenario can't run here.</param>
	public ScenarioSkippedException(string reason)
		: base(reason)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="ScenarioSkippedException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The exception that caused this one.</param>
	public ScenarioSkippedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
