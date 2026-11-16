using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Finding printers": listing printers and looking one up by a saved ID.
/// </summary>
internal static class FindingPrinters
{
	/// <summary>
	/// Lists every printer the catalog can see, with the default printer marked.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <returns>A task that completes when the printers have been listed.</returns>
	public static async Task ListAsync(IPrinterCatalog catalog)
	{
		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync();

		foreach (PrinterInfo printer in printers)
		{
			string marker = printer.IsDefault ? "*" : " ";

			Console.WriteLine($"{marker} {printer.Name}");
			Console.WriteLine($"    ID:       {printer.Id}");
			Console.WriteLine($"    State:    {printer.State}");
			Console.WriteLine($"    Model:    {printer.MakeAndModel ?? "not reported"}");
			Console.WriteLine($"    Location: {printer.Location ?? "not reported"}");
		}
	}

	/// <summary>
	/// Looks up a printer by an ID saved earlier, falling back to the default printer if it has gone.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="savedPrinterId">The printer ID saved in the application's settings.</param>
	/// <returns>The printer, or <see langword="null"/> if it has gone and there is no default printer.</returns>
	public static async Task<IPrinter?> FindSavedPrinterAsync(IPrinterCatalog catalog, string savedPrinterId)
	{
		try
		{
			return await catalog.GetPrinterAsync(savedPrinterId);
		}
		catch (PrinterNotFoundException)
		{
			Console.WriteLine($"Printer {savedPrinterId} isn't available, so using the default printer.");

			return await catalog.GetDefaultPrinterAsync();
		}
	}
}
