---
title: CUPS servers and the Windows spooler
description: Connect to remote CUPS servers with a password and TLS, and print PDFs, save with Microsoft Print to PDF and use print servers on Windows.
sidebar:
  order: 3
---

The CUPS and Windows print systems need no settings for the usual case: the local CUPS scheduler on Linux and macOS, and the local spooler on Windows. This page covers the rest.

## CUPS

### Which server

When you don't set `Server`, Latiprimere uses, in order:

1. the `CUPS_SERVER` environment variable, as the `lp` and `lpstat` commands do;
2. the local scheduler's socket;
3. `localhost:631`.

Set `Server` to use another machine, as a host name with an optional port (`printserver:631`) or the path of a socket. Setting a server also lets you use CUPS from Windows.

A `Server` that isn't either throws `ArgumentException` when `AddLatiprimereCups` is called. An invalid `CUPS_SERVER` doesn't stop your application starting: each request to CUPS fails with a `PrintException` saying what's wrong with it, and the catalog lists the other print systems' printers meanwhile.

### A server with a password and its own certificate

A standard CUPS installation needs no password to print. A print server set up to ask for one needs `Credentials`. They are sent with every request, so Latiprimere encrypts the connection with TLS whenever credentials are set.

CUPS creates a self-signed certificate for itself unless it's given another, and .NET doesn't trust those. Rather than turning the check off, trust that server's own certificate, by its thumbprint:

```csharp
ServiceCollection services = new ServiceCollection();

services.AddLatiprimere();
services.AddLatiprimereCups(options =>
{
	options.Server = "printserver.example.com:631";
	options.Credentials = new NetworkCredential("print-service", password);

	// Trust this server's own certificate, and no other
	options.ServerCertificateValidationCallback = (_, certificate, _, _) =>
		certificate is not null
		&& string.Equals(certificate.GetCertHashString(HashAlgorithmName.SHA256), serverThumbprint, StringComparison.OrdinalIgnoreCase);
});
```

Read the password from a secret store or configuration, never from source code. To find the server's thumbprint, run this on a machine you trust, and remove the colons from the result:

```bash
openssl s_client -connect printserver.example.com:631 </dev/null 2>/dev/null | openssl x509 -noout -fingerprint -sha256
```

:::caution
A callback that accepts every certificate gives up what encryption is for: anyone who can intercept the connection could read the password.
:::

### Other CUPS settings

| Setting          | Default                     | What it does                                                         |
| ---------------- | --------------------------- | -------------------------------------------------------------------- |
| `Encryption`     | `Automatic`                 | `Automatic` encrypts when credentials are set; `Always` encrypts every connection; `Never` sends everything, including any password, unencrypted. |
| `UserName`       | The credentials' user name, or the OS user | The user name CUPS records as the job's owner. CUPS trusts it without checking, until a request is authenticated. |
| `ConnectTimeout` | 10 seconds                  | How long to wait for a connection to open over the network.          |

If a server asks for a password that wasn't given, or refuses the one given, requests throw `PrintAuthenticationException`. To ask the user, catch it, then create the print system again with their credentials.

### Turning PDF pages

Latiprimere sends `PrintOptions.Orientation` to CUPS as IPP's `orientation-requested`. CUPS versions differ in what they do with it for a PDF:

- In testing with CUPS 2.4 on Linux and its CUPS-PDF printer, a portrait PDF printed with `Landscape` came out portrait: the PDF's own layout won.
- macOS's CUPS turned the portrait page onto landscape paper at its full size, so its top and bottom were cut off.

So with an orientation, Latiprimere also sends `print-scaling` as `auto-fit`, which asks CUPS to shrink a page only when it doesn't fit the paper, as Latiprimere does itself when it renders the pages. A `cups:fit-to-page`, `cups:scaling` or `ipp:print-scaling` vendor option of your own is sent instead. To be sure of landscape, make the PDF's pages landscape when you create it.

In the same tests, the paper size and monochrome came out as chosen.

## The Windows spooler

### PDFs need PDFium

CUPS takes JPEG and PNG images as they are, with `Scaling` as IPP's `print-scaling`, and converts them for the printer, as it does PDFs. It lays out text itself too, sent as UTF-8 `text/plain`, with any character its fonts have.

The Windows spooler doesn't print PDFs or images itself. `Latiprimere.Rendering.Pdfium` renders each page with PDFium, and Latiprimere draws the pages through the printer's driver. Register it with `AddLatiprimerePdfium()`, or pass `new PdfiumRasterizer()` to `WindowsPrintSystemFactory.Create`. Raw printer data needs no rasteriser. An image is placed on a page of the job's paper, or the driver's default, as a PDF, and text laid out on such pages in Courier, and both are printed the same way, in their pages' orientation. A text-only driver is refused them, as it is a PDF: send it text as raw data instead.

Because pages are drawn as images, a PDF saved with Microsoft Print to PDF holds images of the pages, not the original text.

### Microsoft Print to PDF in a service

Virtual printers such as Microsoft Print to PDF and the XPS Document Writer normally ask where to save the file. An application without a user interface can't show that dialog, so set `OutputFile`:

```csharp
static async Task SaveWithPrintToPdfAsync(IPrinterCatalog catalog, Stream document, string outputPath)
{
	IPrinter printer = await catalog.GetPrinterAsync("windows:Microsoft Print to PDF");
	PrintOptions options = new PrintOptions { OutputFile = outputPath };

	IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options);

	await foreach (PrintJobStatus status in job.WatchStatusAsync())
	{
		Console.WriteLine(status.State);
	}
}
```

This is also a handy way to test printing code without using paper. Only the Windows spooler can print to a file; other print systems throw `NotSupportedException` when `OutputFile` is set.

### PDFs straight to the printer over IPP

A PDF drawn through a driver reaches the printer as a picture of each page, and the spooler often says a job has finished as soon as it has left the computer. Most network printers' queues stand in front of a printer that also speaks IPP. With `PrintPdfOverIpp` on, a PDF job for such a queue goes straight to that printer instead, as it is or, for a printer that can't take PDF, rendered as PWG raster, Apple raster or PCLm, tried in the order `IppRasterFormats` lists them. It prints faster, with sharp text, and the job reports how it really ended.

```csharp
ServiceCollection services = new ServiceCollection();

services.AddLatiprimere();
services.AddLatiprimerePdfium(); // Draws PDFs through drivers, and renders them for printers that can't take PDF
services.AddLatiprimereWindows(options =>
{
	options.PrintPdfOverIpp = true;

	// A queue on a Standard TCP/IP port doesn't say where its printer's IPP is
	options.IppPrinterUris["Warehouse Laser"] = new Uri("ipp://192.168.20.15/ipp/print");
});
```

Latiprimere finds a queue's printer in one of three ways:

- **Configured:** the queue's name is in `IppPrinterUris`. Needed for queues on Standard TCP/IP ports, as their port doesn't give the IPP address and Latiprimere doesn't guess it.
- **Added by URL:** the queue was added with the printer's address, so its port is named after it.
- **WSD:** the queue is on a WSD port, which Windows sets up for printers it finds itself. Its port records the printer's UUID, and Latiprimere looks on the network, over mDNS, for a printer announcing that UUID, then checks the printer reports the same UUID when asked. The first PDF job listens for up to two seconds; a printer not found isn't looked for again for five minutes. Any device on the network can announce a UUID, so for documents that must reach the real printer, configure its `ipps://` address in `IppPrinterUris` and pin its certificate (see [Anyone on the network can announce a printer](/docs/print-systems/network-printers/#anyone-on-the-network-can-announce-a-printer)), or leave `PrintPdfOverIpp` off.

A job goes through the queue's driver as before when any of these is so:

- the queue's printer can't be found, or is on a print server;
- the printer takes neither PDF nor a raster format in `IppRasterFormats`, or there's no PDF rasteriser for a printer that only takes raster;
- the job sets `OutputFile`, an `InputTray` or a `windows:` vendor option, which need the driver;
- the printer can't be reached when the job is sent, or its `ipps://` certificate isn't trusted: the queue holds the job until the printer is back, and the next job looks for the printer again;
- the printer says it asks for a password it won't be sent, or for authentication other than a user name and password: see below;
- the pages can't be planned as raster for the printer, such as when it reports no paper size.

Those last two need a document stream that can seek, such as a file, to start again through the queue: from one that can't, the exception is thrown instead.

The same route gives a queue what the spooler can't: the printer's details, from `GetDetailsAsync`, and asking it to identify itself, with `IdentifyAsync`. They need the printer to be found, but not to take PDF (see [Printer details, identifying a printer and test pages](/docs/guides/printer-details-and-test-pages/)).

Jobs sent over IPP bypass the Windows queue: they don't appear in its queue window, and its pause, permissions, pooling and accounting don't apply to them. Their job IDs are the printer's, after `ipp:`, such as `ipp:42`, which `GetJobAsync` uses to ask the printer rather than the queue; `ipp:` vendor options are sent with them, and `MaxRenderDpi` limits the resolution of any raster.

#### Printers over IPPS, with a password

An `ipps://` address in `IppPrinterUris`, or a WSD queue's printer that announces one, is reached over an encrypted connection. Printers nearly always present a certificate they made themselves, which no computer trusts, so pin it, and give the printer its password if it asks for one, as for [network printers](/docs/print-systems/secure-printers/) printed to directly:

```csharp
services.AddLatiprimereWindows(options =>
{
	options.PrintPdfOverIpp = true;
	options.IppPrinterUris["Finance"] = new Uri("ipps://192.168.20.30:443/ipp/print");

	// The fingerprint the printer's web page shows
	options.IppTrustedCertificateFingerprints.Add(fingerprint);

	// Sent to this printer only, and only over IPPS, never in clear text
	options.IppPrinterCredentials[new Uri("ipps://192.168.20.30:443/ipp/print")] = new NetworkCredential("finance", password);
});
```

- **Until its certificate is trusted,** the queue's jobs go through its driver, and the fingerprint is logged as a warning, naming `IppTrustedCertificateFingerprints`. Check it against the printer's web page before adding it. `IppServerCertificateValidationCallback` is there for any other rule.
- **Passwords** in `IppCredentials`, for every printer, or `IppPrinterCredentials`, for one, go only over IPPS, as HTTP Basic authentication, never in clear text. With `IppCredentials` set, a WSD queue's printer must have its certificate pinned before it gets the password, as anyone on the network could announce a printer with the queue's UUID.
- **Once a certificate is pinned** or the callback is set, a WSD queue's printer that announces an IPPS address as well is reached there first, and over plain IPP if its certificate isn't trusted.
- **A printer says what it asks for** at each of its addresses. One that asks for a password it won't be sent, as at a plain `ipp://` address, or for authentication other than a user name and password, has its jobs printed through the driver instead, without being sent any, and that's logged.

### Printers on print servers

Printers on a Windows print server that the user has connected to are listed like local printers. To look up one the user hasn't connected to, by an ID such as `windows:\\server\printer`, turn on `AllowAnyPrinterName`:

```csharp
services.AddLatiprimereWindows(options => options.AllowAnyPrinterName = true);
```

:::caution
Looking up `\\server\printer` contacts that server, and Windows may sign in to it with your application's account. A server chosen by someone else could capture that sign-in and misuse it. Turn this on only when every printer ID your application looks up is trusted, never for IDs that come from users or web requests.
:::

### The Print Spooler service

If the Print Spooler service is stopped, the Windows print system can't be used: the catalog skips it, and throws `PrintSystemUnavailableException` if there's no other print system to use.

The code on this page is in [CupsAndWindows.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/CupsAndWindows.cs).
