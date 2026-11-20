using Latiprimere.EndToEnd;

namespace Latiprimere.TestHarness.Commands;

/// <summary>
/// The <c>manual</c> command: checks on a printer a person names, which may be real and print on paper (ADR 0029,
/// decision 5). The person confirms the printer, then each job before it prints, and says what came out.
/// </summary>
internal static class ManualCommand
{
	/// <summary>
	/// The scenarios run unless others are named: every manual one.
	/// </summary>
	private const string DefaultFilter = "manual.*";

	/// <summary>
	/// Describes the command, for the usage text.
	/// </summary>
	public static readonly string[] Usage =
	[
		"  manual --printer <id> [--fingerprint <sha256> ...] [filter ...]",
		"                       Guided checks on a printer you name, which may be real: reads it first, then prints",
		"                       each test job only after you say yes, and asks what came out. Filters choose the",
		"                       scenarios, as for run; by default every manual.* scenario. --fingerprint pins the",
		"                       certificate of a printer reached over ipps://, as checked on its own web page"
	];

	/// <summary>
	/// Runs the manual checks.
	/// </summary>
	/// <param name="arguments"><c>--printer &lt;id&gt;</c>, any <c>--fingerprint &lt;sha256&gt;</c>, then any
	/// filters.</param>
	/// <param name="cancellationToken">Stops the run.</param>
	/// <returns>The exit code.</returns>
	/// <exception cref="ArgumentException">No printer was named, or a filter matches no scenario.</exception>
	public static async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
	{
		string? printerId = null;
		List<string> fingerprints = new List<string>();
		List<string> filters = new List<string>();

		for (int index = 0; index < arguments.Length; index++)
		{
			if (arguments[index] == "--printer" && index + 1 < arguments.Length)
			{
				printerId = arguments[++index];
			}
			else if (arguments[index] == "--fingerprint" && index + 1 < arguments.Length)
			{
				// A printer's own certificate, pinned as an application would (ADR 0037)
				fingerprints.Add(arguments[++index]);
			}
			else
			{
				filters.Add(arguments[index]);
			}
		}

		if (string.IsNullOrWhiteSpace(printerId))
		{
			throw new ArgumentException("manual needs --printer <id>, such as --printer \"windows:Office Printer\". 'list' in the CLI sample shows the IDs.");
		}

		IReadOnlyList<Scenario> scenarios = AllScenarios.Select(filters.Count == 0 ? [ManualCommand.DefaultFilter] : filters);
		ConsolePerson person = new ConsolePerson();

		person.Tell
		(
			$"Manual checks on {printerId}.{Environment.NewLine}"
			+ $"This printer may be real. Each test job is shown first, and prints on paper only if you say yes;{Environment.NewLine}"
			+ $"answer the questions about what comes out. At most {ManualCommand.CountSheets(scenarios)} sheets in all. Ctrl+C stops."
		);

		if (!await person.ConfirmAsync($"Start the manual checks on {printerId}?", cancellationToken))
		{
			Console.WriteLine("Nothing was run.");

			return ExitCodes.Success;
		}

		HarnessTargets detected = await HarnessTargets.DetectAsync(hasPerson: true, printerId, cancellationToken);
		HarnessTargets targets = detected.WithManualCertificateFingerprints(fingerprints);

		return await RunCommand.RunScenariosAsync(scenarios, targets, "manual", person, cancellationToken);
	}

	/// <summary>
	/// Works out the most sheets the manual scenarios can print, to tell the person before they start.
	/// </summary>
	/// <param name="scenarios">The chosen scenarios.</param>
	/// <returns>The number of sheets.</returns>
	private static int CountSheets(IReadOnlyList<Scenario> scenarios)
	{
		// One page each, but two copies of three pages for two-sided and copies, and none for the printer's details
		return scenarios.Sum
		(
			scenario => scenario.Id switch
			{
				"manual.printer-details" => 0,
				"manual.two-sided-and-copies" => 6,
				_ => scenario.Area == ScenarioArea.Manual ? 1 : 0
			}
		);
	}
}
