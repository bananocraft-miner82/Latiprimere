---
title: CUPS servers and the Windows spooler
description: Connect to remote CUPS servers with a password and TLS, and print PDFs, save with Microsoft Print to PDF and use print servers on Windows.
sidebar:
  order: 2
---

The CUPS and Windows print systems need no settings for the usual case: the local CUPS scheduler on Linux and macOS, and the local spooler on Windows. This page covers the rest.

## CUPS

### Which server

When you don't set `Server`, Latiprimere uses, in order:

1. the `CUPS_SERVER` environment variable, as the `lp` and `lpstat` commands do;
2. the local scheduler's socket;
3. `localhost:631`.

Set `Server` to use another machine, as a host name with an optional port (`printserver:631`) or the path of a socket. Setting a server also lets you use CUPS from Windows.

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

## The Windows spooler

### PDFs need PDFium

The Windows spooler doesn't print PDFs itself. `Latiprimere.Rendering.Pdfium` renders each page with PDFium, and Latiprimere draws the pages through the printer's driver. Register it with `AddLatiprimerePdfium()`, or pass `new PdfiumRasterizer()` to `WindowsPrintSystemFactory.Create`. Raw printer data needs no rasteriser.

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
