---
title: Vendor options and Windows Print Capabilities
description: Set print quality, resolution, finishing and a driver's own settings with vendor options, list the ones a printer offers, and use Windows Print Capabilities and Print Tickets.
sidebar:
  order: 3
---

The typed options in `PrintOptions` cover what every printer has: copies, sides, orientation, paper, colour and trays. Printers offer far more: print quality, resolution, finishing such as stapling, media types, pages per sheet, and settings a driver invents for itself. Vendor options reach those, through `PrintOptions.VendorOptions`, and a printer's capabilities list the ones it takes.

## Keys

Each key starts with the print system that reads it, and each print system ignores the others' keys, so one set of options can go to any printer:

| Key                                  | Read by                         | Example                                              |
| ------------------------------------ | ------------------------------- | ---------------------------------------------------- |
| `ipp:<attribute>`                    | CUPS and network printers       | `ipp:print-quality` = `5`, `ipp:printer-resolution` = `1200dpi` |
| `cups:<option>`                      | CUPS                            | `cups:fit-to-page` = `true`                          |
| `windows:feature:<feature>`          | Windows                         | `windows:feature:psk:PageOutputQuality` = `psk:High` |
| `windows:parameter:<parameter>`      | Windows                         | `windows:parameter:ns0000:JobDarkness` = `2`         |
| `windows:print-ticket`               | Windows                         | A Print Ticket, as XML                               |
| `windows:devmode`                    | Windows                         | A DEVMODE saved from the driver, in base64           |

- **Several values** go in one string, separated by commas, for an option that takes several at once, such as IPP's `finishings`.
- **Typed options win.** A key that sets something a typed option sets for the same job, such as `ipp:copies` with `Copies`, is refused with `ArgumentException`. On Windows, copies and orientation are always set, so their keys are always refused.
- **Raw jobs** don't use the `windows:` keys, since the data goes to the printer unchanged.
- **They aren't portable:** a key only means something to the print system it starts with, and its values often only to the printer that listed them. Read them from the printer, as below, rather than writing them into your code for every printer.

## List the options a printer offers

`PrinterCapabilities.VendorOptions` lists them, each with the key and values to pass back:

```csharp
static async Task ShowVendorOptionsAsync(IPrinter printer)
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
| `MinValue`, `MaxValue`    | For a number: its limits, where the printer gives them                        |
| `AllowsSeveralValues`             | Whether several choices can be set at once, separated by commas, as for IPP `finishings` |

Settings the typed options cover, such as paper size, duplex, colour, trays, copies and orientation, aren't listed: use the typed options for them.

## Print with one

Pass a listed value back. This asks for the best print quality, on whichever print system the printer is on:

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

`PrintOptions` is a record, so the result can be combined with the typed options: `options with { Copies = 2 }`.

## Where they come from

- **CUPS and network printers** list the job attributes they accept (`job-creation-attributes-supported`) with their supported values, as `ipp:` keys. They're sent as IPP job attributes. A printer that can't do one may print something close instead, or refuse the job with a `PrintException` naming the setting; a network printer that lists Validate-Job refuses it before the document is sent.
- **Windows** lists the driver's Print Capabilities: its features as `windows:feature:` keys, and its parameters as `windows:parameter:` keys. A feature's name and its options use the prefixes the driver's document declares, such as `psk:` for the Print Schema's own keywords and `ns0000:` for the driver's. An option without a name is identified by its properties, as in `psk:PagesPerSheet=4`.
  - **When printed,** each key is checked against the printer's Print Capabilities, for the job's own settings (its `windows:devmode` and `windows:print-ticket`, if it has them). A feature, option or value the printer doesn't offer is refused with `ArgumentException` before anything is queued, and the message lists what it does offer.
  - **Options the device or an administrator rules out** aren't listed, and are refused if asked for, since the driver would only print with another one instead.
  - **Secrets aren't listed,** such as Microsoft Print to PDF's PDF passwords. The driver expects them encrypted, and a password in plain text would sit in the job's settings and in the spooler's files. An application that knows how to encrypt them can still set them by key.

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

A Print Ticket you write goes to a job as the vendor option `windows:print-ticket`. A ticket can set anything the driver supports, nested features included, which the `windows:feature:` keys can't.

A job's settings are built up in this order, each step overriding the one before:

1. the driver's defaults, or a saved `windows:devmode`;
2. the `windows:print-ticket`;
3. the `windows:feature:` and `windows:parameter:` keys;
4. the typed options, which always win.

A `windows:devmode` is a whole DEVMODE, the driver's binary settings, saved from the driver, for example from an application's own print dialog, and given in base64. It's checked to be a DEVMODE whose sizes add up, then passed to the driver, which drops or adjusts anything it can't do. Prefer a Print Ticket where you can: it's readable, and the same ticket works with any driver that knows its features.

The code on this page is in [VendorOptions.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/VendorOptions.cs). Run it against a printer with `dotnet run --project samples/Latiprimere.Examples -f net10.0 -- vendor-options <printer-id>`.
