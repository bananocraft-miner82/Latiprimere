using Latiprimere.Cli.Commands;
using Latiprimere.Cli.Resources;
using Latiprimere.Ipp;
using Microsoft.Extensions.DependencyInjection;

namespace Latiprimere.Cli;

/// <summary>
/// A command-line harness for trying Latiprimere: lists printers, shows what they can do, and prints files.
/// </summary>
internal static class Program
{
	/// <summary>The environment variable listing extra IPP printer URIs.</summary>
	private const string IppPrintersVariable = "LATIPRIMERE_IPP_PRINTERS";

	/// <summary>
	/// Runs the command named on the command line.
	/// </summary>
	/// <param name="args">The command and its arguments.</param>
	/// <returns>The process exit code.</returns>
	private static async Task<int> Main(string[] args)
	{
		// Register Latiprimere with every platform adapter built so far
		ServiceCollection services = new ServiceCollection();

		services.AddLatiprimere();
		services.AddLatiprimereCups();
		services.AddLatiprimereWindows();
		services.AddLatiprimerePdfium();
		services.AddLatiprimereIpp(Program.ConfigureIpp);

		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrinterCatalog catalog = serviceProvider.GetRequiredService<IPrinterCatalog>();

		// Run the command, reporting print failures as messages rather than stack traces
		try
		{
			return await Program.RunCommandAsync(catalog, args);
		}
		catch (PrintSystemUnavailableException exception)
		{
			ConsoleOutput.WriteError(CliMessages.PrintingUnavailable, exception.Message);

			return ExitCodes.PrintingUnavailable;
		}
		catch (PrintException exception)
		{
			ConsoleOutput.WritePrintException(exception);

			return ExitCodes.PrintFailed;
		}
		catch (NotSupportedException exception)
		{
			// The printer can't take this document, or the print system can't print to a file
			ConsoleOutput.WriteError(CliMessages.PrintFailed, exception.Message);

			return ExitCodes.PrintFailed;
		}
	}

	/// <summary>
	/// Configures direct IPP printing: discovery, plus any printer URIs listed (comma-separated) in the
	/// <c>LATIPRIMERE_IPP_PRINTERS</c> environment variable.
	/// </summary>
	/// <param name="options">The options to configure.</param>
	private static void ConfigureIpp(IppNetworkOptions options)
	{
		string configuredPrinters = Environment.GetEnvironmentVariable(Program.IppPrintersVariable) ?? string.Empty;
		IEnumerable<string> uris = configuredPrinters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (string uri in uris)
		{
			options.PrinterUris.Add(new Uri(uri));
		}
	}

	/// <summary>
	/// Dispatches to the command named by the first argument.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="args">The command and its arguments.</param>
	/// <returns>The process exit code.</returns>
	private static Task<int> RunCommandAsync(IPrinterCatalog catalog, string[] args)
	{
		string command = args.Length > 0 ? args[0] : ListCommand.Name;
		string[] commandArguments = args.Skip(1).ToArray();

		return command switch
		{
			ListCommand.Name         => ListCommand.RunAsync(catalog),
			PrintCommand.Name        => PrintCommand.RunAsync(catalog, commandArguments),
			CapabilitiesCommand.Name => CapabilitiesCommand.RunAsync(catalog, commandArguments),
			_                        => Task.FromResult(ConsoleOutput.WriteUsage()),
		};
	}
}
