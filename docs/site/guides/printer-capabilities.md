---
title: Reading printer capabilities
description: Read a printer's paper sizes, duplex and colour modes, trays, copy limit and vendor options, and choose options it can carry out.
sidebar:
  order: 2
---

`IPrinter.GetCapabilitiesAsync()` asks the print system what a printer can do. Use it to offer only the choices a printer supports, or to check options before printing.

## Read the capabilities

```csharp
static async Task ShowAsync(IPrinter printer)
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
```

For Microsoft Print to PDF on Windows, this prints (with the list of paper sizes shortened):

```text
Paper sizes:   Letter, Tabloid, Legal, Statement, Executive, A3, A4, A5, …
Default paper: A4
Sides:         OneSided
Colour:        Monochrome, Color
Orientations:  Portrait, Landscape
Trays:
Most copies:   1
```

## What's in `PrinterCapabilities`

| Property                             | What it holds                                        |
| ------------------------------------ | ---------------------------------------------------- |
| `MediaSizes`, `DefaultMediaSize`     | Paper sizes, as `MediaSize`                          |
| `DuplexModes`, `DefaultDuplexMode`   | `OneSided`, `LongEdge`, `ShortEdge`                  |
| `ColorModes`, `DefaultColorMode`     | `Monochrome`, `Color`                                |
| `Orientations`                       | `Portrait`, `Landscape`                              |
| `InputTrays`                         | Paper sources, as `InputTray` (an `Id` and a `Name`) |
| `CustomMediaSizes`                   | Ranges of custom or roll sizes, as `MediaSizeRange`  |
| `MediaTypes`                         | Kinds of paper, as `MediaType` (an `Id` and a `Name`) |
| `PrintQualities`, `DefaultPrintQuality` | `Draft`, `Normal`, `High`                         |
| `Finishings`                         | Staples, holes and folds, as `Finishing`             |
| `OutputBins`                         | Where printed sheets go, as `OutputBin` (an `Id` and a `Name`) |
| `SupportsPageRanges`                 | Whether the printer itself prints only some pages    |
| `MaxCopies`                          | The most copies one job can ask for                  |
| `VendorOptions`                      | Everything else the printer offers, as `VendorOption`: see below |

A `MediaSize` has a `DisplayName` to show people, a `Name` that is the print system's own name for it, and its size in `WidthMillimeters` and `HeightMillimeters`.

:::note[Empty means "not reported"]
An empty list or a `null` value means the print system didn't say, not that the printer can't. The lists never contain the `PrinterDefault` values.
:::

Capabilities come from the printer or its driver, so reading them can take a moment, especially for network printers. Read them when you need them, and keep them for as long as the printer is in use rather than reading them for every job.

## Choose options the printer can carry out

This asks for two-sided printing only when the printer reports it, and keeps the number of copies within the printer's limit:

```csharp
static async Task<PrintOptions> ChooseOptionsAsync(IPrinter printer, int copiesWanted)
{
	PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

	// An empty list means the printer didn't say, so leave the choice to the printer
	DuplexMode duplex = capabilities.DuplexModes.Contains(DuplexMode.LongEdge)
		? DuplexMode.LongEdge
		: DuplexMode.PrinterDefault;

	int copies = capabilities.MaxCopies is int maxCopies ? Math.Min(copiesWanted, maxCopies) : copiesWanted;

	return new PrintOptions { Copies = copies, Duplex = duplex };
}
```

## Paper size, trays and colour

Choose them with `PrintOptions.MediaSize`, `PrintOptions.InputTray` and `PrintOptions.ColorMode`, passing values from the capabilities rather than building your own. This asks for A4 and monochrome, each only when the printer reports it:

```csharp
static async Task<PrintOptions> ChoosePaperAndColourAsync(IPrinter printer)
{
	PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

	// Take the size from the capabilities, matched by its Name; null leaves the printer's default paper
	MediaSize? a4 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm");

	ColorMode colorMode = capabilities.ColorModes.Contains(ColorMode.Monochrome)
		? ColorMode.Monochrome
		: ColorMode.PrinterDefault;

	return new PrintOptions { MediaSize = a4, ColorMode = colorMode };
}
```

A tray works the same way: pick one from `InputTrays` and pass it as `InputTray`. Its `Id` is the print system's own, such as `manual` for IPP or a tray number on Windows, so choose by `Id` only on the print system you read it from. Leaving an option unset, or `PrinterDefault`, uses the printer's default, which you can see in `DefaultMediaSize` and `DefaultColorMode`.

How each print system carries them out:

- **CUPS and network printers** get them as IPP job attributes. A printer that can't do one may print on something close instead, or refuse the job with a `PrintException`. A PDF sent to a network printer as raster, because it can't take PDF, is also rendered at that paper size and in that colour.
- **Windows** checks a size or tray against the driver's own lists, and refuses one the driver doesn't list with `NotSupportedException` before anything is queued, since the driver would otherwise print on its default paper without saying so. Monochrome pages are made grey before the driver gets them, as some drivers, Microsoft Print to PDF among them, ignore the setting.
- **Raw jobs** don't use them: the data is sent unchanged, so the printer's own language chooses paper and colour. See [Labels and receipts](/docs/guides/labels-and-receipts/).

## Vendor options: everything else the printer offers

Printers offer far more than the typed options cover: resolution, pages per sheet, finishings beyond staples, holes and folds, and settings a driver invents for itself. `VendorOptions` lists them, each with the key and values to pass back through `PrintOptions.VendorOptions`. On Windows, `IWindowsPrinter` also reads the driver's Print Capabilities and default Print Ticket. Both are described in [Vendor options and Windows Print Capabilities](/docs/guides/vendor-options/).

## Comparing capabilities

`PrinterCapabilities` is a record, but records compare lists by reference, so two capabilities read separately aren't equal even when they describe the same printer. Compare the lists' contents instead, and compare paper sizes by `Name`.

The code on this page is in [ReadingCapabilities.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/ReadingCapabilities.cs).
