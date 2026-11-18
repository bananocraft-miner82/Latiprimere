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
| `MaxCopies`                          | The most copies one job can ask for                  |
| `VendorOptions`                      | Everything else the printer offers, as `VendorOption`: see below |

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

- **CUPS and network printers** get them as IPP job attributes. A printer that can't do one may print on something close instead, or refuse the job with a `PrintException`.
- **Windows** checks a size or tray against the driver's own lists, and refuses one the driver doesn't list with `NotSupportedException` before anything is queued, since the driver would otherwise print on its default paper without saying so. Monochrome pages are made grey before the driver gets them, as some drivers, Microsoft Print to PDF among them, ignore the setting.
- **Raw jobs** don't use them: the data is sent unchanged, so the printer's own language chooses paper and colour. See [Labels and receipts](/docs/guides/labels-and-receipts/).

## Vendor options: everything else the printer offers

Printers offer far more than the typed options cover: print quality, resolution, finishing such as stapling, media types, pages per sheet, and settings a driver invents for itself. `VendorOptions` lists them, each with the key and values to pass back through `PrintOptions.VendorOptions`:

```csharp
static async Task ShowVendorOptionsAsync(IPrinter printer)
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
```

For an HP ENVY inkjet through its Windows driver, this prints, among others:

```text
windows:feature:psk:PageOutputQuality (Output Quality): psk:High (Best), psk:Normal (Normal), psk:Draft (Fast Draft); default psk:Normal
windows:feature:psk:DocumentNUp (Pages per sheet): psk:PagesPerSheet=1 (1), psk:PagesPerSheet=2 (2), psk:PagesPerSheet=4 (4), …; default psk:PagesPerSheet=1
windows:feature:psk:PageMediaType (Media Type): ns0000:_0_1004_4770 (Plain Paper), ns0000:_2_1005_0770 (HP Premium Paper), …; default ns0000:_0_1004_4770
```

and for the same printer over IPP:

```text
ipp:print-quality (Print quality): 3 (Draft), 4 (Normal), 5 (High); default 4
ipp:printer-resolution (Resolution): 300dpi (300dpi), 600dpi (600dpi), 1200dpi (1200dpi); default 600dpi
ipp:print-content-optimize (Optimise for): auto (Auto), photo (Photo), graphics (Graphics), text (Text), text-and-graphics (Text and graphics); default auto
```

Each `VendorOption` has:

| Property                          | What it holds                                                                 |
| --------------------------------- | ----------------------------------------------------------------------------- |
| `Key`                             | The key to set in `PrintOptions.VendorOptions`                               |
| `DisplayName`                     | A name to show people, from the driver where it has one                       |
| `Kind`                            | `Choice`, `Number` or `Text`                                                  |
| `Choices`                         | For a choice: each `Value` to set, with a `DisplayName`                       |
| `DefaultValue`                    | What the printer uses when a job doesn't say                                  |
| `MinimumValue`, `MaximumValue`    | For a number: its limits, where the printer gives them                        |
| `AllowsSeveralValues`             | Whether several choices can be set at once, separated by commas, as for IPP `finishings` |

Pass a listed value back to print with it. This asks for the best print quality, on whichever print system the printer is on:

```csharp
static async Task<PrintOptions> ChooseBestQualityAsync(IPrinter printer)
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
```

Where they come from:

- **CUPS and network printers** list the job attributes they accept (`job-creation-attributes-supported`) with their supported values, as `ipp:` keys.
- **Windows** lists the driver's Print Capabilities: its features as `windows:feature:` keys, and its parameters as `windows:parameter:` keys. A feature's name and its options use the prefixes the driver's document declares, such as `psk:` for the Print Schema's own keywords and `ns0000:` for the driver's. An option without a name is identified by its properties, as in `psk:PagesPerSheet=4`.
  - **When printed,** each key is checked against the printer's Print Capabilities, for the job's own settings (its `windows:devmode` and `windows:print-ticket`, if it has them). A feature, option or value the printer doesn't offer is refused with `ArgumentException` before anything is queued, and the message lists what it does offer.
  - **Options the device or an administrator rules out** aren't listed, and are refused if asked for, since the driver would only print with another one instead.
  - **Secrets aren't listed,** such as Microsoft Print to PDF's PDF passwords. The driver expects them encrypted, and a password in plain text would sit in the job's settings and in the spooler's files. An application that knows how to encrypt them can still set them by key.

Settings the typed options cover, such as paper size, duplex, colour, trays, copies and orientation, aren't listed: use the typed options for them. On Windows, a key for one of them is refused when the job sets the typed option too; copies and orientation are always set, so their keys are always refused. Vendor options aren't portable: a key only means something to the print system it starts with, and its values often only to the printer that listed them.

## Windows: Print Capabilities and Print Tickets

Every `windows:` printer is an `IWindowsPrinter`, which reads the driver's own documents in Microsoft's Print Schema:

- `GetPrintCapabilitiesAsync()` returns the driver's Print Capabilities: every feature, option and parameter, with the driver's names for them. Pass a Print Ticket to see the options that remain once its choices are made, such as the media types one tray takes.
- `GetDefaultPrintTicketAsync()` returns the driver's defaults as a Print Ticket: a starting point for writing one.

```csharp
static async Task SavePrintCapabilitiesAsync(IPrinter printer, string folder)
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
```

A Print Ticket you write goes to a job as the vendor option `windows:print-ticket`. The driver merges it onto its defaults, or onto a saved `windows:devmode`, then the `windows:feature:` and `windows:parameter:` keys, then the typed options, which win. A ticket can set anything the driver supports, nested features included, which the vendor option keys can't.

## Comparing capabilities

`PrinterCapabilities` is a record, but records compare lists by reference, so two capabilities read separately aren't equal even when they describe the same printer. Compare the lists' contents instead, and compare paper sizes by `Name`.

The code on this page is in [ReadingCapabilities.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/ReadingCapabilities.cs).
