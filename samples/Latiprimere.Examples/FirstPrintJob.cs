using System;
using System.IO;
using System.Threading.Tasks;
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;

namespace LatiprimereExamples;

/// <summary>
/// The example from "Your first print job": a complete program that prints a PDF on the default printer.
/// </summary>
internal static class FirstPrintJob
{
	/// <summary>
	/// Prints a PDF on the default printer and follows the job until it finishes.
	/// </summary>
	/// <param name="pdfPath">The PDF to print.</param>
	/// <returns>A task that completes when the job has finished.</returns>
	public static async Task RunAsync(string pdfPath)
	{
		// Register Latiprimere and the print systems for the platforms you run on
		ServiceCollection services = new ServiceCollection();

		services.AddLatiprimere();
		services.AddLatiprimereCups();     // Linux and macOS
		services.AddLatiprimereWindows();  // Windows
		services.AddLatiprimerePdfium();   // PDFs on Windows

		using ServiceProvider provider = services.BuildServiceProvider();
		IPrinterCatalog catalog = provider.GetRequiredService<IPrinterCatalog>();

		// Find the default printer
		IPrinter? printer = await catalog.GetDefaultPrinterAsync();

		if (printer is null)
		{
			Console.WriteLine("There is no default printer.");

			return;
		}

		Console.WriteLine($"Printing on {printer.Info.Name}");

		// Send the PDF
		using FileStream document = File.OpenRead(pdfPath);
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default);

		// Follow the job until it finishes
		await foreach (PrintJobStatus status in job.WatchStatusAsync())
		{
			Console.WriteLine($"Job {job.Id}: {status.State}");
		}
	}
}
