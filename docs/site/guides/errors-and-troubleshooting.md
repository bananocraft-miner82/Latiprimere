---
title: Errors and troubleshooting
description: What each Latiprimere exception means, how to log what the print systems are doing, and fixes for common problems.
sidebar:
  order: 4
---

Every Latiprimere exception derives from `PrintException`, so one `catch` handles them all. Catch the specific ones first when you want to respond differently.

## Exceptions

| Exception                         | Thrown when                                                                  | What to do                                       |
| --------------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------ |
| `PrinterNotFoundException`        | No print system knows the printer ID. `PrinterId` holds the ID.              | Let the user choose another printer.             |
| `PrintAuthenticationException`    | A print server needs a user name and password, or refused the ones given.    | Ask for credentials and create the print system again. |
| `PrintSystemUnavailableException` | No registered print system works on this machine, or its service isn't running. | Register the right print system, or start its service. |
| `PrintException`                  | Anything else the print system reports: a rejected job, a printer that can't be reached, a timeout. | Show the message; `NativeErrorCode` helps diagnose it. |
| `NotSupportedException`           | The printer can't take the document or option, such as raw data on a virtual printer. | Change what you send.                           |

`NotSupportedException` is .NET's own, not a `PrintException`, because it means the request itself can't work, rather than that printing failed.

`NativeErrorCode` holds the print system's own error code when there is one: a Windows error number, an IPP status code, or an HTTP status such as 401.

## Handle each failure

```csharp
static async Task<bool> TryPrintAsync(IPrinterCatalog catalog, string printerId, Stream document)
{
	try
	{
		IPrinter printer = await catalog.GetPrinterAsync(printerId);
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default);

		Console.WriteLine($"Sent as job {job.Id}");

		return true;
	}
	catch (PrinterNotFoundException exception)
	{
		Console.WriteLine($"There's no printer with the ID {exception.PrinterId}.");
	}
	catch (PrintAuthenticationException)
	{
		Console.WriteLine("The print server needs a user name and password.");
	}
	catch (PrintSystemUnavailableException exception)
	{
		Console.WriteLine($"Printing isn't available: {exception.Message}");
	}
	catch (NotSupportedException exception)
	{
		Console.WriteLine($"This printer can't print that: {exception.Message}");
	}
	catch (PrintException exception)
	{
		Console.WriteLine($"Printing failed: {exception.Message} (error code {exception.NativeErrorCode})");
	}

	return false;
}
```

The messages are written for people, so you can show them as they are.

## When one print system fails

The catalog keeps going when one print system fails. While listing printers or finding the default, it skips a print system that fails and logs a warning, so one unreachable CUPS server doesn't hide your Windows printers. It throws only if every print system fails: the single failure as it was, or a `PrintException` wrapping them all.

Looking a printer up by ID is different. The ID belongs to one print system, so its failure is thrown.

## See what's happening

The catalog logs which print systems it uses and which it skipped, with the reason. Register logging to see it. This uses the console logger from the `Microsoft.Extensions.Logging.Console` package:

```csharp
ServiceCollection services = new ServiceCollection();

services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Debug));
services.AddLatiprimere();
services.AddLatiprimereCups();
services.AddLatiprimereWindows();
services.AddLatiprimerePdfium();
```

ASP.NET Core and the Generic Host set up logging already; the catalog uses it without any change.

## Common problems

**"No supported print system is available on this platform."**
No print system you registered works here. Check that you registered the one for this platform: `AddLatiprimereWindows()` on Windows, `AddLatiprimereCups()` on Linux and macOS. On Windows, check that the Print Spooler service is running.

**"Printing PDF documents on Windows needs a PDF rasteriser."**
Add the `Latiprimere.Rendering.Pdfium` package and call `AddLatiprimerePdfium()`.

**CUPS printers are missing on Linux or macOS.**
Check that the CUPS scheduler is running: `lpstat -r` should say so. In a container, CUPS usually isn't installed; point Latiprimere at a CUPS server with `AddLatiprimereCups(options => options.Server = "host:631")`, or print to network printers directly.

**A network printer isn't found.**
Discovery only sees printers on the same part of the network, and needs multicast DNS (UDP port 5353) to get through firewalls. Add the printer by its address instead: see [Network printers over IPP](/docs/print-systems/network-printers/).

**Microsoft Print to PDF fails with "The printer driver's request was cancelled".**
The driver wanted to show a Save dialog, which an app without a user interface can't. Set `PrintOptions.OutputFile`: see [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/).

**Following a job never ends.**
The print system isn't reporting the job's state, so it stays `Unknown`. Always follow jobs with a timeout: see [Print options and following jobs](/docs/guides/print-options-and-jobs/).

## Should you retry?

- **Don't retry** `PrinterNotFoundException`, `PrintAuthenticationException` or `NotSupportedException`: the same request fails the same way.
- **A `PrintException`** for a printer or server that couldn't be reached may succeed later, once the printer is back.
- **Busy network printers** are retried for you, for two minutes by default.

The code on this page is in [HandlingErrors.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/HandlingErrors.cs).
