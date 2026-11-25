---
title: Print options and following jobs
description: Choose copies, pages, two-sided printing, quality and paper, print images, text and password-protected PDFs, follow a job or a printer, cancel a job, and find it again after a restart.
sidebar:
  order: 1
---

`SubmitAsync` takes the document and a `PrintOptions`. Options left at their defaults use the printer's own settings, so `PrintOptions.Default` prints one copy the way the printer is set up.

## Print options

| Option        | Values                                                     | Default                        |
| ------------- | ---------------------------------------------------------- | ------------------------------ |
| `JobName`     | The name shown in print queues                             | Chosen by the print system     |
| `Copies`      | 1 or more                                                  | 1                              |
| `Duplex`      | `OneSided`, `LongEdge` or `ShortEdge`                      | `PrinterDefault`               |
| `Orientation` | `Portrait` or `Landscape`                                  | `PrinterDefault`               |
| `MediaSize`   | One of the printer's paper sizes                           | `null`: the printer's default  |
| `ColorMode`   | `Color` or `Monochrome`                                    | `PrinterDefault`               |
| `Scaling`     | `Fit`, `Fill` or `None`, for images                        | `PrinterDefault`               |
| `InputTray`   | One of the printer's trays                                 | `null`: the printer's default  |
| `MediaType`   | One of the printer's kinds of paper, such as labels        | `null`: the printer's default  |
| `PageRanges`  | Pages to print, such as `[new PageRange(2, 5)]`            | Empty: every page              |
| `Collate`     | `true` for whole copies in turn, `false` for each page's copies together | `null`: the printer's default |
| `Quality`     | `Draft`, `Normal` or `High`                                | `PrinterDefault`               |
| `Finishings`  | Staples, holes or a fold, as `Finishing` values            | Empty: the printer's default   |
| `OutputBin`   | One of the printer's output bins                           | `null`: the printer's default  |
| `OutputFile`  | A file to print to instead of the printer (Windows only)   | `null`: print normally         |
| `VendorOptions` | Anything else the printer offers, by key: see below      | Empty                          |

For two-sided printing, `LongEdge` turns pages like a book, and `ShortEdge` flips them like a notepad, which suits landscape pages. Take `MediaSize` and `InputTray` from the printer's capabilities, as [Reading printer capabilities](/docs/guides/printer-capabilities/#paper-size-trays-and-colour) shows: they only mean something to the printer that reported them. `OutputFile` is for virtual printers such as Microsoft Print to PDF: see [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/).

:::note[Orientation and PDFs on CUPS]
On Windows, and wherever Latiprimere renders the pages as raster, `Landscape` turns a portrait page and shrinks it to fit across the paper. CUPS queues and network printers are sent the setting as IPP's `orientation-requested`, with `print-scaling` set to `auto-fit` so that a turned page is shrunk to fit rather than cut off, but what they do with a PDF is up to them: in testing, CUPS 2.4 on Linux kept a portrait PDF portrait. To be sure of landscape, make the PDF's pages landscape. See [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/#turning-pdf-pages).
:::

`PageRanges` counts pages from 1, in order and without overlapping; `new PageRange(3, int.MaxValue)` prints from page 3 to the end. Where Latiprimere renders the pages, on the Windows spooler or as raster, only those are rendered; a printer or CUPS queue sent the PDF as it is is asked for them. Raw data's pages can't be seen, so a raw job with page ranges is refused with `NotSupportedException`.

For labels and receipts, `MediaSize.Custom(width, height)` makes a size in millimetres, which a printer takes when it is inside one of its `CustomMediaSizes`:

```csharp
PrintOptions label = new PrintOptions { MediaSize = MediaSize.Custom(102, 152) };
```

Finishings and output bins aren't applied by the Windows spooler yet: a Windows printer refuses a job that sets them with `NotSupportedException`, rather than printing without them. Its driver's stapling and bins are reached with `windows:feature:` [vendor options](/docs/guides/vendor-options/).

`Scaling` sizes an image on the paper. `Fit` fills the paper within 5 mm margins. `Fill` covers it, cutting off what overhangs. `None` prints the image at its own size, from the resolution it gives, or 96 dpi if it gives none. `PrinterDefault` shrinks an image larger than the paper and leaves a smaller one at its own size. An image is turned as its EXIF orientation says, and printed landscape when it is wider than tall, unless you set `Orientation`. A PNG's transparent parts print as the paper's white.

```csharp
using FileStream photo = File.OpenRead("photo.jpg");

IPrintJob job = await printer.SubmitAsync(PrintContent.FromJpeg(photo), new PrintOptions { Scaling = ScalingMode.Fit });
```

Text, from `PrintContent.FromText`, is set in Courier at 10 points within half-inch margins, on portrait pages unless you set `Orientation` to `Landscape`. Long lines wrap at a space, tabs stop every 8 columns, and a form feed starts a new page. CUPS lays text out its own way.

```csharp
IPrintJob job = await printer.SubmitAsync(PrintContent.FromText("Order 1042\nTotal\t€42.50"), PrintOptions.Default);
```

A password-protected PDF is printed with its password:

```csharp
IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document, password), PrintOptions.Default);
```

The password is never logged. Where Latiprimere renders the PDF, on the Windows spooler or as raster, it opens the PDF with it. A network printer that takes PDF is sent the password with the document if it says it takes one, and otherwise the pages as raster. A CUPS queue, which Latiprimere can't send raster, refuses the job with `NotSupportedException` unless it takes passwords. A wrong password fails the job with a `PrintException` before anything prints.

Setting `Copies` below 1 throws `ArgumentOutOfRangeException`. A printer that doesn't support a setting may ignore it or reject the job, depending on the print system: [Reading printer capabilities](/docs/guides/printer-capabilities/) shows how to check first.

### Vendor options

`VendorOptions` sets what the typed options don't cover, such as resolution, pages per sheet or a driver's own settings, by keys such as `ipp:printer-resolution` = `1200dpi` or `windows:parameter:ns0000:JobDarkness` = `2`. Each key starts with the print system that reads it, and each print system ignores the others' keys. A key that sets something a typed option sets for the same job, such as `ipp:copies` with `Copies`, is refused with `ArgumentException`.

The keys, how to list the ones a printer takes, and Windows Print Tickets are in [Vendor options and Windows Print Capabilities](/docs/guides/vendor-options/).

`PrintOptions` is a record, so you can start from the defaults and change one thing:

```csharp
PrintOptions threeCopies = PrintOptions.Default with { Copies = 3 };
```

## Print with options and follow the job

This prints two double-sided copies in landscape and reports progress, for at most five minutes:

```csharp
static async Task PrintWithOptionsAsync(IPrinter printer, string pdfPath)
{
	PrintOptions options = new PrintOptions
	{
		JobName = "Quarterly report",
		Copies = 2,
		Duplex = DuplexMode.LongEdge,
		Orientation = PageOrientation.Landscape,
	};

	using FileStream document = File.OpenRead(pdfPath);
	IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options);

	using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));

	try
	{
		await foreach (PrintJobStatus status in job.WatchStatusAsync(timeout.Token))
		{
			string pages = status.PagesCompleted is int count ? $", {count} pages printed" : string.Empty;

			Console.WriteLine($"{status.State}{pages} {status.StateMessage}");
		}
	}
	catch (OperationCanceledException) when (timeout.IsCancellationRequested)
	{
		Console.WriteLine("Stopped following the job. It may still print.");
	}
}
```

## Print some of the pages

This prints pages 2 and 3, two copies collated, at draft quality where the printer offers it:

```csharp
static async Task PrintPagesAsync(IPrinter printer, string pdfPath)
{
	PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

	PrintOptions options = new PrintOptions
	{
		PageRanges = [new PageRange(2, 3)],
		Copies = 2,
		Collate = true,
		Quality = capabilities.PrintQualities.Contains(PrintQuality.Draft) ? PrintQuality.Draft : PrintQuality.PrinterDefault,
	};

	using FileStream document = File.OpenRead(pdfPath);
	IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options);

	Console.WriteLine($"Printing pages 2 and 3 as job {job.Id}");
}
```

Collated copies come out as whole copies in turn: 2, 3, 2, 3. With `Collate = false`, each page's copies come out together: 2, 2, 3, 3.

## Job states

`WatchStatusAsync` reports the job's status each time its state, message, reasons or page count changes, never twice the same. It ends after the first final state. The Windows spooler signals each change, so a Windows job is reported as it changes; CUPS and network printers are asked every half second, or at the interval you give.

| State        | Meaning                                                          | Final |
| ------------ | ---------------------------------------------------------------- | ----- |
| `Pending`    | Waiting in the queue                                             |       |
| `Held`       | Held in the queue until someone releases it                      |       |
| `Processing` | Being processed or printed                                       |       |
| `Stopped`    | Stopped part-way, for example because the printer needs attention |      |
| `Completed`  | Printed                                                          | Yes   |
| `Canceled`   | Cancelled                                                        | Yes   |
| `Aborted`    | Ended by the print system because of an error                    | Yes   |
| `Unknown`    | The print system didn't say                                      |       |

Each `PrintJobStatus` also has:

- **`StateMessage`:** the print system's own explanation, such as "Media empty", when it gives one. For a network printer or a CUPS queue that gives none, Latiprimere explains its reason instead, when it knows it: "The job was cancelled at the printer.", or "The printer can't print the job with the options chosen.";
- **`StateReasons`:** why the job is in its state, as IPP `job-state-reasons` keywords such as `job-canceled-at-device`, `aborted-by-system` or `printer-stopped`, for code to act on. Network printers and CUPS give them; the Windows spooler doesn't, except for jobs it sends to a queue's IPP printer, so the list is empty there;
- **`PagesCompleted`:** pages printed so far, or `null` if the print system doesn't count them;
- **`IsTerminal`:** whether the state is final.

:::caution[Always set a timeout]
A job whose state the print system can't report is `Unknown`, which isn't final, so following it would never end. Pass a cancellation token with a timeout, as above. Cancelling it stops *following* the job; the job itself carries on.
:::

To wait for the result rather than show each step, keep the last status. This checks every two seconds instead of the default half second:

```csharp
static async Task<bool> WaitForJobAsync(IPrintJob job, CancellationToken cancellationToken)
{
	PrintJobStatus? finalStatus = null;

	await foreach (PrintJobStatus status in job.WatchStatusAsync(TimeSpan.FromSeconds(2), cancellationToken))
	{
		finalStatus = status;
	}

	return finalStatus?.State == PrintJobState.Completed;
}
```

For a single check, call `job.GetStatusAsync()`.

## Follow a printer

`IPrinter.WatchStatusAsync` reports the printer's state each time it changes, such as when it runs out of paper or is paused, until you cancel it. The Windows spooler signals each change; CUPS and network printers are asked every five seconds, or at the interval you give.

```csharp
static async Task FollowPrinterAsync(IPrinter printer, CancellationToken cancellationToken)
{
	try
	{
		await foreach (PrinterStatus status in printer.WatchStatusAsync(cancellationToken))
		{
			string reasons = status.StateReasons.Count > 0 ? $" ({string.Join(", ", status.StateReasons)})" : string.Empty;

			Console.WriteLine($"{status.State}{reasons} {status.StateMessage}");
		}
	}
	catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
	{
		Console.WriteLine("Stopped following the printer.");
	}
}
```

Each `PrinterStatus` has the `State` (`Idle`, `Processing`, `Stopped` or `Unknown`), the print system's `StateMessage`, `StateReasons` as IPP `printer-state-reasons` keywords such as `media-empty-error`, and `IsAcceptingJobs`. A printer doesn't finish, so the token is the only way to stop: it's required here, where following a job can do without one. For a single check, call `printer.GetStatusAsync()`.

## Cancel a job

`CancelAsync` asks the print system to cancel the job. It returns `true` if the cancellation was accepted, after which the job reports `Canceled`, and `false` if the job had already finished. This cancels a job that hasn't finished within a minute:

```csharp
static async Task CancelIfSlowAsync(IPrintJob job)
{
	using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));

	try
	{
		await foreach (PrintJobStatus status in job.WatchStatusAsync(timeout.Token))
		{
			Console.WriteLine(status.State);
		}
	}
	catch (OperationCanceledException) when (timeout.IsCancellationRequested)
	{
		bool cancelled = await job.CancelAsync();

		Console.WriteLine(cancelled ? "Cancelled the job." : "The job had already finished.");
	}
}
```

Pages already sent to the printer may still come out. A print system that can't cancel jobs throws `NotSupportedException`.

The cancellation token you pass to `SubmitAsync` works the same way: it cancels sending the job, but not a job the print system has already accepted. Use `CancelAsync` for that.

## Find a job again

A web service or worker that restarts loses its `IPrintJob` objects, but not its jobs. Store the job's `PrinterId` and `Id` when you submit it, and after a restart look the printer up and ask it for the job with `GetJobAsync`:

```csharp
static async Task ReportStoredJobAsync(IPrinterCatalog catalog, string printerId, string jobId, CancellationToken cancellationToken)
{
	IPrinter printer = await catalog.GetPrinterAsync(printerId, cancellationToken);
	IPrintJob? job = await printer.GetJobAsync(jobId, cancellationToken);

	if (job is null)
	{
		// Print systems forget finished jobs, so it has most likely finished
		Console.WriteLine("The print system no longer knows the job.");

		return;
	}

	PrintJobStatus status = await job.GetStatusAsync(cancellationToken);
	Console.WriteLine(status.State);
}
```

The job found can be followed and cancelled like the one `SubmitAsync` returned. `GetJobAsync` returns `null` for a job the print system no longer knows:

- **Windows** removes a job from the queue as soon as it has printed, unless the printer's **Keep printed documents** setting is on.
- **CUPS** keeps finished jobs for a while, as its `PreserveJobHistory` setting says; a printer printed to directly, until it needs the memory.

So a job that isn't found has most likely finished, but whether it printed can't be told. To know that, follow the job until it ends while your application is running.

The code on this page is in [PrintOptionsAndJobs.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/PrintOptionsAndJobs.cs).
