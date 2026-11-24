---
title: Secure network printers (IPPS)
description: Print to network printers over an encrypted connection, trust their own certificates by pinning them, and send them a user name and password.
sidebar:
  order: 2
---

Many network printers also take jobs over an encrypted connection, at an `ipps://` address, and some ask for a user name and password. This page is about [network printers over IPP](/docs/print-systems/network-printers/) printed to directly. For a CUPS server with a password, see [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/#a-server-with-a-password-and-its-own-certificate).

## Why IPPS needs your say-so

A printer nearly always presents a certificate it made itself, which no computer trusts. Accepting it without a check would let anyone who can intercept the connection read the documents and any password. So Latiprimere refuses a printer's IPPS address until you say its certificate is the right one, and says which certificate it was:

```text
The printer at 'ipps://192.168.20.15:443/ipp/print' presented a certificate that isn't trusted, with SHA-256
fingerprint 3A:7F:…:C8. If that is the fingerprint the printer's own web page shows, add it to
IppNetworkOptions.TrustedCertificateFingerprints to trust it.
```

That's a `PrintCertificateException`, from `GetPrinterAsync` or whichever call first reaches the printer. It derives from `PrintException`, and has the fingerprint in `CertificateFingerprint` and the address in `PrinterUri`, so an application can show them and ask whether to trust the printer:

```csharp
try
{
	IPrinter printer = await catalog.GetPrinterAsync(printerId);
}
catch (PrintCertificateException exception)
{
	// Show it, to be checked against the printer's own web page, before pinning it
	Console.WriteLine($"{exception.PrinterUri} presented a certificate with fingerprint {exception.CertificateFingerprint}.");
}
```

## Pin the printer's certificate

Check the fingerprint against one you get another way, then pin it:

- **The printer's own web page,** usually under security or certificates, or its front panel. This is the check that matters: it doesn't travel over the network you're guarding against.
- **`openssl`,** from a computer on the same network, which shows what the printer presents but can't tell you it's the printer:

  ```bash
  openssl s_client -connect 192.168.20.15:443 </dev/null | openssl x509 -noout -fingerprint -sha256
  ```

```csharp
ServiceCollection services = new ServiceCollection();

services.AddLatiprimere();
services.AddLatiprimerePdfium();
services.AddLatiprimereIpp(options =>
{
	options.PrinterUris.Add(new Uri("ipps://192.168.20.15:443/ipp/print"));

	// From the PrintCertificateException, checked against the printer's web page
	options.TrustedCertificateFingerprints.Add(fingerprint);

	// Sent to this printer only, and only over IPPS, never in clear text
	options.PrinterCredentials[new Uri("ipps://192.168.20.15:443/ipp/print")] = new NetworkCredential("office", password);
});
```

- **Fingerprints** are SHA-256, in hexadecimal, with or without colons, spaces or dashes, in either case. One that isn't 32 bytes throws `ArgumentException` when `AddLatiprimereIpp` is called.
- **A pinned certificate is trusted whatever name it carries,** since printers' certificates rarely name the address they're reached at.
- **The port:** an `ipps://` address without one means IPP's port, 631. Some printers take IPPS on another port, often 443: use the port the printer's web page or announcement gives.
- **A new certificate,** after a reset or a firmware update, is refused until you pin it too. Keep the old fingerprint until the printer has its new one, then remove it.
- **Pin it at one address** with `PrinterCertificateFingerprints[new Uri("ipps://192.168.20.15:443/ipp/print")] = fingerprint` instead. Printers of one model sometimes share a certificate made at the factory, and a fingerprint in `TrustedCertificateFingerprints` trusts every device that presents it. One pinned by address is refused at any other host, so pin a printer found on the network by the address it announces.

A certificate the computer already trusts, such as one your organisation's certificate authority issued to the printer, needs no pin. For any other rule, set `ServerCertificateValidationCallback`: it's asked about each certificate that isn't pinned and that the computer's trust isn't enough for, which, with a shared password, includes printers found on the network (see below). Return `true` only for a certificate you have checked.

## Passwords

`PrinterCredentials` gives one printer a user name and password, by its URI; `Credentials` gives one to every printer. Either way:

- **They go only over IPPS,** with every request, as HTTP Basic authentication, never in clear text. A printer that asks for a password over plain IPP gets a `PrintAuthenticationException` saying to use its `ipps://` address.
- **A printer's own credentials win** over `Credentials`. They're matched by the host and port in the URI, so give them the address the printer is reached at.
- **A user name** must be set, and can't contain a colon, which HTTP Basic uses to separate it from the password. A bad one throws `ArgumentException` when `AddLatiprimereIpp` is called. The domain of a `NetworkCredential` isn't used.
- **A password that's refused** throws a `PrintAuthenticationException` saying the printer refused it.
- **A printer that asks for a password** it isn't sent is refused with a `PrintAuthenticationException`. If the printer lists an `ipps://` address of its own, the message gives it, at the host you reached it by; if it asks for authentication Latiprimere can't give, such as a client certificate, the message says so.

### Who gets the shared password

`Credentials` goes to every printer reached over IPPS whose certificate is trusted. Anyone on the network can announce a printer, at a host of their choosing, with a certificate a public authority issued, which the computer trusts. So while `Credentials` is set, a certificate the computer trusts is enough only for printers you configured in `PrinterUris` or `PrinterCredentials`. A printer found on the network needs its certificate pinned, or accepted by the callback, before it gets the shared password; until then its IPPS address is refused, as above, with a message saying so.

If you pin several printers but have a password for only one, give it in `PrinterCredentials` rather than `Credentials`.

## Printers found on the network

Once a certificate is pinned or the callback is set, a printer found on the network that announces both an IPP and an IPPS address is reached over IPPS. If its certificate isn't trusted, or nothing answers at its IPPS address, it's reached over plain IPP instead, without any password, and that's logged as a warning. With neither set, printers found on the network are reached over plain IPP, as when IPPS wasn't supported. An IPPS address is only taken from an announcement by the same host as the printer's IPP one.

**A printer reached over IPPS once is never reached over plain IPP again** under its name. That holds if its IPPS address stops answering, its certificate changes, or its name is announced with only a plain address, any of which a device on the network could cause. It's refused with a `PrintException` instead, which says why. With `AddressStorePath` set, that's remembered across restarts. A printer that changes its certificate is refused until you pin the new one. To fall back as before, for printers whose documents needn't be kept private, set `AllowPlainIppAfterIpps`.

The fallback is still there for a printer that has never been reached over IPPS, so pinning one printer doesn't stop the others printing. To refuse plain IPP for every printer, set `RequireEncryption`:

```csharp
services.AddLatiprimereIpp(options =>
{
	// Only over IPPS: a printer that announces no IPPS address, or whose certificate isn't trusted, is refused
	options.RequireEncryption = true;
	options.TrustedCertificateFingerprints.Add(fingerprint);
});
```

With `RequireEncryption` set:

- **A printer found on the network is reached only at its IPPS address.** One that announces none, or whose certificate isn't trusted, is refused with a `PrintException`.
- **Looking up an `ipp://` URI is refused** without connecting.
- **An `ipp://` URI in `PrinterUris`** throws `ArgumentException` when `AddLatiprimereIpp` is called.
- **The system's trust is enough** for a certificate an authority issued, without pinning.

Even so, a device that announces the printer's name with a certificate you trust gets its jobs. For documents that must reach the real printer, print to its `ipps://` address. See [Anyone on the network can announce a printer](/docs/print-systems/network-printers/#anyone-on-the-network-can-announce-a-printer).

### Hosts are compared as written

A printer found on the network is reached at the IPv4 address it announces, such as `192.168.20.15`, not at a name such as `hp-envy.local`. Configure it in `PrinterUris` and `PrinterCredentials` with that same address. One configured by name isn't matched when it's looked up by its `ipp:dnssd:` ID, so it doesn't get its own credentials, and with `Credentials` set its certificate must be pinned. To use a name, look the printer up by the URI you configured instead.

## Not covered here

- **Windows queues** whose PDFs go straight to their printer over IPP take the same settings in `WindowsOptions`: `IppTrustedCertificateFingerprints`, `IppServerCertificateValidationCallback`, `IppCredentials` and `IppPrinterCredentials`. See [Printers over IPPS, with a password](/docs/print-systems/cups-and-windows/#printers-over-ipps-with-a-password).
- **Trust on first use,** pinning whatever certificate a printer presents the first time, isn't offered: it would trust whatever answered first.

The code on this page is in [SecurePrinters.cs](https://github.com/bananocraft-miner82/Latiprimere/blob/main/samples/Latiprimere.Examples/SecurePrinters.cs).
