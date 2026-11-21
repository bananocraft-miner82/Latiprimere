# Latiprimere

A cross-platform printing library for .NET: find printers, read their capabilities, submit jobs and follow them to completion, with the same code on Windows, Linux and macOS. It is headless, so it works in console apps, services, containers and web back ends, and it is trimming and Native AOT compatible.

> **Preview.** This is an early preview for evaluation. The API may change before 1.0. macOS hasn't been checked on real hardware yet.

## Packages

| Package | What it does |
|---|---|
| `Latiprimere` | The printer catalog: one list of printers from every print system you register |
| `Latiprimere.Abstractions` | The contracts: printers, jobs, options, capabilities and exceptions |
| `Latiprimere.Cups` | Prints through CUPS on Linux and macOS, or through a CUPS print server from any platform |
| `Latiprimere.Windows` | Prints through the Windows print spooler |
| `Latiprimere.Rendering.Pdfium` | Renders PDFs with PDFium, which the Windows spooler needs to print them |
| `Latiprimere.Ipp` | Prints straight to IPP network printers, found by mDNS or given by URI |

Install `Latiprimere` and the print systems you need:

```bash
dotnet add package Latiprimere --prerelease
dotnet add package Latiprimere.Cups --prerelease
dotnet add package Latiprimere.Windows --prerelease
dotnet add package Latiprimere.Rendering.Pdfium --prerelease
```

## Quick start

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

foreach (PrinterInfo info in await catalog.GetPrintersAsync())
{
	Console.WriteLine($"{info.Id}  {info.Name}  {info.State}");
}

IPrinter printer = await catalog.GetDefaultPrinterAsync() ?? throw new InvalidOperationException("No default printer.");

using FileStream document = File.OpenRead("invoice.pdf");
IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default);

await foreach (PrintJobStatus status in job.WatchStatusAsync())
{
	Console.WriteLine(status.State);
}
```

A print system that isn't available on the machine, such as the Windows spooler on Linux, simply contributes no printers. Use `PrintContent.FromRaw` to send data in the printer's own language, such as ZPL to a label printer.

## Security

Report a vulnerability privately, as the [security policy](https://github.com/bananocraft-miner82/Latiprimere/blob/main/SECURITY.md) describes, not in a public issue. The policy also says which versions get security updates, and for how long. Each package carries a CycloneDX software bill of materials at `sbom/<package>.cdx.json`.

## Licence

Latiprimere is free under the [Community Licence](https://github.com/bananocraft-miner82/Latiprimere/blob/main/LICENSE) for:
- individuals and organisations with annual revenue under US$1 million;
- non-profits and education;
- open-source projects;
- evaluation and development;
- use through someone else's product.

Larger organisations using it in production need a [Commercial Licence](https://github.com/bananocraft-miner82/Latiprimere/blob/main/COMMERCIAL.md). The licence text is in each package.

## Links

- [Source code and issues](https://github.com/bananocraft-miner82/Latiprimere)
- [Documentation](https://github.com/bananocraft-miner82/Latiprimere/tree/main/docs)
