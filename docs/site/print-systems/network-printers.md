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
services.AddLatiprimereIpp(options =>
{
	// A printer that discovery can't see, such as one on another subnet
	options.PrinterUris.Add(new Uri("ipp://192.168.20.15/ipp/print"));

	options.DiscoveryDuration = TimeSpan.FromSeconds(3); // Listen a little longer on a busy network
	options.ConnectTimeout = TimeSpan.FromSeconds(5);    // Give up sooner on printers that are off
	options.BusyRetryDuration = TimeSpan.FromMinutes(5); // A slow label printer can be busy for a while
});
```

Without dependency injection, pass the same settings to `IppNetworkPrintSystemFactory.Create(new IppNetworkOptions { … })`, and dispose the print system when you've finished.

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

Each retry sends the whole document again, so this only works with a seekable stream, such as a file or a `MemoryStream`. A job from a stream that can't seek fails on the first busy answer.

## Other settings

| Setting             | Default        | What it does                                                         |
| ------------------- | -------------- | -------------------------------------------------------------------- |
| `ConnectTimeout`    | 10 seconds     | How long to wait for a connection to open. It doesn't limit how long the printer takes to answer. |
| `UserName`          | The OS user    | The user name sent with jobs, which some printers show or log.        |

The code on this page is in [NetworkPrinters.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/NetworkPrinters.cs).
