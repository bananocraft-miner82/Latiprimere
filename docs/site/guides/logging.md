---
title: Logging
description: See what Latiprimere does through Microsoft.Extensions.Logging, choose how much with categories and levels, and know what each message means.
sidebar:
  order: 7
---

Latiprimere logs through `Microsoft.Extensions.Logging`, the logging every .NET host uses. It logs what it carries on without: a print system it skipped, a printer whose vendor options couldn't be read, a PDF sent as raster, an address store it couldn't write. Failures it can't get past are thrown as exceptions instead, not logged: see [Errors and troubleshooting](/docs/guides/errors-and-troubleshooting/).

## Turn it on

Register logging in the same service collection as Latiprimere. This writes to the console, with the `Microsoft.Extensions.Logging.Console` package, everything Latiprimere logs from Debug up, and only warnings from everything else:

```csharp
ServiceCollection services = new ServiceCollection();

services.AddLogging
(
	logging => logging
		.AddConsole()
		.SetMinimumLevel(LogLevel.Warning)
		.AddFilter("Latiprimere", LogLevel.Debug) // Every category that starts with "Latiprimere"
);

services.AddLatiprimere();
services.AddLatiprimereCups();
services.AddLatiprimereWindows();
services.AddLatiprimerePdfium();
services.AddLatiprimereIpp();
```

Listing printers then starts with:

```text
dbug: Latiprimere.PrinterCatalog[1403631096]
      Print system CUPS is available.
dbug: Latiprimere.PrinterCatalog[1403631096]
      Print system Windows is available.
dbug: Latiprimere.PrinterCatalog[1403631096]
      Print system IPP is available.
```

ASP.NET Core and the Generic Host set logging up already, so the print systems use it with no change. Choose the levels in `appsettings.json`, as for any other library:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Latiprimere": "Debug"
    }
  }
}
```

Filter by category, as above, rather than by event ID: the IDs aren't part of the API.

## Without dependency injection

Pass a logger factory to each print system's factory, and a logger to the catalog. Each factory has an overload that takes an `ILoggerFactory`; the others log nothing.

```csharp
using ILoggerFactory loggerFactory = LoggerFactory.Create(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));

// Each print system logs through the logger factory it is given
using IPrintSystem cups = CupsPrintSystemFactory.Create(options: null, loggerFactory);
using IPrintSystem windows = WindowsPrintSystemFactory.Create(new PdfiumRasterizer(), options: null, loggerFactory);
using IPrintSystem network = IppNetworkPrintSystemFactory.Create(new PdfiumRasterizer(), options: null, loggerFactory);

// And the catalog through its logger
PrinterCatalog catalog = new PrinterCatalog([cups, windows, network], loggerFactory.CreateLogger<PrinterCatalog>());
```

## Categories

| Category                     | What logs there                                                                 |
| ---------------------------- | ------------------------------------------------------------------------------- |
| `Latiprimere.PrinterCatalog` | The catalog: which print systems it uses, and any it skipped                    |
| `Latiprimere.Ipp`            | Network printers over IPP, CUPS, and Windows queues whose PDFs go straight to their printer over IPP |
| `Latiprimere.Windows`        | The Windows spooler and its drivers, and finding a Windows queue's printer over IPP |

## What each message means

Nothing Latiprimere logs is above Warning: an error is thrown. Messages carry printer names, printer URIs and file paths, and an exception when there is one. They never carry passwords or the documents' content.

### `Latiprimere.PrinterCatalog`

| Level   | Message                                                     | What to do                                       |
| ------- | ----------------------------------------------------------- | ------------------------------------------------ |
| Debug   | Print system *name* is available.                           | Nothing: one line for each print system registered and supported here |
| Warning | No supported print system is available on this platform.    | Register the print system for this platform; see [Installation](/docs/getting-started/installation/) |
| Warning | Print system *name* failed and was skipped.                 | The exception says why; the other print systems' printers are still listed |

### `Latiprimere.Ipp`

| Level       | Message                                                                                   | What to do |
| ----------- | ----------------------------------------------------------------------------------------- | ---------- |
| Debug       | *URI* doesn't take PDF, so the PDF is sent as *format*, *colour space* at *dpi* dpi, gzipped: *yes or no*. | Nothing: see [Printers that don't take PDF](/docs/print-systems/network-printers/#printers-that-dont-take-pdf) |
| Debug       | *URI* couldn't validate the job's attributes now, so the job is sent without that check. | Nothing: the printer answered with a server error, such as busy, and checks the job itself when it gets it |
| Information | The encrypted connection to *IPPS URI* couldn't be opened, because its certificate isn't trusted or nothing answered there, so *IPP URI* is used instead. | To use IPPS, pin its certificate; see [Secure network printers](/docs/print-systems/secure-printers/) |
| Warning     | The vendor options of *URI* couldn't be read, so its capabilities list none.             | The exception says why; the typed capabilities are still read |
| Debug       | Read *count* remembered printer addresses from *path*.                                    | Nothing |
| Debug       | There is no address store at *path* yet, so no printer addresses are remembered.         | Nothing: it's written when a printer is first found |
| Warning     | The address store at *path* couldn't be read, so no printer addresses are remembered; it is written afresh at the next change. | Nothing, unless it keeps happening: the exception says why |
| Warning     | The address store at *path* is in format *version*, later than the *version* this version of Latiprimere reads, so no printer addresses are remembered and the file is left as it is. | A later version of Latiprimere shares the file: give each its own `AddressStorePath` |
| Warning     | The address store at *path* couldn't be written; the next change tries again.            | Check the application can write to the folder |

### `Latiprimere.Windows`

| Level       | Message                                                                                   | What to do |
| ----------- | ----------------------------------------------------------------------------------------- | ---------- |
| Information | The Print Capabilities of '*printer*' couldn't be read, so its capabilities list no vendor options. | Nothing: an older driver without Print Ticket support. It isn't asked again |
| Warning     | The driver of '*printer*' gave Print Capabilities or a default Print Ticket that can't be read, so its vendor options may be missing or have no defaults. | The exception says what's wrong with the driver's document |
| Debug       | The WSD queue '*printer*' is the printer at *URI*, by its UUID, so its PDF jobs go there over IPP. | Nothing: see [PDFs straight to the printer over IPP](/docs/print-systems/cups-and-windows/#pdfs-straight-to-the-printer-over-ipp) |
| Information | The printer at *URI* doesn't report the UUID the WSD queue '*printer*' records, so the job goes through the queue. | Nothing: the queue's driver prints it, and the next job looks again |
| Information | Searching the network for a WSD queue's IPP printer failed, so its jobs go through the queue. | Nothing, unless you expected it to be found: the exception says why, often multicast blocked by a firewall |
| Information | The PDF can't be sent as raster to the IPP printer of '*printer*', so the job goes through the queue. | Nothing: the exception says what the printer lacks, such as a paper size |

The code on this page is in [Logging.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/Logging.cs). Run it with `dotnet run --project samples/Latiprimere.Examples -f net10.0 -- logging`.
