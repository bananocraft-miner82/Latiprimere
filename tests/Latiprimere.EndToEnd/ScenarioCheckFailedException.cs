namespace Latiprimere.EndToEnd;

/// <summary>
/// Ends a scenario whose required check failed. The check itself is already recorded, so the runner reports the
/// scenario as failed without an error of its own.
/// </summary>
public sealed class ScenarioCheckFailedException : Exception
{
	/// <summary>
	/// Initialises a new instance of the <see cref="ScenarioCheckFailedException"/> class.
	/// </summary>
	public ScenarioCheckFailedException()
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="ScenarioCheckFailedException"/> class.
	/// </summary>
	/// <param name="checkName">The check that failed.</param>
	public ScenarioCheckFailedException(string checkName)
		: base($"Required check failed: {checkName}")
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="ScenarioCheckFailedException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The exception that caused this one.</param>
	public ScenarioCheckFailedException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}
