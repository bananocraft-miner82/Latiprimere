using System;
using System.IO;
using System.Linq;
using System.Threading;
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

			case "logging":
				// Disposed before returning, so the console logger writes everything it was given
				using (ServiceProvider loggingProvider = Logging.BuildServiceProvider())
				{
					await FindingPrinters.ListAsync(loggingProvider.GetRequiredService<IPrinterCatalog>());
				}

				return 0;

			case "logging-without-di":
				await Logging.ListWithoutDependencyInjectionAsync();
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
				await ReadingCapabilities.ShowAsync(await catalog.GetPrinterAsync(arguments[0]));
				return 0;

			case "vendor-options" when arguments.Length == 1:
				IPrinter printer = await catalog.GetPrinterAsync(arguments[0]);
				await VendorOptions.ShowVendorOptionsAsync(printer);
				PrintOptions twoUp = await VendorOptions.ChooseTwoPagesPerSheetAsync(printer);
				Console.WriteLine($"Two pages per sheet: {string.Join(", ", twoUp.VendorOptions.Select(option => $"{option.Key}={option.Value}"))}");
				return 0;

			case "options" when arguments.Length == 2:
				await PrintOptionsAndJobs.PrintWithOptionsAsync(await catalog.GetPrinterAsync(arguments[0]), arguments[1]);
				return 0;

			case "pages" when arguments.Length == 2:
				await PrintOptionsAndJobs.PrintPagesAsync(await catalog.GetPrinterAsync(arguments[0]), arguments[1]);
				return 0;

			case "printer-status" when arguments.Length == 1:
				using (CancellationTokenSource following = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
				{
					await PrintOptionsAndJobs.FollowPrinterAsync(await catalog.GetPrinterAsync(arguments[0]), following.Token);
				}

				return 0;

			case "pdf-label" when arguments.Length == 2:
				using (FileStream label = File.OpenRead(arguments[1]))
				{
					await LabelsAndReceipts.PrintPdfLabelAsync(await catalog.GetPrinterAsync(arguments[0]), label);
				}

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
		Console.WriteLine("  vendor-options <printer-id>        Vendor options and Windows Print Capabilities");
		Console.WriteLine("  pages <printer-id> <pdf>           Print options: pages 2 and 3, collated, at draft quality");
		Console.WriteLine("  printer-status <printer-id>        Print options: following a printer for three seconds");
		Console.WriteLine("  label <printer-id> <order>         Labels and receipts: a ZPL label");
		Console.WriteLine("  pdf-label <printer-id> <pdf>       Labels and receipts: a PDF on 102 x 152 mm labels");
		Console.WriteLine("  receipt <printer-id>               Labels and receipts: an ESC/POS receipt");
		Console.WriteLine("  errors <printer-id> <pdf>          Errors and troubleshooting");
		Console.WriteLine("  logging                            Logging: listing printers, logging at Debug");
		Console.WriteLine("  logging-without-di                 Logging, without dependency injection");
		Console.WriteLine("  print-to-pdf <pdf> <output-file>   CUPS servers and the Windows spooler");
		Console.WriteLine("  web                                Printing from a web app or background service");
	}
}
