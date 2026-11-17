using Latiprimere.EndToEnd;
using Latiprimere.TestHarness.Commands;

namespace Latiprimere.TestHarness;

/// <summary>
/// The console harness for Latiprimere's end-to-end scenarios: lists them, shows which test printers this machine
/// has, and runs them with a report. See docs/test-harness-plan.md.
/// </summary>
internal static class Program
{
	/// <summary>
	/// Runs the command named on the command line.
	/// </summary>
	/// <param name="args">The command and its arguments.</param>
	/// <returns>The process exit code (<see cref="ExitCodes"/>).</returns>
	private static async Task<int> Main(string[] args)
	{
		using CancellationTokenSource cancellation = new CancellationTokenSource();

		// Ctrl+C stops the run between checks rather than killing the process, so the report is still written
		Console.CancelKeyPress += (sender, eventArgs) =>
		{
			eventArgs.Cancel = true;
			cancellation.Cancel();
		};

		string command = args.Length > 0 ? args[0] : string.Empty;
		string[] arguments = args.Length > 0 ? args[1..] : [];

		try
		{
			switch (command)
			{
				case "list":
					return await ListCommand.RunAsync(cancellation.Token);

				case "targets":
					return await TargetsCommand.RunAsync(cancellation.Token);

				case "run":
					return await RunCommand.RunAsync(arguments, cancellation.Token);

				case "fake-printer":
					return await FakePrinterCommand.RunAsync(arguments, cancellation.Token);

				case "manual":
					return await ManualCommand.RunAsync(arguments, cancellation.Token);

				case "capture":
					return await CaptureCommand.RunAsync(arguments, cancellation.Token);

				default:
					Program.WriteUsage();

					return command is "" or "help" or "--help" or "-h" ? ExitCodes.Success : ExitCodes.InvalidArguments;
			}
		}
		catch (ArgumentException exception)
		{
			Console.Error.WriteLine(exception.Message);

			return ExitCodes.InvalidArguments;
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
			Console.Error.WriteLine("Cancelled.");

			return ExitCodes.Cancelled;
		}
	}

	/// <summary>
	/// Shows the commands.
	/// </summary>
	private static void WriteUsage()
	{
		Console.WriteLine("Latiprimere end-to-end test harness");
		Console.WriteLine();
		Console.WriteLine("Usage: Latiprimere.TestHarness <command> [arguments]");
		Console.WriteLine();
		Console.WriteLine("  list                 Lists the scenarios, and whether each can run on this machine");
		Console.WriteLine("  targets              Shows the test printers found on this machine");
		Console.WriteLine("  run [filter ...]     Runs the scenarios and writes a report. Filters are scenario IDs,");
		Console.WriteLine("                       ID prefixes ending in * (such as cups.*) or areas (such as Lifecycle);");
		Console.WriteLine("                       with none, every scenario runs");

		foreach (string line in ManualCommand.Usage.Concat(CaptureCommand.Usage))
		{
			Console.WriteLine(line);
		}

		foreach (string line in FakePrinterCommand.Usage)
		{
			Console.WriteLine(line);
		}

		Console.WriteLine();
		Console.WriteLine("The test printers are found from the LATIPRIMERE_TEST_* variables (see CONTRIBUTING.md);");
		Console.WriteLine("tools/test-harness/run.ps1 and run.sh set them up. Reports go to out/harness, or to");
		Console.WriteLine($"{HarnessEnvironment.ReportRootVariable} if set.");
		Console.WriteLine();
		Console.WriteLine("Exit codes: 0 every scenario that ran passed; 1 a scenario failed; 2 bad arguments.");
	}
}
