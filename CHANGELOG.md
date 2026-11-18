# Changelog

What changed in each release of Latiprimere, newest first. All the packages share one version number, so each entry covers all of them. The website shows this file at https://latiprimere.com/changelog/.

Each release is a heading in the form `## <version> - <yyyy-mm-dd>`, with `###` sections such as Added, Changed, Fixed, Removed and Known limitations. Work not yet released goes under `## Unreleased`, which the website doesn't show.

## Unreleased

### Added

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
