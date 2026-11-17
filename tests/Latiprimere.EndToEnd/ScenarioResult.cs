using System.Globalization;
using System.Text;

namespace Latiprimere.EndToEnd;

/// <summary>
/// What happened when a scenario ran.
/// </summary>
public sealed class ScenarioResult
{
	/// <summary>
	/// Gets the scenario's ID.
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// Gets the part of Latiprimere the scenario checks.
	/// </summary>
	public required ScenarioArea Area { get; init; }

	/// <summary>
	/// Gets what the scenario shows when it passes.
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	/// Gets how the scenario ended.
	/// </summary>
	public required ScenarioOutcome Outcome { get; init; }

	/// <summary>
	/// Gets how long it took.
	/// </summary>
	public TimeSpan Duration { get; init; }

	/// <summary>
	/// Gets why the scenario was skipped, or <see langword="null"/> if it ran.
	/// </summary>
	public string? SkipReason { get; init; }

	/// <summary>
	/// Gets the error that ended the scenario early, or <see langword="null"/>.
	/// </summary>
	public string? Error { get; init; }

	/// <summary>
	/// Gets the checks made, in order.
	/// </summary>
	public IReadOnlyList<ScenarioCheck> Checks { get; init; } = [];

	/// <summary>
	/// Gets the evidence kept.
	/// </summary>
	public IReadOnlyList<ScenarioEvidence> Evidence { get; init; } = [];

	/// <summary>
	/// Gets the progress notes.
	/// </summary>
	public IReadOnlyList<string> Log { get; init; } = [];

	/// <summary>
	/// Describes why the scenario failed: the failed checks, the error, and where the evidence is.
	/// </summary>
	/// <returns>The description, several lines long.</returns>
	public string DescribeFailure()
	{
		StringBuilder text = new StringBuilder();
		text.AppendLine(CultureInfo.InvariantCulture, $"Scenario {this.Id} failed: {this.Title}");

		foreach (ScenarioCheck check in this.Checks.Where(check => !check.Passed))
		{
			string detail = check.Detail is null ? string.Empty : $" ({check.Detail})";
			text.AppendLine(CultureInfo.InvariantCulture, $"  FAIL {check.Name}{detail}");
		}

		if (this.Checks.Count == 0 && this.Error is null)
		{
			text.AppendLine("  The scenario made no checks.");
		}

		if (this.Error is not null)
		{
			text.AppendLine("  Error: " + this.Error);
		}

		foreach (ScenarioEvidence item in this.Evidence)
		{
			text.AppendLine(CultureInfo.InvariantCulture, $"  Evidence: {item.Description}: {item.Path}");
		}

		return text.ToString();
	}
}
