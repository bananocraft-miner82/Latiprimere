using System;
using System.Linq;
using System.Threading.Tasks;
using Latiprimere;

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
}
