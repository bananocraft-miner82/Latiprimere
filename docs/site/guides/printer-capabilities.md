---
title: Reading printer capabilities
description: Read a printer's paper sizes, duplex and colour modes, trays and copy limit, and choose options it can carry out.
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
| `MaxCopies`                          | The most copies one job can ask for                  |

A `MediaSize` has a `DisplayName` to show people, a `Name` that is the print system's own name for it, and its size in `WidthMillimetres` and `HeightMillimetres`.

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

In 0.1.0-preview you can **read** a printer's paper sizes, trays and colour modes, but `PrintOptions` can't choose them yet. Jobs use the printer's defaults, which you can see in `DefaultMediaSize` and `DefaultColorMode`. With raw printing, the printer's own language chooses them: see [Labels and receipts](/docs/guides/labels-and-receipts/).

## Comparing capabilities

`PrinterCapabilities` is a record, but records compare lists by reference, so two capabilities read separately aren't equal even when they describe the same printer. Compare the lists' contents instead, and compare paper sizes by `Name`.

The code on this page is in [ReadingCapabilities.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/ReadingCapabilities.cs).
