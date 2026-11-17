namespace Latiprimere.EndToEnd.Reporting;

/// <summary>
/// One run of the harness: where and how it ran, the targets it found, and each scenario's result.
/// </summary>
public sealed class HarnessReport
{
	/// <summary>
	/// Gets the program that ran the scenarios, such as <c>xunit</c> or <c>console</c>.
	/// </summary>
	public required string Host { get; init; }

	/// <summary>
	/// Gets the operating system: <c>windows</c>, <c>linux</c> or <c>macos</c>.
	/// </summary>
	public required string OperatingSystem { get; init; }

	/// <summary>
	/// Gets the .NET the scenarios ran on, such as <c>net10.0</c>.
	/// </summary>
	public required string Framework { get; init; }

	/// <summary>
	/// Gets when the run started.
	/// </summary>
	public required DateTimeOffset StartedAt { get; init; }

	/// <summary>
	/// Gets when the run finished.
	/// </summary>
	public required DateTimeOffset FinishedAt { get; init; }

	/// <summary>
	/// Gets the targets found, one line each.
	/// </summary>
	public required IReadOnlyList<string> Targets { get; init; }

	/// <summary>
	/// Gets each scenario's result, in the order they ran.
	/// </summary>
	public required IReadOnlyList<ScenarioResult> Results { get; init; }

	/// <summary>
	/// Gets the number of scenarios that passed.
	/// </summary>
	public int Passed
	{
		get
		{
			return this.Results.Count(result => result.Outcome == ScenarioOutcome.Passed);
		}
	}

	/// <summary>
	/// Gets the number of scenarios that failed.
	/// </summary>
	public int Failed
	{
		get
		{
			return this.Results.Count(result => result.Outcome == ScenarioOutcome.Failed);
		}
	}

	/// <summary>
	/// Gets the number of scenarios skipped.
	/// </summary>
	public int Skipped
	{
		get
		{
			return this.Results.Count(result => result.Outcome == ScenarioOutcome.Skipped);
		}
	}
}
