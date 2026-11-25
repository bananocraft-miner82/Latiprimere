using System;
using System.Linq;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Printer details, identifying a printer and test pages".
/// </summary>
internal static class PrinterDetailsAndTestPages
{
	/// <summary>
	/// Shows what a printer reports about itself beyond printing.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>A task that completes when the details have been shown.</returns>
	public static async Task ShowDetailsAsync(IPrinter printer)
	{
		PrinterDetails details = await printer.GetDetailsAsync();

		Console.WriteLine($"Web page:      {details.WebPage?.ToString() ?? "not reported"}");
		Console.WriteLine($"Supplies page: {details.SuppliesWebPage?.ToString() ?? "not reported"}");
		Console.WriteLine($"Icons:         {string.Join(", ", details.Icons)}");
		Console.WriteLine($"Firmware:      {string.Join(", ", details.Firmware.Select(part => $"{part.Name} {part.Version}"))}");
		Console.WriteLine($"Identify by:   {string.Join(", ", details.IdentifyActions)}");
	}

	/// <summary>
	/// Asks a printer to make itself known, if it can.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>A task that completes when the printer has been asked, or it was found it can't be.</returns>
	public static async Task IdentifyAsync(IPrinter printer)
	{
		try
		{
			await printer.IdentifyAsync($"This is {printer.Info.Name}");
			Console.WriteLine($"Asked {printer.Info.Name} to identify itself.");
		}
		catch (NotSupportedException exception)
		{
			Console.WriteLine($"{printer.Info.Name} can't be identified: {exception.Message}");
		}
	}

	/// <summary>
	/// Prints a test page and follows its job until it finishes.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>A task that completes when the job has finished.</returns>
	public static async Task PrintTestPageAsync(IPrinter printer)
	{
		IPrintJob job = await printer.PrintTestPageAsync(PrintOptions.Default);

		await foreach (PrintJobStatus status in job.WatchStatusAsync())
		{
			Console.WriteLine($"Test page: {status.State}");
		}
	}
}
