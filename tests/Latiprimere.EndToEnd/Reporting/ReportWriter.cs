using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Latiprimere.EndToEnd.Reporting;

/// <summary>
/// Writes a run's report as <c>report.md</c>, to read or paste into docs/progress.md, and <c>report.json</c>, for
/// tools.
/// </summary>
public static class ReportWriter
{
	/// <summary>
	/// Writes both files into the run's report folder.
	/// </summary>
	/// <param name="report">The report.</param>
	/// <param name="reportDirectory">The run's report folder.</param>
	/// <param name="cancellationToken">Cancels writing.</param>
	/// <returns>The path of <c>report.md</c>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="report"/> is <see langword="null"/>.</exception>
	public static async Task<string> WriteAsync(HarnessReport report, string reportDirectory, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(report);
		ArgumentException.ThrowIfNullOrWhiteSpace(reportDirectory);

		Directory.CreateDirectory(reportDirectory);
		string markdownPath = Path.Combine(reportDirectory, "report.md");
		await File.WriteAllTextAsync(markdownPath, ReportWriter.ToMarkdown(report, reportDirectory), cancellationToken);

		string json = JsonSerializer.Serialize(report, ReportJsonContext.Default.HarnessReport);
		await File.WriteAllTextAsync(Path.Combine(reportDirectory, "report.json"), json, cancellationToken);

		return markdownPath;
	}

	/// <summary>
	/// Formats a report as Markdown.
	/// </summary>
	/// <param name="report">The report.</param>
	/// <param name="reportDirectory">The report folder; evidence paths are shown relative to it.</param>
	/// <returns>The Markdown.</returns>
	public static string ToMarkdown(HarnessReport report, string reportDirectory)
	{
		ArgumentNullException.ThrowIfNull(report);

		StringBuilder text = new StringBuilder();
		CultureInfo culture = CultureInfo.InvariantCulture;

		text.AppendLine(culture, $"# End-to-end harness: {report.Passed} passed, {report.Failed} failed, {report.Skipped} skipped");
		text.AppendLine();
		text.AppendLine(culture, $"- Run: {report.StartedAt:yyyy-MM-dd HH:mm:ss zzz}, {(report.FinishedAt - report.StartedAt).TotalSeconds:0} s");
		text.AppendLine(culture, $"- On: {report.OperatingSystem}, {report.Framework}, {report.Host}");

		foreach (string target in report.Targets)
		{
			text.AppendLine(culture, $"- {target}");
		}

		text.AppendLine();
		text.AppendLine("| Scenario | Area | Outcome | Time | Notes |");
		text.AppendLine("|---|---|---|---|---|");

		foreach (ScenarioResult result in report.Results)
		{
			string notes = result.Outcome switch
			{
				ScenarioOutcome.Skipped => result.SkipReason ?? string.Empty,
				ScenarioOutcome.Failed => string.Join("; ", result.Checks.Where(check => !check.Passed).Select(check => check.Name))
					+ (result.Error is null ? string.Empty : " (error)"),
				_ => $"{result.Checks.Count} checks"
			};

			string time = result.Outcome == ScenarioOutcome.Skipped ? "—" : string.Create(culture, $"{result.Duration.TotalSeconds:0.0} s");
			text.AppendLine(culture, $"| `{result.Id}` | {result.Area} | {result.Outcome} | {time} | {ReportWriter.EscapeCell(notes)} |");
		}

		foreach (ScenarioResult result in report.Results.Where(result => result.Outcome != ScenarioOutcome.Skipped))
		{
			text.AppendLine();
			text.AppendLine(culture, $"## {result.Id}: {result.Outcome}");
			text.AppendLine();
			text.AppendLine(result.Title);
			text.AppendLine();

			foreach (ScenarioCheck check in result.Checks)
			{
				string detail = check.Detail is null ? string.Empty : $" ({check.Detail})";
				text.AppendLine(culture, $"- {(check.Passed ? "✅" : "❌")} {check.Name}{detail}");
			}

			if (result.Error is not null)
			{
				text.AppendLine();
				text.AppendLine("```text");
				text.AppendLine(result.Error);
				text.AppendLine("```");
			}

			if (result.Log.Count > 0)
			{
				text.AppendLine();
				text.AppendLine("Notes:");

				foreach (string line in result.Log)
				{
					text.AppendLine(culture, $"- {line}");
				}
			}

			foreach (ScenarioEvidence item in result.Evidence)
			{
				string path = Path.GetRelativePath(reportDirectory, item.Path).Replace('\\', '/');
				text.AppendLine(culture, $"- Evidence: [{item.Description}]({Uri.EscapeDataString(path).Replace("%2F", "/", StringComparison.Ordinal)})");
			}
		}

		return text.ToString();
	}

	/// <summary>
	/// Makes text safe inside a Markdown table cell.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The text with pipes escaped and line breaks removed.</returns>
	private static string EscapeCell(string text)
	{
		return text.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
	}
}
