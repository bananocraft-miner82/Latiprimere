---
title: Finding printers
description: List printers across every print system, understand printer IDs, and find a saved printer again.
sidebar:
  order: 3
---

The printer catalog, `IPrinterCatalog`, combines the printers of every print system you registered into one list. You find printers through it in three ways:

| Method                    | Returns                                                       |
| ------------------------- | ------------------------------------------------------------- |
| `GetPrintersAsync()`      | Every printer, as `PrinterInfo` descriptions                  |
| `GetDefaultPrinterAsync()` | The default printer, or `null` if there isn't one           |
| `GetPrinterAsync(id)`     | The printer with that ID; throws `PrinterNotFoundException` if there isn't one |

## List printers

```csharp
static async Task ListAsync(IPrinterCatalog catalog)
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
```

On a Windows machine this prints something like:

```text
* Microsoft Print to PDF
    ID:       windows:Microsoft Print to PDF
    State:    Idle
    Model:    Microsoft Print To PDF
    Location: not reported
  Office Laser
    ID:       windows:Office Laser
    State:    Stopped
    Model:    HP LaserJet Pro M404
    Location: Second floor
```

Each `PrinterInfo` describes the printer as it was when listed:

| Property       | What it holds                                                                 |
| -------------- | ----------------------------------------------------------------------------- |
| `Id`           | The ID to look the printer up with later                                      |
| `Name`         | The name to show people                                                       |
| `State`        | `Idle`, `Processing`, `Stopped` or `Unknown`                                  |
| `IsDefault`    | Whether it is the default printer of its print system                         |
| `MakeAndModel`, `Location`, `Description` | Extra details, when the print system reports them  |

The catalog asks every print system at once, so listing takes as long as the slowest one. With network discovery registered, that's about two seconds, the time it listens for printers announcing themselves. If one print system fails, for example because CUPS isn't running, its printers are left out and a warning is logged; listing fails only if every print system does.

## Printer IDs

Every printer has an ID, unique across the catalog. It starts with the print system's name:

| Print system                    | ID                                       |
| ------------------------------- | ---------------------------------------- |
| Windows spooler                 | `windows:Office Laser`                   |
| CUPS                            | `cups:Office_Laser`                      |
| Network printer found by discovery | `ipp:dnssd:Office Laser`              |
| Network printer by address      | `ipp:ipp://192.168.1.20:631/ipp/print`   |

Save the ID when a user chooses a printer, and look it up again next time, even after a restart. An ID stays the same for as long as the printer keeps its name in its print system. Don't parse IDs or build them yourself: the one exception is the address form for a network printer you configured, described in [Network printers over IPP](/docs/print-systems/network-printers/).

## Find a saved printer again

`GetPrinterAsync` reads the printer's current details and throws `PrinterNotFoundException` if it has gone. A common pattern is to fall back to the default printer:

```csharp
static async Task<IPrinter?> FindSavedPrinterAsync(IPrinterCatalog catalog, string savedPrinterId)
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
```

The returned `IPrinter` is what you print with. Its `Info` property holds the printer's details as read by the lookup. An `IPrinter` is safe to use from several threads at once.

## The default printer

`GetDefaultPrinterAsync` asks each print system in the order you registered them, and returns the first default printer it finds. On Windows that's the spooler's default printer; with CUPS it's the server's default. It returns `null` when no print system has one, which is common on servers and in containers.

The code on this page is in [FindingPrinters.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/FindingPrinters.cs).
