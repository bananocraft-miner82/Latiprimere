namespace Latiprimere.EndToEnd;

/// <summary>
/// One end-to-end check: what it is, what it needs, and the code that runs it.
/// </summary>
/// <remarks>
/// Scenarios use Latiprimere's public API only, and record what they find through <see cref="ScenarioContext"/>.
/// The same scenario runs under xUnit and in the console harness.
/// </remarks>
public sealed class Scenario
{
	/// <summary>
	/// Gets the scenario's ID: lower-case words separated by dots, such as <c>cups.raw.same-bytes</c>. Used to choose
	/// scenarios on the command line and to name their evidence folders.
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// Gets the part of Latiprimere the scenario checks.
	/// </summary>
	public required ScenarioArea Area { get; init; }

	/// <summary>
	/// Gets what the scenario shows when it passes, in one sentence.
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	/// Gets what the scenario needs; it is skipped wherever any of it is missing.
	/// </summary>
	public TargetNeeds Needs { get; init; }

	/// <summary>
	/// Gets the longest the scenario may run, or <see langword="null"/> for <see cref="ScenarioRunner.Timeout"/>.
	/// Scenarios that wait for a person or a real printer need longer.
	/// </summary>
	public TimeSpan? Timeout { get; init; }

	/// <summary>
	/// Gets the code that runs the scenario.
	/// </summary>
	public required Func<ScenarioContext, Task> RunAsync { get; init; }

	/// <summary>
	/// Returns the scenario's ID, which xUnit shows as the test case's name.
	/// </summary>
	/// <returns>The ID.</returns>
	public override string ToString()
	{
		return this.Id;
	}
}
