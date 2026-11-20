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

Without dependency injection, pass the same settings to `IppNetworkPrintSystemFactory.Create(new PdfiumRasterizer(), new IppNetworkOptions { … }, loggerFactory)`, with `null` for a logger factory if you don't log,, and dispose the print system when you've finished.

## Printers that don't take PDF

Many network printers, most inkjets among them, don't take PDF over IPP. Instead they take page images: PWG raster, which every IPP Everywhere printer takes; Apple raster, which every AirPrint printer takes; or PCLm, which Mopria printers take. With `AddLatiprimerePdfium()` from Latiprimere.Rendering.Pdfium registered, Latiprimere renders the PDF itself and sends its pages in one of these the printer takes. A printer that takes PDF is always sent the PDF.

Without PDFium, a PDF to such a printer is refused with `NotSupportedException`, saying to add it, before anything is sent. Raw printer data is never rendered.

`RasterFormats` sets which raster formats may be used, in the order they're tried: by default PWG raster, then Apple raster, then PCLm. A printer that takes more than one gets the first. To try PCLm first, or leave a format out:

```csharp
services.AddLatiprimereIpp(options =>
{
	options.RasterFormats.Clear();
	options.RasterFormats.Add(IppRasterFormat.Pclm);
	options.RasterFormats.Add(IppRasterFormat.AppleRaster);
});
```

With the list empty, PDFs are never rendered: a PDF to a printer that can't take PDF is refused with `NotSupportedException`, naming the raster formats it takes that were left out.

Images go the same way. A JPEG to a printer that takes JPEG, as IPP Everywhere and AirPrint printers are expected to, is sent as it is, with `Scaling` as IPP's `print-scaling`. A PNG, or a JPEG to a printer that doesn't take JPEG, is placed on a page of the job's paper, or the printer's default, as a PDF, which is sent as it is or rendered as raster like any PDF. A printer that takes neither PDF nor a raster format Latiprimere can render for it is refused with `NotSupportedException` before anything is sent.

Text is always laid out by Latiprimere as a PDF on the job's paper, even for a printer that lists `text/plain`, so it looks the same on every printer. It is limited to the Windows-1252 characters Courier shows; text with others is refused with `NotSupportedException` before anything is sent.

PCLm is a PDF holding each page as image strips. Strips are as tall as the printer prefers, and compressed with Flate or run-length encoding, whichever it prefers of the two. Latiprimere doesn't write PCLm's JPEG strips, which would blur text.

The job is printed the way the printer itself would print a PDF:

- **Resolution:** the printer's default. `MaxRenderDpi` sets a limit; the printer is then sent the highest resolution it lists within that limit, or its lowest if none is that low. Resolutions above 2400 dpi, and pages more than 100,000 pixels across or down, are never rendered.
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

Some printers stop answering the search while they sleep. To find them after your application restarts, set `AddressStorePath` to a file where Latiprimere can remember each printer's last address:

```csharp
options.AddressStorePath = Path.Combine(AppContext.BaseDirectory, "printers.json");
```

- **Used only while it's the same printer:** a remembered address is used only while the printer there still reports the UUID it announced, and the search follows if it doesn't answer.
- **Never an error:** a missing or damaged file means starting with nothing remembered, and is logged. So does a file a later version of Latiprimere wrote in a newer format, which is then left as it is.
- **Keep it where only your application can write:** its addresses count as printers found on the network.

### Anyone on the network can announce a printer

Discovery takes printers at their word. Multicast DNS has no way to check who's speaking, so any device on the same network can announce a printer under any name, including a name your real printer uses, or answer a search for it first. A job for a printer found that way goes to whoever announced it, and they can read the document.

Latiprimere guards what it can:

- **A remembered address is used only while the printer there reports the UUID it announced,** which keeps a job from going to another printer that took over the address. A device that pretends to be the printer can report any UUID it likes, so this doesn't stop it.
- **A printer's IPPS address is used only if it was announced by the same host as its IPP one,** and not with another UUID.
- **A shared password goes to a printer found on the network only once its certificate is pinned:** see [who gets the shared password](/docs/print-systems/secure-printers/#who-gets-the-shared-password).

None of that stops a device that announces a printer from receiving the jobs sent to it by name. When that matters, as for payslips or medical records:

- **Configure the printer by its `ipps://` address in `PrinterUris`, pin its certificate, and print to it by that address,** as `ipp:ipps://…`. A pinned certificate is the one thing another device can't present. Looked up by name instead, a printer whose IPPS certificate isn't trusted is reached over plain IPP, so pinning protects only the address.
- **Turn discovery off** with `EnableDiscovery = false` once your printers are configured, so they aren't listed alongside whatever else announces itself. Looking up an `ipp:dnssd:` ID still searches the network, so keep those IDs out of printer IDs you store.
- **Keep untrusted devices off the network your printers are on,** as for any service found by multicast DNS.

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

## Secure printers (IPPS)

Many printers also take jobs over an encrypted connection, at an `ipps://` address, and some ask for a user name and password. Printers nearly always present a certificate they made themselves, which no computer trusts, so Latiprimere refuses a printer's IPPS address until you pin its certificate. Pinning, passwords, and how printers found on the network are reached over IPPS are in [Secure network printers](/docs/print-systems/secure-printers/).

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
| `UploadStallTimeout` | 5 minutes     | How long a printer may go without taking any more of a job, or without answering once it has been sent it all, before the job fails. A printer that keeps reading, however slowly, is never cut short. `Timeout.InfiniteTimeSpan` waits as long as it takes. |
| `UserName`          | The OS user    | The user name sent with jobs, which some printers show or log.        |
| `ValidateJobs`      | `true`         | Whether to ask the printer to check a job's settings before sending the document. |
| `MaxRenderDpi`      | None           | The highest resolution a PDF is rendered at for a printer that can't take PDF. |
| `TrustedCertificateFingerprints` | None | SHA-256 fingerprints of printers' certificates to trust over IPPS. See [Secure network printers](/docs/print-systems/secure-printers/). |
| `ServerCertificateValidationCallback` | None | Your own check of a certificate that isn't pinned and the system's trust isn't enough for. |
| `Credentials`       | None           | A user name and password for every printer, sent only over IPPS, to printers whose certificate is trusted. |
| `PrinterCredentials` | None          | A user name and password for one printer, by its URI, sent only over IPPS. |
| `AddressStorePath`  | None           | A file in which to remember discovered printers' addresses across restarts. |

The code on this page is in [NetworkPrinters.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/NetworkPrinters.cs).
