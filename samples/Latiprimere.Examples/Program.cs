using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;

// The examples use this namespace rather than Latiprimere.Examples, so each file needs "using Latiprimere;"
// just as the code in the docs does.
namespace LatiprimereExamples;

/// <summary>
/// Runs one of the documentation's examples, named on the command line, such as
/// <c>dotnet run -- first-print-job invoice.pdf</c>. With no name, lists the examples.
/// </summary>
internal static class Program
{
	/// <summary>
	/// Runs the example named by the first argument.
	/// </summary>
	/// <param name="args">The example's name, then its arguments.</param>
	/// <returns>0 if the example ran; 1 if the name or arguments weren't recognised.</returns>
	private static async Task<int> Main(string[] args)
	{
		string name = args.Length > 0 ? args[0] : string.Empty;
		string[] arguments = args.Length > 0 ? args[1..] : [];

		// These examples set up Latiprimere themselves
		switch (name)
		{
			case "first-print-job" when arguments.Length == 1:
				await FirstPrintJob.RunAsync(arguments[0]);
				return 0;

			case "without-di":
				await Installation.ListWithoutDependencyInjectionAsync();
				return 0;

			case "web":
				await WebService.RunAsync(arguments);
				return 0;
		}

		// The rest use the printer catalog with every print system registered
		using ServiceProvider provider = Installation.BuildServiceProvider();
		IPrinterCatalog catalog = provider.GetRequiredService<IPrinterCatalog>();

		switch (name)
		{
			case "list":
				await FindingPrinters.ListAsync(catalog);
				return 0;

			case "capabilities" when arguments.Length == 1:
				IPrinter printer = await catalog.GetPrinterAsync(arguments[0]);
				await ReadingCapabilities.ShowAsync(printer);
				await ReadingCapabilities.ShowVendorOptionsAsync(printer);
				PrintOptions best = await ReadingCapabilities.ChooseBestQualityAsync(printer);
				Console.WriteLine($"Best quality: {string.Join(", ", best.VendorOptions.Select(option => $"{option.Key}={option.Value}"))}");
				return 0;

			case "options" when arguments.Length == 2:
				await PrintOptionsAndJobs.PrintWithOptionsAsync(await catalog.GetPrinterAsync(arguments[0]), arguments[1]);
				return 0;

			case "label" when arguments.Length == 2:
				await LabelsAndReceipts.PrintLabelAsync(await catalog.GetPrinterAsync(arguments[0]), arguments[1]);
				return 0;

			case "receipt" when arguments.Length == 1:
				await LabelsAndReceipts.PrintReceiptAsync(await catalog.GetPrinterAsync(arguments[0]));
				return 0;

			case "errors" when arguments.Length == 2:
				using (FileStream document = File.OpenRead(arguments[1]))
				{
					await HandlingErrors.TryPrintAsync(catalog, arguments[0], document);
				}

				return 0;

			case "print-to-pdf" when arguments.Length == 2:
				using (FileStream document = File.OpenRead(arguments[0]))
				{
					await CupsAndWindows.SaveWithPrintToPdfAsync(catalog, document, Path.GetFullPath(arguments[1]));
				}

				return 0;

			default:
				Program.WriteUsage();
				return 1;
		}
	}

	/// <summary>
	/// Lists the examples and their arguments.
	/// </summary>
	private static void WriteUsage()
	{
		Console.WriteLine("Usage: dotnet run -- <example> [arguments]");
		Console.WriteLine();
		Console.WriteLine("  first-print-job <pdf>              Your first print job");
		Console.WriteLine("  without-di                         Installation, without dependency injection");
		Console.WriteLine("  list                               Finding printers");
		Console.WriteLine("  options <printer-id> <pdf>         Print options and following jobs");
		Console.WriteLine("  capabilities <printer-id>          Reading printer capabilities");
		Console.WriteLine("  label <printer-id> <order>         Labels and receipts: a ZPL label");
		Console.WriteLine("  receipt <printer-id>               Labels and receipts: an ESC/POS receipt");
		Console.WriteLine("  errors <printer-id> <pdf>          Errors and troubleshooting");
		Console.WriteLine("  print-to-pdf <pdf> <output-file>   CUPS servers and the Windows spooler");
		Console.WriteLine("  web                                Printing from a web app or background service");
	}
}
