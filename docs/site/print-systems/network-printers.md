---
title: Network printers over IPP
description: Print straight to network printers with IPP, without CUPS or the Windows spooler, finding them automatically or configuring them by address.
sidebar:
  order: 1
---

Most network printers made in the last decade speak IPP, the Internet Printing Protocol. `Latiprimere.Ipp` prints to them directly, with no driver, no CUPS and no Windows spooler. That makes it a good fit for containers, servers and kiosks, and for label printers that have no driver installed.

## Set it up

`AddLatiprimereIpp()` on its own finds printers on the local network. This also adds a printer by address, and changes three timings:

```csharp
ServiceCollection services = new ServiceCollection();

services.AddLatiprimere();
services.AddLatiprimerePdfium(); // Renders PDFs for printers that can't take PDF
services.AddLatiprimereIpp(options =>
{
	// A printer that discovery can't see, such as one on another subnet
	options.PrinterUris.Add(new Uri("ipp://192.168.20.15/ipp/print"));

	options.DiscoveryDuration = TimeSpan.FromSeconds(3); // Listen a little longer on a busy network
	options.ConnectTimeout = TimeSpan.FromSeconds(5);    // Give up sooner on printers that are off
	options.BusyRetryDuration = TimeSpan.FromMinutes(5); // A slow label printer can be busy for a while
});
```

Without dependency injection, pass the same settings to `IppNetworkPrintSystemFactory.Create(new PdfiumRasterizer(), new IppNetworkOptions { … })`, and dispose the print system when you've finished.

## Printers that don't take PDF

Many network printers, most inkjets among them, don't take PDF over IPP. Instead they take page images: PWG raster, which every IPP Everywhere printer takes, or Apple raster, which every AirPrint printer takes. With `AddLatiprimerePdfium()` from Latiprimere.Rendering.Pdfium registered, Latiprimere renders the PDF itself and sends its pages in whichever of the two the printer takes, preferring PWG raster. A printer that takes PDF is always sent the PDF.

Without PDFium, a PDF to such a printer is refused with `NotSupportedException`, saying to add it, before anything is sent. Raw printer data is never rendered.

The job is printed the way the printer itself would print a PDF:

- **Resolution:** the printer's default. `MaxRenderDpi` sets a limit; the printer is then sent the highest resolution it lists within that limit, or its lowest if none is that low.
- **Colour:** colour when `ColorMode` asks for it or the printer prints in colour by default, and the printer can; otherwise grey, or black and white on a printer that takes only that.
- **Paper:** `MediaSize`, or the printer's default paper. Each page is centred on the paper and shrunk to fit if it's too big, never enlarged.
- **Orientation:** each page is turned to fit the paper, unless you set `Orientation`.
- **Two-sided printing:** back sides are turned the way the printer says it needs them.
- **Copies:** the printer makes them, from one rendering.

Pages are rendered while they upload, so memory doesn't grow with the number of pages: beyond the PDF itself, a job holds a band of a page, 16 MB at most. The upload is gzipped when the printer offers it. Rendering takes time: an A4 page in colour at 600 dpi is about 100 MB of pixels before compression. `MaxRenderDpi = 300` renders a quarter of that, at the cost of softer small text.

## Discovery

Printers announce themselves on the local network with DNS-SD, also known as Bonjour or mDNS. Each time you list printers, Latiprimere listens for `DiscoveryDuration`, two seconds by default, and lists every IPP printer that answered.

- **Listing doesn't contact the printers,** so a printer that's switched off can still appear, with an `Unknown` state. Looking it up with `GetPrinterAsync` reads its current details.
- **Discovery stays on the local network.** It doesn't cross routers, and firewalls must let multicast DNS through (UDP port 5353). In containers it often doesn't work at all. Configure those printers by address instead.
- **To turn discovery off,** set `EnableDiscovery = false`, for example when you only use printers you've configured.

A discovered printer's ID is its announced name, such as `ipp:dnssd:Office Laser`. The ID survives the printer getting a new IP address: a lookup tries the last address seen for that name, then searches the network if it doesn't answer. Renaming the printer changes its ID.

## Printers by address

Add a printer's IPP address to `PrinterUris` to list it whether or not it announces itself. Its ID is `ipp:` followed by the address, and that never changes:

```csharp
static async Task PrintToConfiguredPrinterAsync(IPrinterCatalog catalog, Stream label)
{
	IPrinter printer = await catalog.GetPrinterAsync("ipp:ipp://192.168.20.15/ipp/print");

	await printer.SubmitAsync(PrintContent.FromRaw(label), PrintOptions.Default);
}
```

A printer's IPP address is usually on its configuration page or in its manual. The path is often `/ipp/print`, and the port is 631 unless the address says otherwise.

### Only known addresses, unless you allow any

By default, `GetPrinterAsync` only connects to addresses in `PrinterUris` or printers found by discovery. An ID with any other address finds no printer, without connecting to it.

That matters when printer IDs come from outside your application, such as from a web request. If any address were accepted, anyone who can send you an ID could make your server connect to internal services on their behalf, an attack called server-side request forgery. Set `AllowAnyPrinterUri = true` only when every printer ID your application looks up is trusted.

## Busy printers

Many printers, especially label printers, take one job at a time and turn others away while busy. Latiprimere sends the job again until it is accepted or `BusyRetryDuration` runs out: two minutes by default, or zero to send each job once. It waits a second before the first retry, and doubles the wait up to 15 seconds.

Each retry sends the whole document again, so this only works with a seekable stream, such as a file or a `MemoryStream`. A job from a stream that can't seek fails on the first busy answer. A PDF sent as raster is the exception: its pages are rendered again for each retry, so its stream needn't seek.

## Options the printer can't carry out

A network printer refuses a job that asks for something it can't do, such as two-sided printing on a one-sided printer or a paper size it doesn't have, with a `PrintException` whose `NativeErrorCode` is the IPP status `0x040B` (client-error-attributes-or-values-not-supported). Its message names the setting.

Before sending the document, Latiprimere asks the printer to check the job's settings with Validate-Job, so a refusal comes at once, without uploading the document first. It costs one short request per job, and is only sent to printers that list Validate-Job among their operations, as almost all do. A printer too busy to answer it is sent the job anyway, and checks it then. Set `ValidateJobs = false` to skip it.

To avoid refusals altogether, choose settings from the printer's capabilities: see [Reading printer capabilities](/docs/guides/printer-capabilities/).

## Other settings

| Setting             | Default        | What it does                                                         |
| ------------------- | -------------- | -------------------------------------------------------------------- |
| `ConnectTimeout`    | 10 seconds     | How long to wait for a connection to open. It doesn't limit how long the printer takes to answer. |
| `UserName`          | The OS user    | The user name sent with jobs, which some printers show or log.        |
| `ValidateJobs`      | `true`         | Whether to ask the printer to check a job's settings before sending the document. |
| `MaxRenderDpi`      | None           | The highest resolution a PDF is rendered at for a printer that can't take PDF. |

The code on this page is in [NetworkPrinters.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/NetworkPrinters.cs).
