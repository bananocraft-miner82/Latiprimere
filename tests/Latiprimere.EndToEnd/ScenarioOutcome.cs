namespace Latiprimere.EndToEnd;

/// <summary>
/// How a scenario ended.
/// </summary>
public enum ScenarioOutcome
{
	/// <summary>
	/// Every check passed.
	/// </summary>
	Passed = 0,

	/// <summary>
	/// A check failed, the scenario threw, timed out, or made no checks.
	/// </summary>
	Failed = 1,

	/// <summary>
	/// Not run, because something it needs isn't available here.
	/// </summary>
	Skipped = 2
}
