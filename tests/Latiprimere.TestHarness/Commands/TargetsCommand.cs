using Latiprimere.EndToEnd;

namespace Latiprimere.TestHarness.Commands;

/// <summary>
/// The <c>targets</c> command: the test printers found on this machine.
/// </summary>
internal static class TargetsCommand
{
	/// <summary>
	/// Shows the targets.
	/// </summary>
	/// <param name="cancellationToken">Cancels finding them.</param>
	/// <returns>The exit code.</returns>
	public static async Task<int> RunAsync(CancellationToken cancellationToken)
	{
		HarnessTargets targets = await HarnessTargets.DetectAsync(hasPerson: false, cancellationToken);

		foreach (string line in targets.Describe())
		{
			Console.WriteLine(line);
		}

		return ExitCodes.Success;
	}
}
