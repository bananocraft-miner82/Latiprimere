using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Latiprimere;
using Latiprimere.Windows;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Vendor options and Windows Print Capabilities".
/// </summary>
internal static class VendorOptions
{
	/// <summary>
	/// Shows the printer's vendor options: everything it offers beyond the typed options.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>A task that completes when the options have been shown.</returns>
	public static async Task ShowVendorOptionsAsync(IPrinter printer)
	{
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

		foreach (VendorOption option in capabilities.VendorOptions)
		{
			string values = option.Kind switch
			{
				VendorOptionKind.Choice => string.Join(", ", option.Choices.Select(choice => $"{choice.Value} ({choice.DisplayName})")),
				VendorOptionKind.Number => $"a number from {option.MinValue?.ToString(CultureInfo.CurrentCulture) ?? "any"} to {option.MaxValue?.ToString(CultureInfo.CurrentCulture) ?? "any"}",
				_ => "text",
			};

			Console.WriteLine($"{option.Key} ({option.DisplayName}): {values}; default {option.DefaultValue ?? "not reported"}");
		}
	}

	/// <summary>
	/// Asks for two pages on each sheet, through the printer's vendor options, when it offers that.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>The options; the defaults if the printer doesn't offer two pages per sheet.</returns>
	public static async Task<PrintOptions> ChooseTwoPagesPerSheetAsync(IPrinter printer)
	{
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

		// IPP printers and CUPS list ipp:number-up; Windows drivers list the Print Schema's DocumentNUp
		VendorOption? pagesPerSheet = capabilities.VendorOptions.FirstOrDefault
		(
			option => option.Key is "ipp:number-up" or "windows:feature:psk:DocumentNUp"
		);

		VendorOptionChoice? two = pagesPerSheet?.Choices.FirstOrDefault(choice => choice.Value is "2" or "psk:PagesPerSheet=2");

		if (pagesPerSheet is null || two is null)
		{
			return PrintOptions.Default;
		}

		return new PrintOptions
		{
			VendorOptions = new Dictionary<string, string> { [pagesPerSheet.Key] = two.Value }
		};
	}

	/// <summary>
	/// Saves a Windows printer driver's Print Capabilities and default Print Ticket, to write a Print Ticket from.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <param name="folder">The folder to save them in.</param>
	/// <returns>A task that completes when they are saved; nothing is saved for a printer that isn't a Windows
	/// one.</returns>
	public static async Task SavePrintCapabilitiesAsync(IPrinter printer, string folder)
	{
		if (printer.GetFeature<IWindowsPrinter>() is not { } windowsPrinter)
		{
			return;
		}

		string capabilities = await windowsPrinter.GetPrintCapabilitiesAsync();
		string defaultTicket = await windowsPrinter.GetDefaultPrintTicketAsync();

		await File.WriteAllTextAsync(Path.Combine(folder, "print-capabilities.xml"), capabilities);
		await File.WriteAllTextAsync(Path.Combine(folder, "default-print-ticket.xml"), defaultTicket);
	}
}
