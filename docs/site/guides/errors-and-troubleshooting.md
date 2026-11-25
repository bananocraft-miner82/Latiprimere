---
title: Errors and troubleshooting
description: What each Latiprimere exception means, how to log what the print systems are doing, and fixes for common problems.
sidebar:
  order: 6
---

Every Latiprimere exception derives from `PrintException`, so one `catch` handles them all. Catch the specific ones first when you want to respond differently.

## Exceptions

| Exception                         | Thrown when                                                                  | What to do                                       |
| --------------------------------- | ---------------------------------------------------------------------------- | ------------------------------------------------ |
| `PrinterNotFoundException`        | No print system knows the printer ID. `PrinterId` holds the ID.              | Let the user choose another printer.             |
| `PrintAuthenticationException`    | A CUPS server or network printer needs a user name and password, or refused the ones given; or a network printer asks for one over plain IPP, where it's never sent. | Ask for credentials and create the print system again; for a network printer, use its `ipps://` address. |
| `PrintCertificateException`       | A network printer's `ipps://` address presented a certificate that isn't trusted. `CertificateFingerprint` and `PrinterUri` say which. | Check the fingerprint against the printer's web page, then pin it. |
| `PrintSystemUnavailableException` | No registered print system works on this machine, or its service isn't running. | Register the right print system, or start its service. |
| `PrintException`                  | Anything else the print system reports: a rejected job, a printer that can't be reached, a timeout. | Show the message; `NativeErrorCode` helps diagnose it. |
| `NotSupportedException`           | The printer can't take the document or option, such as raw data on a virtual printer, or a PDF on a text-only printer. | Change what you send.                           |
| `ArgumentException`               | An option the print system refuses, such as a vendor option the printer doesn't offer, or settings that can't work, such as a malformed certificate fingerprint, when the print system is registered. | Fix the option or setting: the message says which. |

`NotSupportedException` and `ArgumentException` are .NET's own, not `PrintException`s, because they mean the request itself can't work, rather than that printing failed.

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

The catalog logs which print systems it uses and which it skipped, with the reason, and the print systems log what they carry on without, such as a PDF sent as raster or vendor options that couldn't be read. ASP.NET Core and the Generic Host set logging up already, so Latiprimere uses it without any change. [Logging](/docs/guides/logging/) shows how to turn it on elsewhere, and what each message means.

## Common problems

**"No supported print system is available on this platform."**
No print system you registered works here. Check that you registered the one for this platform: `AddLatiprimereWindows()` on Windows, `AddLatiprimereCups()` on Linux and macOS. On Windows, check that the Print Spooler service is running.

**"Printing PDF documents on Windows needs a PDF rasteriser."**
Add the `Latiprimere.Rendering.Pdfium` package and call `AddLatiprimerePdfium()`.

**CUPS printers are missing on Linux or macOS.**
Check that the CUPS scheduler is running: `lpstat -r` should say so. In a container, CUPS usually isn't installed; point Latiprimere at a CUPS server with `AddLatiprimereCups(options => options.Server = "host:631")`, or print to network printers directly.

**A network printer isn't found.**
Discovery only sees printers on the same part of the network, and needs multicast DNS (UDP port 5353) to get through firewalls. Add the printer by its address instead: see [Network printers over IPP](/docs/print-systems/network-printers/). A printer that sleeps may not answer the search: `AddressStorePath` remembers where each printer was last seen.

**A network printer's `ipps://` address "presented a certificate that isn't trusted".**
Printers make their own certificates, which no computer trusts. Check the fingerprint, in the message and in the `PrintCertificateException`'s `CertificateFingerprint`, against the printer's web page, then pin it: see [Secure network printers](/docs/print-systems/secure-printers/).

**A network printer "stopped taking the document" or "did not answer".**
The printer took no more of the job, or didn't answer once it had been sent it all, for `UploadStallTimeout`, five minutes by default: it may have jammed, run out of paper with nowhere to keep the job, or dropped off the network. Check the printer. If it "did not answer", it may still print the job, so look before printing it again. A printer that reads slowly but steadily is never cut short; raise the limit only for one that pauses longer between pages: see [Network printers over IPP](/docs/print-systems/network-printers/).

**Microsoft Print to PDF fails with "The printer driver's request was cancelled".**
The driver wanted to show a Save dialog, which an app without a user interface can't. Set `PrintOptions.OutputFile`: see [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/).

**Following a job never ends.**
The print system isn't reporting the job's state, so it stays `Unknown`. Always follow jobs with a timeout: see [Print options and following jobs](/docs/guides/print-options-and-jobs/).

## Should you retry?

- **Don't retry** `PrinterNotFoundException`, `PrintAuthenticationException`, `PrintCertificateException` or `NotSupportedException`: the same request fails the same way.
- **A `PrintException`** for a printer or server that couldn't be reached may succeed later, once the printer is back.
- **Busy network printers** are retried for you, for two minutes by default.
- **A network printer that stopped taking a job** isn't retried for you, as some of it may already be on paper.

The code on this page is in [HandlingErrors.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/HandlingErrors.cs).
