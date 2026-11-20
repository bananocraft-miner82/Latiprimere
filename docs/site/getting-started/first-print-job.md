---
title: Your first print job
description: A complete console program that prints a PDF on the default printer and follows the job until it finishes.
sidebar:
  order: 2
---

This page builds a console app that prints a PDF on your default printer and reports the job's progress. It runs unchanged on Windows, Linux and macOS.

## Create the app

```bash
dotnet new console -n FirstPrint
cd FirstPrint
dotnet add package Latiprimere --prerelease
dotnet add package Latiprimere.Cups --prerelease
dotnet add package Latiprimere.Windows --prerelease
dotnet add package Latiprimere.Rendering.Pdfium --prerelease
dotnet add package Microsoft.Extensions.DependencyInjection
```

## The program

Replace `Program.cs` with:

```csharp title="Program.cs"
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;

string pdfPath = args[0];

// Register Latiprimere and the print systems for the platforms you run on
ServiceCollection services = new ServiceCollection();

services.AddLatiprimere();
services.AddLatiprimereCups();     // Linux and macOS
services.AddLatiprimereWindows();  // Windows
services.AddLatiprimerePdfium();   // PDFs on Windows

using ServiceProvider provider = services.BuildServiceProvider();
IPrinterCatalog catalog = provider.GetRequiredService<IPrinterCatalog>();

// Find the default printer
IPrinter? printer = await catalog.GetDefaultPrinterAsync();

if (printer is null)
{
	Console.WriteLine("There is no default printer.");

	return;
}

Console.WriteLine($"Printing on {printer.Info.Name}");

// Send the PDF
using FileStream document = File.OpenRead(pdfPath);
IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default);

// Follow the job until it finishes
await foreach (PrintJobStatus status in job.WatchStatusAsync())
{
	Console.WriteLine($"Job {job.Id}: {status.State}");
}
```

Run it with a PDF:

```bash
dotnet run -- invoice.pdf
```

You'll see something like this:

```text
Printing on Office Laser
Job 42: Pending
Job 42: Processing
Job 42: Completed
```

## What each step does

1. **Registering.** `AddLatiprimere()` adds the printer catalog, and each `Add…` call adds a print system. The ones that can't work on this machine are left out, which is why the same program runs everywhere.
2. **Finding the default printer.** `GetDefaultPrinterAsync()` returns `null` when there isn't one, so check for it. [Finding printers](/docs/getting-started/finding-printers/) shows how to list printers and choose one.
3. **Sending the document.** `PrintContent.FromPdf` wraps your stream. You own the stream, so dispose it, as `using` does here. `PrintOptions.Default` prints one copy with the printer's own settings; [Print options and following jobs](/docs/guides/print-options-and-jobs/) covers the rest.
4. **Following the job.** `WatchStatusAsync()` reports each change: as it happens on Windows, which signals changes, and by checking the job twice a second elsewhere. It ends when the job reaches a final state: completed, cancelled or aborted.

`SubmitAsync` returns once the print system has accepted the job, not when it has printed. Following the job is optional: if you don't need to know when it finishes, stop after `SubmitAsync`.

## If something goes wrong

- **"No supported print system is available"**: no print system for this platform was registered, or its service isn't running.
- **"Printing PDF documents on Windows needs a PDF rasteriser"**: add `Latiprimere.Rendering.Pdfium` and call `AddLatiprimerePdfium()`.

[Errors and troubleshooting](/docs/guides/errors-and-troubleshooting/) covers these and more.

This program is `FirstPrintJob.RunAsync` in [FirstPrintJob.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/FirstPrintJob.cs).
