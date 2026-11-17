using Latiprimere.EndToEnd;

namespace Latiprimere.TestHarness.Commands;

/// <summary>
/// The <c>list</c> command: every scenario, what it needs, and whether it can run here.
/// </summary>
internal static class ListCommand
{
	/// <summary>
	/// Lists the scenarios.
	/// </summary>
	/// <param name="cancellationToken">Cancels finding the targets.</param>
	/// <returns>The exit code.</returns>
	public static async Task<int> RunAsync(CancellationToken cancellationToken)
	{
		HarnessTargets targets = await HarnessTargets.DetectAsync(hasPerson: false, cancellationToken);

		foreach (Scenario scenario in AllScenarios.List)
		{
			IReadOnlyList<string> missing = targets.DescribeMissing(scenario.Needs);
			string availability = missing.Count == 0 ? "can run here" : "skips here: " + string.Join("; ", missing);

			Console.WriteLine($"{scenario.Id}  [{scenario.Area}]");
			Console.WriteLine($"    {scenario.Title}");
			Console.WriteLine($"    Needs: {scenario.Needs}; {availability}");
		}

		return ExitCodes.Success;
	}
}
