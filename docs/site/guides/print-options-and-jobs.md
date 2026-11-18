---
title: Print options and following jobs
description: Choose copies, two-sided printing and orientation, follow a job's progress with a timeout, and cancel it.
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
| `OutputFile`  | A file to print to instead of the printer (Windows only)   | `null`: print normally         |
| `VendorOptions` | Anything else the printer offers, by key: see below      | Empty                          |

For two-sided printing, `LongEdge` turns pages like a book, and `ShortEdge` flips them like a notepad, which suits landscape pages. `OutputFile` is for virtual printers such as Microsoft Print to PDF: see [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/).

:::note[Orientation and PDFs on CUPS]
CUPS may not turn a PDF's pages for `Landscape`: in testing with CUPS 2.4, a portrait PDF stayed portrait. To print landscape through CUPS, make the PDF's pages landscape. On Windows, `Landscape` turns the page and scales the content to fit across it. Network printers are sent the setting as IPP's `orientation-requested`, and what a printer does with it for a PDF is up to the printer. See [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/#pdfs-keep-their-own-orientation).
:::

Setting `Copies` below 1 throws `ArgumentOutOfRangeException`. A printer that doesn't support a setting may ignore it or reject the job, depending on the print system: [Reading printer capabilities](/docs/guides/printer-capabilities/) shows how to check first.

### Vendor options

`VendorOptions` sets what the typed options don't cover, such as print quality, stapling or a driver's own settings. Each key starts with the print system that reads it, and each print system ignores the others' keys:

| Key                                  | Read by                         | Example                                              |
| ------------------------------------ | ------------------------------- | ---------------------------------------------------- |
| `ipp:<attribute>`                    | CUPS and network printers       | `ipp:print-quality` = `5`, `ipp:printer-resolution` = `1200dpi` |
| `cups:<option>`                      | CUPS                            | `cups:fit-to-page` = `true`                          |
| `windows:feature:<feature>`          | Windows                         | `windows:feature:psk:PageOutputQuality` = `psk:High` |
| `windows:parameter:<parameter>`      | Windows                         | `windows:parameter:ns0000:JobDarkness` = `2`         |
| `windows:print-ticket`               | Windows                         | A Print Ticket, as XML                               |
| `windows:devmode`                    | Windows                         | A DEVMODE saved from the driver, in base64           |

The printer's capabilities list the keys and values it takes: see [Vendor options](/docs/guides/printer-capabilities/#vendor-options-everything-else-the-printer-offers). A key that sets something a typed option sets for the same job, such as `ipp:copies` with `Copies`, is refused with `ArgumentException`. On Windows, raw jobs don't use the `windows:` keys, since the data goes to the printer unchanged.

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

## Job states

`WatchStatusAsync` checks the job's status every half second and reports it each time its state, message or page count changes. It ends after the first final state.

| State        | Meaning                                                          | Final |
| ------------ | ---------------------------------------------------------------- | ----- |
| `Pending`    | Waiting in the queue                                             |       |
| `Held`       | Held in the queue until someone releases it                      |       |
| `Processing` | Being processed or printed                                       |       |
| `Stopped`    | Stopped part-way, for example because the printer needs attention |      |
| `Completed`  | Printed                                                          | Yes   |
| `Cancelled`  | Cancelled                                                        | Yes   |
| `Aborted`    | Ended by the print system because of an error                    | Yes   |
| `Unknown`    | The print system didn't say                                      |       |

Each `PrintJobStatus` also has:

- **`StateMessage`:** the print system's own explanation, such as "Media empty", when it gives one;
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

## Cancel a job

`CancelAsync` asks the print system to cancel the job. It returns `true` if the cancellation was accepted, after which the job reports `Cancelled`, and `false` if the job had already finished. This cancels a job that hasn't finished within a minute:

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

The code on this page is in [PrintOptionsAndJobs.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/PrintOptionsAndJobs.cs).
