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
/// The examples from "Reading printer capabilities".
/// </summary>
internal static class ReadingCapabilities
{
	/// <summary>
	/// Shows what a printer reports it can do.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>A task that completes when the capabilities have been shown.</returns>
	public static async Task ShowAsync(IPrinter printer)
	{
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

		Console.WriteLine($"Paper sizes:   {string.Join(", ", capabilities.MediaSizes.Select(size => size.DisplayName))}");
		Console.WriteLine($"Default paper: {capabilities.DefaultMediaSize?.DisplayName ?? "not reported"}");
		Console.WriteLine($"Sides:         {string.Join(", ", capabilities.DuplexModes)}");
		Console.WriteLine($"Colour:        {string.Join(", ", capabilities.ColorModes)}");
		Console.WriteLine($"Orientations:  {string.Join(", ", capabilities.Orientations)}");
		Console.WriteLine($"Trays:         {string.Join(", ", capabilities.InputTrays.Select(tray => tray.Name))}");
		Console.WriteLine($"Most copies:   {(capabilities.MaxCopies is int maxCopies ? $"{maxCopies}" : "not reported")}");
	}

	/// <summary>
	/// Chooses options a printer can carry out: two-sided only if it reports it can, and no more copies than its
	/// limit.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <param name="copiesWanted">How many copies the user asked for.</param>
	/// <returns>The options to print with.</returns>
	public static async Task<PrintOptions> ChooseOptionsAsync(IPrinter printer, int copiesWanted)
	{
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

		// An empty list means the printer didn't say, so leave the choice to the printer
		DuplexMode duplex = capabilities.DuplexModes.Contains(DuplexMode.LongEdge)
			? DuplexMode.LongEdge
			: DuplexMode.PrinterDefault;

		int copies = capabilities.MaxCopies is int maxCopies ? Math.Min(copiesWanted, maxCopies) : copiesWanted;

		return new PrintOptions { Copies = copies, Duplex = duplex };
	}

	/// <summary>
	/// Chooses A4 paper and monochrome printing, each only if the printer reports it.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>The options to print with.</returns>
	public static async Task<PrintOptions> ChoosePaperAndColourAsync(IPrinter printer)
	{
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

		// Take the size from the capabilities, matched by its Name; null leaves the printer's default paper
		MediaSize? a4 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm");

		ColorMode colorMode = capabilities.ColorModes.Contains(ColorMode.Monochrome)
			? ColorMode.Monochrome
			: ColorMode.PrinterDefault;

		return new PrintOptions { MediaSize = a4, ColorMode = colorMode };
	}

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
				VendorOptionKind.Number => $"a number from {option.MinimumValue?.ToString(CultureInfo.CurrentCulture) ?? "any"} to {option.MaximumValue?.ToString(CultureInfo.CurrentCulture) ?? "any"}",
				_ => "text",
			};

			Console.WriteLine($"{option.Key} ({option.DisplayName}): {values}; default {option.DefaultValue ?? "not reported"}");
		}
	}

	/// <summary>
	/// Asks for the printer's best print quality, through its vendor options, when it offers one.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>The options; the defaults if the printer offers no print quality.</returns>
	public static async Task<PrintOptions> ChooseBestQualityAsync(IPrinter printer)
	{
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

		// IPP printers and CUPS list ipp:print-quality; Windows drivers list the Print Schema's PageOutputQuality
		VendorOption? quality = capabilities.VendorOptions.FirstOrDefault
		(
			option => option.Key is "ipp:print-quality" or "windows:feature:psk:PageOutputQuality"
		);

		// 5 is IPP's "high"; psk:High is the Print Schema's
		VendorOptionChoice? best = quality?.Choices.FirstOrDefault(choice => choice.Value is "5" or "psk:High");

		if (quality is null || best is null)
		{
			return PrintOptions.Default;
		}

		return new PrintOptions
		{
			VendorOptions = new Dictionary<string, string> { [quality.Key] = best.Value }
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
		if (printer is not IWindowsPrinter windowsPrinter)
		{
			return;
		}

		string capabilities = await windowsPrinter.GetPrintCapabilitiesAsync();
		string defaultTicket = await windowsPrinter.GetDefaultPrintTicketAsync();

		await File.WriteAllTextAsync(Path.Combine(folder, "print-capabilities.xml"), capabilities);
		await File.WriteAllTextAsync(Path.Combine(folder, "default-print-ticket.xml"), defaultTicket);
	}
}
