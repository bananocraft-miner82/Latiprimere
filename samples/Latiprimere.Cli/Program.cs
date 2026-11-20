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

	/// <summary>The environment variable listing the SHA-256 fingerprints of printer certificates to trust over IPPS.</summary>
	internal const string IppFingerprintsVariable = "LATIPRIMERE_IPP_FINGERPRINTS";

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

		// The settings are checked as they are registered, so a bad printer address in the environment fails here
		try
		{
			services.AddLatiprimereIpp(Program.ConfigureIpp);
		}
		catch (ArgumentException exception)
		{
			ConsoleOutput.WriteError(CliMessages.InvalidArguments, exception.Message);

			return ExitCodes.InvalidArguments;
		}

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
		catch (ArgumentException exception)
		{
			// A vendor option the printer doesn't offer, or a Print Ticket that isn't one: the message says what's wrong
			ConsoleOutput.WriteError(CliMessages.InvalidArguments, exception.Message);

			return ExitCodes.InvalidArguments;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// A --ticket file, or the file to print, that can't be read
			ConsoleOutput.WriteError(CliMessages.InvalidArguments, CliMessages.FormatFileUnreadable(exception.Message));

			return ExitCodes.InvalidArguments;
		}
	}

	/// <summary>
	/// Configures direct IPP printing: discovery, plus any printer URIs listed (comma-separated) in the
	/// <c>LATIPRIMERE_IPP_PRINTERS</c> environment variable, and the certificates of printers reached over IPPS listed
	/// by SHA-256 fingerprint in <c>LATIPRIMERE_IPP_FINGERPRINTS</c>.
	/// </summary>
	/// <param name="options">The options to configure.</param>
	/// <exception cref="ArgumentException">A value in the first variable isn't an absolute <c>ipp</c> or <c>ipps</c>
	/// URI. A fingerprint that isn't one is refused as the options are registered.</exception>
	private static void ConfigureIpp(IppNetworkOptions options)
	{
		// Printers present certificates they made themselves, so one is trusted only once its fingerprint is listed,
		// checked against the printer's own web page
		string fingerprints = Environment.GetEnvironmentVariable(Program.IppFingerprintsVariable) ?? string.Empty;

		foreach (string fingerprint in fingerprints.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			options.TrustedCertificateFingerprints.Add(fingerprint);
		}

		string configuredPrinters = Environment.GetEnvironmentVariable(Program.IppPrintersVariable) ?? string.Empty;
		IEnumerable<string> uris = configuredPrinters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (string text in uris)
		{
			if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("ipp" or "ipps"))
			{
				throw new ArgumentException(CliMessages.FormatIppPrinterUriInvalid(Program.IppPrintersVariable, text));
			}

			options.PrinterUris.Add(uri);
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
			ListCommand.Name              => ListCommand.RunAsync(catalog),
			PrintCommand.Name             => PrintCommand.RunAsync(catalog, commandArguments),
			CapabilitiesCommand.Name      => CapabilitiesCommand.RunAsync(catalog, commandArguments),
			PrintCapabilitiesCommand.Name => PrintCapabilitiesCommand.RunAsync(catalog, commandArguments),
			_                             => Task.FromResult(ConsoleOutput.WriteUsage()),
		};
	}
}
