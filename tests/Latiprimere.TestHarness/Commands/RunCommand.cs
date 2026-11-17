using Latiprimere.EndToEnd;
using Latiprimere.EndToEnd.Reporting;

namespace Latiprimere.TestHarness.Commands;

/// <summary>
/// The <c>run</c> command: runs scenarios one after another, showing each check as it happens, then writes the
/// report. Manual mode runs its scenarios the same way, with a person.
/// </summary>
internal static class RunCommand
{
	/// <summary>
	/// Runs the chosen scenarios, unattended.
	/// </summary>
	/// <param name="filters">Scenario IDs, ID prefixes ending in <c>*</c>, or area names; none runs everything.</param>
	/// <param name="cancellationToken">Stops the run; the report is still written.</param>
	/// <returns>The exit code.</returns>
	/// <exception cref="ArgumentException">A filter matches no scenario.</exception>
	public static async Task<int> RunAsync(string[] filters, CancellationToken cancellationToken)
	{
		IReadOnlyList<Scenario> scenarios = AllScenarios.Select(filters);
		HarnessTargets targets = await HarnessTargets.DetectAsync(hasPerson: false, cancellationToken);

		return await RunCommand.RunScenariosAsync(scenarios, targets, "console", person: null, cancellationToken);
	}

	/// <summary>
	/// Runs scenarios and writes the report.
	/// </summary>
	/// <param name="scenarios">The scenarios.</param>
	/// <param name="targets">The targets found.</param>
	/// <param name="host">Names the report folder: <c>console</c> or <c>manual</c>.</param>
	/// <param name="person">The person at the keyboard, or <see langword="null"/> for none.</param>
	/// <param name="cancellationToken">Stops the run; the report is still written.</param>
	/// <returns>The exit code.</returns>
	public static async Task<int> RunScenariosAsync
	(
		IReadOnlyList<Scenario> scenarios,
		HarnessTargets targets,
		string host,
		IPerson? person,
		CancellationToken cancellationToken
	)
	{
		DateTimeOffset startedAt = DateTimeOffset.Now;
		string reportDirectory = HarnessEnvironment.CreateReportDirectory(host);

		Console.WriteLine($"Latiprimere end-to-end harness: {scenarios.Count} scenarios on {HarnessEnvironment.OperatingSystemName}, {HarnessEnvironment.FrameworkName}");

		foreach (string line in targets.Describe())
		{
			Console.WriteLine("  " + line);
		}

		Console.WriteLine();
		List<ScenarioResult> results = new List<ScenarioResult>();
		bool isCancelled = false;

		foreach (Scenario scenario in scenarios)
		{
			try
			{
				results.Add(await ScenarioRunner.RunAsync(scenario, targets, reportDirectory, Console.WriteLine, person, cancellationToken));
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				isCancelled = true;

				break;
			}
		}

		HarnessReport report = new HarnessReport
		{
			Host = host,
			OperatingSystem = HarnessEnvironment.OperatingSystemName,
			Framework = HarnessEnvironment.FrameworkName,
			StartedAt = startedAt,
			FinishedAt = DateTimeOffset.Now,
			Targets = targets.Describe(),
			Results = results
		};

		string reportPath = await ReportWriter.WriteAsync(report, reportDirectory, CancellationToken.None);
		Console.WriteLine();
		Console.WriteLine($"{report.Passed} passed, {report.Failed} failed, {report.Skipped} skipped. Report: {reportPath}");

		if (isCancelled)
		{
			return ExitCodes.Cancelled;
		}

		return report.Failed > 0 ? ExitCodes.ScenarioFailed : ExitCodes.Success;
	}
}
