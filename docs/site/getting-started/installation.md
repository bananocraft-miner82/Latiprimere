---
title: Installation
description: Choose the packages for your platforms, then set Latiprimere up with or without dependency injection.
sidebar:
  order: 1
---

Latiprimere is split into small packages: one that finds printers, and one for each print system. Every package targets .NET 10 and .NET 11, and works with trimming and Native AOT.

## Choose your packages

| Package                        | What it does                                                                        |
| ------------------------------ | ----------------------------------------------------------------------------------- |
| `Latiprimere`                  | The printer catalog, which finds printers across every print system. Always install it. |
| `Latiprimere.Cups`             | Prints through CUPS: on Linux and macOS, or with a CUPS server from any platform.   |
| `Latiprimere.Windows`          | Prints through the Windows print spooler.                                           |
| `Latiprimere.Rendering.Pdfium` | Lets the Windows spooler print PDFs.                                                |
| `Latiprimere.Ipp`              | Prints straight to network printers, without CUPS or the spooler, on any platform.  |

`Latiprimere.Abstractions`, with the interfaces and types, comes with them. Reference it on its own only from a library that should depend on the interfaces and nothing else.

Only previews are published so far, so add `--prerelease`:

```bash
dotnet add package Latiprimere --prerelease
dotnet add package Latiprimere.Cups --prerelease
dotnet add package Latiprimere.Windows --prerelease
dotnet add package Latiprimere.Rendering.Pdfium --prerelease
dotnet add package Latiprimere.Ipp --prerelease
```

You can install every print system, even for an app that runs on one platform. A print system that can't work on the machine, such as the Windows spooler on Linux, reports itself as unsupported, and the catalog leaves it out. So one build runs everywhere.

## Set up with dependency injection

Register the catalog with `AddLatiprimere()`, then each print system:

```csharp
ServiceCollection services = new ServiceCollection();

services.AddLatiprimere();         // The printer catalog
services.AddLatiprimereCups();     // Linux and macOS
services.AddLatiprimereWindows();  // Windows
services.AddLatiprimerePdfium();   // PDFs on Windows
services.AddLatiprimereIpp();      // Network printers, on every platform

using ServiceProvider provider = services.BuildServiceProvider();
IPrinterCatalog catalog = provider.GetRequiredService<IPrinterCatalog>();
```

In a console app, `ServiceCollection` comes from the `Microsoft.Extensions.DependencyInjection` package. ASP.NET Core and the .NET Generic Host already include it: call the same methods on `builder.Services`.

The catalog is registered once, as a singleton, so you can inject `IPrinterCatalog` wherever you need it.

Each print system's method takes optional settings, such as `AddLatiprimereCups(options => options.Server = "printserver:631")`. They are covered in [Network printers over IPP](/docs/print-systems/network-printers/) and [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/).

## Set up without dependency injection

Each print system has a factory, and you can create the catalog yourself:

```csharp
using IPrintSystem cups = CupsPrintSystemFactory.Create();
using IPrintSystem windows = WindowsPrintSystemFactory.Create(new PdfiumRasterizer());
using IPrintSystem network = IppNetworkPrintSystemFactory.Create();

PrinterCatalog catalog = new PrinterCatalog([cups, windows, network]);

IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync();

foreach (PrinterInfo printer in printers)
{
	Console.WriteLine($"{printer.Name}  ({printer.Id})");
}
```

- **Dispose the print systems** when you've finished with them, as the `using` statements do. They hold connections. The catalog never disposes them.
- **Pass a PDF rasteriser to the Windows factory** to print PDFs. Without one, the Windows spooler can print raw data only.
- **Each factory takes the same settings** as its `Add…` method, for example `CupsPrintSystemFactory.Create(new CupsOptions { Server = "printserver:631" })`.

The code on this page is in [Installation.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/Installation.cs).

## Next

[Your first print job](/docs/getting-started/first-print-job/) puts this together into a complete program.
