---
title: Printer details, identifying a printer and test pages
description: Read a printer's web page, icons and firmware, ask it to flash or beep so it can be found, and print Latiprimere's test page.
sidebar:
  order: 3
---

Beyond printing, a printer can say more about itself, make itself known so someone can find it, and print a page that shows whether it prints what it is sent. All three work through `IPrinter`, the same on every print system that can do them.

## Read the printer's details

`IPrinter.GetDetailsAsync()` reads what the printer reports about itself: its web page, a page for its supplies, pictures of it, its firmware, and how it can be identified.

```csharp
static async Task ShowDetailsAsync(IPrinter printer)
{
	PrinterDetails details = await printer.GetDetailsAsync();

	Console.WriteLine($"Web page:      {details.WebPage?.ToString() ?? "not reported"}");
	Console.WriteLine($"Supplies page: {details.SuppliesWebPage?.ToString() ?? "not reported"}");
	Console.WriteLine($"Icons:         {string.Join(", ", details.Icons)}");
	Console.WriteLine($"Firmware:      {string.Join(", ", details.Firmware.Select(part => $"{part.Name} {part.Version}"))}");
	Console.WriteLine($"Identify by:   {string.Join(", ", details.IdentifyActions)}");
}
```

| Property          | What it holds                                                                 |
| ----------------- | ----------------------------------------------------------------------------- |
| `WebPage`         | The printer's own web page, usually where it is set up; for a CUPS queue, its page on the CUPS server |
| `SuppliesWebPage` | Where its ink or toner is shown or ordered                                    |
| `Icons`           | Pictures of the printer, smallest first: 48, 128 and 512 pixels, as PNG       |
| `Firmware`        | Each part of its firmware, as `PrinterFirmware` (a `Name` and a `Version`)    |
| `IdentifyActions` | How it can make itself known: `Display`, `Flash`, `Sound`, `Speak`            |

As with capabilities, an empty list or a `null` means the print system didn't say. IPP Everywhere printers, and CUPS queues for them, report most of these. The Windows spooler reports none, so a Windows queue has them only from its IPP printer, with `WindowsOptions.PrintPdfOverIpp` on, as [PDFs straight to the printer over IPP](/docs/print-systems/cups-and-windows/#pdfs-straight-to-the-printer-over-ipp) describes.

:::caution[The addresses are the printer's say-so]
Only `http` and `https` addresses are given, but they come from the printer. If your application fetches an icon, expect it from the printer's own host, and trust it no more than you trust the printer.
:::

## Ask a printer to identify itself

`IPrinter.IdentifyAsync()` asks the printer to flash, beep, show a message or speak, as it does by default, so whoever stands among several printers can tell which one it is. A message is shown on printers with a display, and may be at most 127 bytes in UTF-8.

```csharp
static async Task IdentifyAsync(IPrinter printer)
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
```

Network printers and CUPS queues are asked with IPP's Identify-Printer, which IPP Everywhere printers take. A printer that doesn't take it, and a Windows queue without `PrintPdfOverIpp`, throw `NotSupportedException`. Check `IdentifyActions` in the details first to show an "Identify" button only for printers that can.

## Print a test page

`IPrinter.PrintTestPageAsync()` prints a page Latiprimere draws. It shows:

- the printer's name, ID, make and model, location, print system and address, the paper, when it was printed and Latiprimere's version;
- what the printer's capabilities say: paper sizes, two-sided modes, colour modes, qualities, trays and formats;
- **a line a quarter of an inch (6.35 mm) in from each edge.** A side that doesn't print is outside what the printer can print;
- **two rulers, 100 mm and 4 inches.** Shorter than they say means the page was scaled;
- **colour patches** in cyan, magenta, yellow, black, red, green and blue, and a grey ramp from white to black in 10% steps.

```csharp
static async Task PrintTestPageAsync(IPrinter printer)
{
	IPrintJob job = await printer.PrintTestPageAsync(PrintOptions.Default);

	await foreach (PrintJobStatus status in job.WatchStatusAsync())
	{
		Console.WriteLine($"Test page: {status.State}");
	}
}
```

The page goes on the job's `MediaSize`, else the printer's default paper. On paper too small for it, such as a 4 × 6 inch label, it is shrunk to fit and says so. It takes the same options as any job, and returns its job to follow or cancel.

It is a PDF, printed as any PDF is, so it needs what a PDF needs: on Windows, a PDF rasteriser such as Latiprimere.Rendering.Pdfium, unless the queue's IPP printer takes the page; and for a network printer that takes only raster, the same. Without one, it fails with `NotSupportedException`, as a PDF job would.

The code on this page is in [PrinterDetailsAndTestPages.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/PrinterDetailsAndTestPages.cs).
