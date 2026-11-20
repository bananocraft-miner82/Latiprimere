# Changelog

What changed in each release of Latiprimere, newest first. All the packages share one version number, so each entry covers all of them. The website shows this file at https://latiprimere.com/changelog/.

Each release is a heading in the form `## <version> - <yyyy-mm-dd>`, with `###` sections such as Added, Changed, Fixed, Removed and Known limitations. Work not yet released goes under `## Unreleased`, which the website doesn't show.

## 11.0.7-preview - 2026-11-20

The public API as it will be for 1.0 ([ADR 0040](docs/decisions/0040-api-review-1-0.md)).

### Added

- **Find a job again after a restart** with `IPrinter.GetJobAsync(jobId)`: store the job's `PrinterId` and `Id`, look the printer up, and ask it for the job, to follow or cancel it as before. It returns `null` for a job the print system no longer knows, as printers and CUPS forget finished jobs after a while, and Windows as soon as they have printed. Classes of your own that implement `IPrinter`, such as test fakes, need to add it.
- **`PrintCertificateException`,** a `PrintException` thrown when a network printer's certificate isn't trusted, with the fingerprint to pin in `CertificateFingerprint` and the printer's address in `PrinterUri`, so an application can ask whether to trust the printer without reading the message. Existing `catch (PrintException)` blocks still catch it.
- **Logging without dependency injection:** `CupsPrintSystemFactory.Create(options, loggerFactory)`, `WindowsPrintSystemFactory.Create(pdfRasterizer, options, loggerFactory)` and `IppNetworkPrintSystemFactory.Create(pdfRasterizer, options, loggerFactory)` create print systems that log through the logger factory given.

### Changed

- **Breaking: public names use .NET's spelling and abbreviations,** as the rest of the API and .NET itself do. Each keeps its value; rename it where your code uses it, and recompile code built against an earlier version:
  - `MediaSize.WidthMillimetres` and `HeightMillimetres` are now `WidthMillimeters` and `HeightMillimeters`;
  - `PrintJobState.Cancelled` is now `PrintJobState.Canceled`;
  - `RasterRotation.Anticlockwise90` is now `RasterRotation.Counterclockwise90`;
  - `VendorOption.MinimumValue` and `MaximumValue` are now `MinValue` and `MaxValue`, as in `PrinterCapabilities.MaxCopies`.

  Messages and documentation stay in British English.
- **Jobs that Windows sends straight to a queue's IPP printer have IDs starting `ipp:`,** such as `ipp:42`, so `GetJobAsync` can tell them from the spooler's job IDs. Looking one up when that printer can't be reached, or a WSD queue's printer can't be found, throws `PrintException`, as the job may still be there.
- **PDFium 160,** from 158, in Latiprimere.Rendering.Pdfium. PDFium reads the PDFs you print, so its updates often include security fixes.
- **IPP requests aren't redirected.** A printer or CUPS server that answers with an HTTP redirect is refused with that status, rather than the request going on to an address you didn't configure. Credentials were never sent on after a redirect.

### Fixed

- **A secure connection's error names the right certificate.** Once a printer's certificate had been refused, a later TLS failure at the same address for another reason, after the certificate was trusted, still quoted the old certificate's fingerprint.

## 11.0.6-preview - 2026-11-19

### Added

- **Printers found on the network are remembered across restarts** with the new `IppNetworkOptions.AddressStorePath`. A printer that stops answering mDNS while asleep is still found by name at its last address, as long as it still reports the UUID it announced. The file is JSON; a missing or damaged one is logged, never an error.

- **Secure printers over IPPS:** direct IPP can now trust a printer's own certificate and send it a password.
  - **Pinning:** add the certificate's SHA-256 fingerprint to `IppNetworkOptions.TrustedCertificateFingerprints`, or decide in `ServerCertificateValidationCallback`. A refused certificate's `PrintException` gives the fingerprint to pin.
  - **Passwords:** `IppNetworkOptions.Credentials`, or `PrinterCredentials` for one printer, are sent over IPPS only, never in clear text. A printer that asks for one over plain IPP is refused with a `PrintAuthenticationException` saying to use its `ipps://` address.
  - **Who gets the shared password:** while `Credentials` is set, a certificate the system trusts is enough only for printers configured in `PrinterUris` or `PrinterCredentials`. A printer found on the network needs its certificate pinned or accepted by the callback, since anyone on the network can announce a printer at a host with a public certificate.
  - **IPPS preferred once trusted:** with trust configured, printers found on the network that announce IPPS are reached over it, falling back to plain IPP if their certificate isn't trusted or nothing answers there.

- **Windows WSD queues print PDFs over IPP without configuration,** when `WindowsOptions.PrintPdfOverIpp` is on. A queue Windows set up itself on a WSD port records its printer's UUID. The printer announcing that UUID over mDNS, and confirming it when asked, gets the queue's PDF jobs straight over IPP, so each job reports how it ended. Before, such queues needed their address in `IppPrinterUris`.
  - **Printers that don't take PDF:** routed queues send PDFs to such printers as PWG raster or Apple raster, rendered with the Windows print system's rasteriser at the printer's resolution, limited by `WindowsOptions.MaxRenderDpi`. Before, they stayed on their driver.
  - **Finding the printer:** the first PDF job to a WSD queue listens for up to two seconds. A printer not found isn't searched for again for five minutes, and its jobs go through the driver as before.
- **PDFs to network printers that can't take PDF,** such as most inkjets, are sent as PWG raster or Apple raster: whichever the printer takes, PWG raster first. This needs Latiprimere.Rendering.Pdfium: register it with `AddLatiprimerePdfium()`, or pass a `PdfiumRasterizer` to the new `IppNetworkPrintSystemFactory.Create(pdfRasterizer, options)`. Without it, such a job is refused with `NotSupportedException` saying so, as before.
  - **Settings:** pages are rendered at the printer's default resolution, which the new `IppNetworkOptions.MaxRenderDpi` can limit. They're in colour or grey as `ColorMode` and the printer allow, on `MediaSize` or the printer's default paper. They're turned to fit unless `Orientation` is set, and two-sided back sides are turned as the printer asks.
  - **The upload:** pages are rendered while they upload, and gzipped when the printer offers it.
  - **A busy printer** is sent the pages again, rendered afresh, even from a stream that can't seek.
- **`RasterBand.Rotation`:** a band can ask for its page turned in quarter turns, with the new `RasterRotation` enum. PDFium renders the page turned, without extra memory. The default is no turn, so existing rasterisers and callers are unaffected; one that can't turn pages should throw `NotSupportedException` for any other value.

- **Print Tickets on Windows** with the vendor option `windows:print-ticket`: a Print Ticket in XML, such as one that asks for stapling or a driver's own quality setting. The printer's driver merges it onto its defaults, or onto a `windows:devmode` given with it, for PDF jobs, and resolves any conflicts as it does for its own settings. Options you set in `PrintOptions`, such as `MediaSize`, win over the ticket's. As with `windows:devmode`, the number of copies always comes from `Copies`, and the orientation from `Orientation` or else the document's first page. A value that isn't XML, or isn't a Print Ticket, is refused with `ArgumentException`, as is a ticket the driver finds malformed. Raw jobs ignore it, as they ignore `windows:devmode`.
- **Vendor options in the capabilities:** `PrinterCapabilities.VendorOptions` lists everything else a printer offers, such as print quality, resolution, finishing, media types, pages per sheet and a driver's own settings. Each comes with its name, its values (or limits, for a number) and its default, and the key and values to set it through `PrintOptions.VendorOptions` as they are. Settings the typed options cover aren't listed.
  - **CUPS and network printers** list the job attributes they accept, as `ipp:` keys, such as `ipp:print-quality` with Draft (`3`), Normal (`4`) and High (`5`).
  - **Windows** lists the driver's Print Capabilities: features as `windows:feature:` keys, such as `windows:feature:psk:PageOutputQuality` = `psk:High`, and parameters as `windows:parameter:` keys. A PDF job checks each against the printer's Print Capabilities before anything prints, and refuses one the printer doesn't offer with `ArgumentException`, saying what it does offer. A key for something a typed option sets for the same job is refused as well.
- **`IWindowsPrinter`**, which every Windows printer implements, reads the driver's Print Capabilities (`GetPrintCapabilitiesAsync`, optionally for the choices a Print Ticket makes) and its defaults as a Print Ticket (`GetDefaultPrintTicketAsync`), both as XML.
- **`ipp:printer-resolution`** and other resolution attributes, whose names end in `-resolution`, take values such as `600dpi`, `600x1200dpi` or `118dpcm`.
- **Logging:** with logging registered in dependency injection, the CUPS, IPP and Windows print systems log what they carry on without, such as vendor options that couldn't be read, under the categories `Latiprimere.Ipp` and `Latiprimere.Windows`.

### Changed

- **Network printers check a job's settings before the document is sent.** A job asking for something the printer can't do, such as two-sided printing on a one-sided printer, is refused with Validate-Job before its document is uploaded, with the same `PrintException` and IPP status as before. It's one short request per job, sent only to printers that list Validate-Job, and a printer too busy to answer it gets the job anyway. `IppNetworkOptions.ValidateJobs = false` turns it off. Windows queues routed straight to their IPP printer check the same way.
- **`AddLatiprimereIpp`, `AddLatiprimereCups` and `AddLatiprimereWindows` check their options when called,** with the same `ArgumentException` as before, rather than when the catalog is first resolved. A bad fingerprint, credential, CUPS server or IPP printer URI is now reported where it was configured, instead of failing the whole catalog later.

### Fixed

- **A Windows queue routed over IPP whose printer stops answering is found again.** Once its printer had been found, a moved or switched-off printer made every later PDF job wait for the connection to time out before the driver printed it. Now the first such job forgets the printer, and the next one looks for it afresh, by its UUID for a WSD queue.
- **`cups:..` and `cups:.` find no printer.** No queue has those names, and `..` reached the CUPS server itself, which prints to its default destination.

- **A configured printer that also announces itself is listed once,** under its configured identifier, when its URI in `IppNetworkOptions.PrinterUris` leaves out IPP's port 631. Before, it was listed twice: by URI and by name.
- **PDFs printed as raster are never rendered above 2400 dpi,** nor as pages more than 100,000 pixels across or down, whatever the printer reports. A printer listing only higher resolutions, or a paper too large, is refused with `NotSupportedException` instead of rendering without end.

## 11.0.5-preview - 2026-11-17

### Changed

- **Monochrome PDF jobs print grey on Windows,** whatever the driver: with `PrintOptions.ColorMode` set to monochrome, a PDF job's pages are made grey before the printer's driver gets them, since some drivers, Microsoft Print to PDF among them, ignore the setting.
- **PDFs to printers with the Generic / Text Only driver are refused** on Windows with `NotSupportedException`, before anything is queued. That driver can't print the page images a PDF is printed as: the job ended in an error and stopped the printer's queue. Send it raw data, such as plain text, instead. A PDF that goes straight to the queue's IPP printer isn't affected.

### Fixed

- **Finding the Windows default printer no longer fails** with "The data area passed to a system call is too small" when the default changes while it is read. With "Let Windows manage my default printer" on, Windows makes the last printer used the default, so printing from another program at the same moment could cause it.

## 11.0.3-preview - 2026-11-16

### Added

- **Choose the paper size, colour mode and tray** with `PrintOptions.MediaSize`, `PrintOptions.ColorMode` and `PrintOptions.InputTray`. Pass values from the printer's capabilities. On Windows, a size or tray the printer's driver doesn't list is refused with `NotSupportedException`, rather than printed on the default paper. Raw jobs on Windows don't apply them, as with duplex: the data chooses its own paper.
- **Limit the resolution Windows renders PDFs at** with `WindowsOptions.MaxRenderDpi`, set through `AddLatiprimereWindows` or `WindowsPrintSystemFactory.Create`. A lower limit makes PDF jobs faster and lighter on memory, with softer small text. By default, pages are still rendered at the printer's own resolution.
- **Vendor options** with `PrintOptions.VendorOptions`, for settings the other options don't cover, such as print quality or stapling. Keys start with the print system that reads them:
  - `ipp:` sends an IPP attribute to CUPS and network printers, such as `ipp:print-quality` = `5`;
  - `cups:` sends a CUPS option to CUPS only;
  - `windows:devmode` gives the Windows spooler a DEVMODE saved from the printer's driver, in base64, for PDF jobs.

  These settings aren't portable between print systems. A key that repeats a typed option, such as `ipp:copies` with `Copies`, is refused.
- **PDFs straight to IPP printers behind Windows queues,** with `WindowsOptions.PrintPdfOverIpp` (off by default). A PDF job goes to the printer over IPP, as it is, when Latiprimere knows the queue's IPP address and the printer accepts PDF. That's faster and sharper than rendering pages through the driver, and the job reports how it really ended. The address is known when the queue was added by URL, or when you give it in `WindowsOptions.IppPrinterUris`. Such jobs bypass the Windows queue; every other job goes through it as before.

### Changed

- **`Latiprimere.Windows` now depends on `Latiprimere.Ipp`,** for sending PDFs straight to IPP printers.
- **Version numbers now follow .NET:** `<.NET major>.0.<build>`, starting at 11.0, the newest .NET the packages target. This release follows 0.1.0-preview. Releases stay previews, ending in `-preview`, until testing with real printers is done. Every package still supports .NET 10 and .NET 11.

## 0.1.0-preview - 2026-11-15

The first preview, for evaluation. The API may change before 1.0.

### Added

- **Six packages:** `Latiprimere`, `Latiprimere.Abstractions`, `Latiprimere.Cups`, `Latiprimere.Windows`, `Latiprimere.Rendering.Pdfium` and `Latiprimere.Ipp`, for .NET 10 and .NET 11. All are trimming and Native AOT compatible.
- **One printer catalog** across every print system you register: list printers, find one by ID, and get the default printer.
- **Printer capabilities:** paper sizes, duplex, colour, trays and copies.
- **Printing** PDF documents, or raw data in the printer's own language, such as ZPL or ESC/POS.
- **Following jobs** from pending to completed, and cancelling them.
- **CUPS** on Linux and macOS, or any CUPS print server, including servers that need a user name and password.
- **The Windows print spooler,** with PDFs rendered by PDFium.
- **Direct IPP** to network printers, found by mDNS or given by URI. Jobs a busy printer turns away are sent again.

### Known limitations

- macOS hasn't been checked on real hardware yet.
