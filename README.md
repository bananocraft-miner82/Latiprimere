# Latiprimere

A cross-platform printing library for .NET: discover printers, read their capabilities, submit jobs and track them to completion, with the same code on Windows, Linux and macOS.

> **Status:** preview. Version 0.1.0-preview is on [NuGet](https://www.nuget.org/packages/Latiprimere) for evaluation. The API may still change before 1.0, so it isn't ready for production use yet.

## Goals

- One API for printer discovery, capabilities, job submission, status and cancellation.
- Headless first: works in console apps, services, containers and web backends.
- No UI framework dependency in the core.
- IPP-first, using native platform APIs rather than command-line tools.
- Trimming and Native AOT compatible.

## Quick start

Install the catalog and the print systems for your platforms:

```bash
dotnet add package Latiprimere --prerelease
dotnet add package Latiprimere.Cups --prerelease               # Linux and macOS
dotnet add package Latiprimere.Windows --prerelease            # Windows
dotnet add package Latiprimere.Rendering.Pdfium --prerelease   # PDFs on Windows
dotnet add package Microsoft.Extensions.DependencyInjection
```

Then print a PDF on the default printer and follow the job:

```csharp
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new ServiceCollection();

services.AddLatiprimere();
services.AddLatiprimereCups();     // Linux and macOS
services.AddLatiprimereWindows();  // Windows
services.AddLatiprimerePdfium();   // PDFs on Windows

using ServiceProvider provider = services.BuildServiceProvider();
IPrinterCatalog catalog = provider.GetRequiredService<IPrinterCatalog>();

IPrinter printer = await catalog.GetDefaultPrinterAsync() ?? throw new InvalidOperationException("No default printer.");

using FileStream document = File.OpenRead("invoice.pdf");
IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default);

await foreach (PrintJobStatus status in job.WatchStatusAsync())
{
	Console.WriteLine($"Job {job.Id}: {status.State}");
}
```

A print system that can't work on the machine, such as the Windows spooler on Linux, is left out, so the same code runs everywhere.

## More examples

**A label in ZPL**, sent unchanged to a Zebra label printer:

```csharp
string zpl = $"""
	^XA
	^CF0,40
	^FO50,50^FDOrder {orderNumber}^FS
	^FO50,110^BY3^BCN,100,Y,N,N^FD{orderNumber}^FS
	^XZ
	""";

using MemoryStream data = new MemoryStream(Encoding.ASCII.GetBytes(zpl));
IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default);
```

**A network printer by address**, printed to directly over IPP, with the `Latiprimere.Ipp` package:

```csharp
services.AddLatiprimereIpp(options => options.PrinterUris.Add(new Uri("ipp://192.168.20.15/ipp/print")));

// Later, through the catalog
IPrinter printer = await catalog.GetPrinterAsync("ipp:ipp://192.168.20.15/ipp/print");
```

**Two double-sided copies, cancelled if not finished within a minute:**

```csharp
PrintOptions options = new PrintOptions { Copies = 2, Duplex = DuplexMode.LongEdge };
IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options);

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
	await job.CancelAsync();
}
```

## Documentation

- **[Developer documentation](https://latiprimere.com/docs/)**: installation, guides and the print systems, from easy to advanced. The pages are in [docs/site/](docs/site/).
- **[Examples](samples/Latiprimere.Examples/)**: every example from the documentation, as a program built with every change. Run one with `dotnet run --project samples/Latiprimere.Examples -f net10.0 -- list`.
- **[Project documents](docs/README.md)**: the feasibility research, project plan, decision records and progress log.

## Licence

Latiprimere is free under the [Community Licence](LICENSE) for:
- individuals and organisations with annual revenue under US$1 million;
- non-profits and education;
- open-source projects;
- evaluation and development;
- use through someone else's product.

Larger organisations using it in production need a [Commercial Licence](COMMERCIAL.md).

Contributors sign the [Contributor Licence Agreement](CLA.md).
