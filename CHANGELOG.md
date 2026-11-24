# Changelog

What changed in each release of Latiprimere, newest first. All the packages share one version number, so each entry covers all of them. The website shows this file at https://latiprimere.com/changelog/.

Each release is a heading in the form `## <version> - <yyyy-mm-dd>`, with `###` sections such as Added, Changed, Fixed, Removed and Known limitations. Work not yet released goes under `## Unreleased`, which the website doesn't show.

## 11.0.10-preview - 2026-11-24

### Added

- **Print password-protected PDFs** with `PrintContent.FromPdf(stream, password)`. Where Latiprimere renders the PDF, on the Windows spooler or as raster, it opens it with the password. A network printer that takes PDF is sent the password with the document when it lists `document-password-supported`, and otherwise the pages as raster. A CUPS queue that takes no passwords refuses the job with `NotSupportedException`. The password is never logged ([ADR 0054](docs/decisions/0054-rasteriser-options.md)).
- **Render in grey, and without smoothing:** `RasterBand.PixelFormat` chooses `RasterPixelFormat.Bgra32` or `Gray8`, a quarter of the memory, and `RasterRenderOptions` turns off the smoothing of text, lines and images, for printers that print only black dots. `PdfOpenOptions` gives a PDF's password to the rasteriser ([ADR 0054](docs/decisions/0054-rasteriser-options.md)).
- **`IPrinter.AppliedOptions`** says which `PrintOptions` settings a printer applies, as `PrintOptionKinds` flags. A printer from the catalog refuses a job that sets any other with `NotSupportedException`, before anything is sent, so a setting added in a later version is never dropped without a word by a print system that doesn't know it. Every print system applies every setting there is today ([ADR 0055](docs/decisions/0055-catalog-printers.md)).
- **More print options** ([ADR 0056](docs/decisions/0056-typed-options-before-1-0.md)):
  - `PrintOptions.PageRanges`, of `PageRange`s, to print some pages: only those are rendered on the Windows spooler and as raster, and IPP printers sent the PDF are asked for them with `page-ranges`;
  - `Collate`, `Quality` (`Draft`, `Normal`, `High`), `Finishings` (staples, holes and a fold), `OutputBin` and `MediaType`;
  - custom and roll paper: `MediaSize.Custom(width, height)`, inside a printer's `PrinterCapabilities.CustomMediaSizes`;
  - `PrinterCapabilities.CustomMediaSizes`, `MediaTypes`, `PrintQualities`, `DefaultPrintQuality`, `Finishings`, `OutputBins` and `SupportsPageRanges`.

  The Windows spooler applies all but finishings and output bins, which a Windows printer refuses rather than ignores.
- **`IPrinter.GetFeature<T>()` and `IPrintJob.GetFeature<T>()`** give what a print system's printer or job offers beyond the interface, such as `IWindowsPrinter` ([ADR 0055](docs/decisions/0055-catalog-printers.md)).
- **Documentation and samples:** the guides show printing some of a PDF's pages, following a printer, and PDF labels on a custom paper size. The CLI sample's `print` takes `--pages` and `--quality`, and opens a password-protected PDF with the password in `LATIPRIMERE_PDF_PASSWORD`; its `caps` lists custom sizes, media types, qualities, finishings, output bins and whether page ranges work.

### Changed

- **IPP printers no longer list `ipp:print-quality`, `ipp:output-bin`, `ipp:media-type` or `ipp:multiple-document-handling` among their vendor options,** as the typed `Quality`, `OutputBin`, `MediaType` and `Collate` set them on every print system. The keys still work on their own. `ipp:finishings` is still listed, as it holds far more than the `Finishing` names ([ADR 0057](docs/decisions/0057-api-review-phase-4.md)).
- **Breaking: the catalog's printers are its own,** around the print system's, so `printer is IWindowsPrinter` is now false. Use `printer.GetFeature<IWindowsPrinter>()` ([ADR 0055](docs/decisions/0055-catalog-printers.md)).

- **Grey and black jobs use a quarter of the memory:** IPP raster in grey or black, and monochrome jobs on the Windows spooler, are rendered in 8-bit grey instead of 32-bit colour. Grey comes from PDFium, whose weighting of colours differs from before by a few levels at most ([ADR 0054](docs/decisions/0054-rasteriser-options.md)).
- **Breaking, for classes implementing the rasteriser:** `IPdfRasterizer.Open` takes `PdfOpenOptions? options = null`, and `IRasterDocument.RenderBand` takes `RasterRenderOptions? options = null`. Code calling them compiles as before. The static `RasterBand.BytesPerPixel` is now an instance property, from the band's pixel format; `RasterBand.GetBytesPerPixel(format)` gives it for any format ([ADR 0054](docs/decisions/0054-rasteriser-options.md)).

### Fixed

- **Printers found over mDNS that announce no IPv4 address can be reached.** Latiprimere used only the first part of their announced host name, such as `office-laser` rather than `office-laser.local`, which rarely resolves and which printers that guard against DNS rebinding refuse: ippeveprinter answers 400 Bad Request. The full name is used now, and an announced IPv6 address that isn't link-local before it.

## 11.0.9-preview - 2026-11-21

### Added

- **A software bill of materials in every package,** in CycloneDX format at `sbom/<package>.cdx.json`, listing what the package is built from ([ADR 0051](docs/decisions/0051-security-policy-sbom-and-cra.md)).
- **A security policy:** [SECURITY.md](SECURITY.md) says how to report a vulnerability privately, through GitHub or to security@latiprimere.com, how quickly reports are answered, and which versions get security updates ([ADR 0051](docs/decisions/0051-security-policy-sbom-and-cra.md)).

### Changed

- **Remembered network printers are limited:** at most 256 printers are kept in `IppNetworkOptions.AddressStorePath`, and one not seen for 90 days is forgotten, so made-up mDNS announcements can't fill the file or stay trusted forever. A forgotten printer is found again by searching. Each entry now records when it was last seen; the file stays at format 1, so earlier versions still read it ([ADR 0038](docs/decisions/0038-remembered-addresses.md)).

### Fixed

- **A very wide PDF page can't get past the rendering size check:** Latiprimere.Rendering.Pdfium now works out the size of a band of pixels without overflowing.
- **A PDF document that wasn't disposed no longer holds up the finalizer thread** while another thread is using PDFium: it is closed at the next PDFium call instead.

## 11.0.8-preview - 2026-11-21

### Added

- **Follow a printer's state:** `IPrinter.GetStatusAsync` reads it, as a new `PrinterStatus` with its state, message, IPP reasons such as `media-empty-error` or `paused`, and whether it accepts jobs. `IPrinter.WatchStatusAsync`, or the `PrinterExtensions.WatchStatusAsync` extension, reports each change until cancelled; a state that can't be read, as when the printer is off, is reported as `Unknown` with why, and the watch goes on. On Windows the spooler's status flags are given as the IPP reasons that say the same ([ADR 0049](docs/decisions/0049-following-printers-and-jobs.md)).
- **Print plain text** with `PrintContent.FromText(string)`, or `FromText(stream, encoding)` for text in any encoding. CUPS lays it out itself. Elsewhere Latiprimere lays it out as a PDF: Courier at 10 points within half-inch margins, long lines wrapped at a space, tabs every 8 columns, and a form feed starting a new page. That shows the Windows-1252 characters, which cover Western European languages; text with any other is refused with `NotSupportedException` naming the character and where it is ([ADR 0048](docs/decisions/0048-printing-plain-text.md)).
- **Print JPEG and PNG images** with `PrintContent.FromJpeg` and `PrintContent.FromPng`, each on one page, on every print system. `PrintOptions.Scaling` sizes them: `Fit`, `Fill`, `None`, or by default shrunk only if larger than the paper. They are turned as their EXIF orientation says, printed landscape when wider than tall unless `Orientation` says otherwise, and a PNG's transparency prints as white. Images up to 300 million pixels, and 100,000 a side, can be printed. CUPS converts them itself; a network printer that takes JPEG gets a JPEG as it is; otherwise, and on Windows, the image is placed on a page as a PDF, which is printed as PDFs are ([ADR 0047](docs/decisions/0047-printing-images.md)).
- **`PrinterCapabilities.DocumentFormats`** lists the document formats a printer can be sent, whether it takes them itself or Latiprimere converts them.
- **PDFs to printers that take PCLm,** Mopria's raster format, for network printers that take neither PDF, PWG raster nor Apple raster. Pages are sent as image strips as tall as the printer prefers, compressed with Flate or run-length encoding, whichever it prefers ([ADR 0046](docs/decisions/0046-pclm-and-raster-format-order.md)).
- **Choose the raster formats PDFs are rendered into,** and their order, with `IppNetworkOptions.RasterFormats`, and `WindowsOptions.IppRasterFormats` for Windows queues printing over IPP: by default PWG raster, then Apple raster, then PCLm, so printers that take PWG or Apple raster get what they got before. An empty list turns rendering to raster off.
- **Windows queues printing PDFs over IPP can use IPPS with a printer's own certificate and a password:** `WindowsOptions.IppTrustedCertificateFingerprints`, `IppServerCertificateValidationCallback`, `IppCredentials` and `IppPrinterCredentials`, which work as their `IppNetworkOptions` counterparts do. Until a printer's certificate is trusted, its queue's jobs go through the driver as before, and the fingerprint to pin is now logged as a warning. Once trust is configured, a WSD queue's printer that announces an IPPS address is reached there first ([ADR 0042](docs/decisions/0042-ipps-for-windows-queues-and-uri-authentication.md)).
- **Documentation: anyone on the network can announce a printer.** The network printers guide says what discovery can and can't guard against, and how to make sure a confidential document reaches the real printer: its `ipps://` address with its certificate pinned.
- **A network printer that stops taking a job fails it, rather than holding it forever.** If a printer goes `IppNetworkOptions.UploadStallTimeout` (5 minutes by default) without taking any more of a job's document, or without answering once it has been sent it all, `SubmitAsync` throws a `PrintException` saying which. Previously only your own cancellation token ended the wait. A printer that keeps reading, however slowly, is never cut short, and the time spent rendering a PDF as raster isn't counted. `Timeout.InfiniteTimeSpan` restores the old behaviour. Windows queues printing over IPP get the default ([ADR 0041](docs/decisions/0041-upload-stall-timeout.md)).

- **Why a job is in its state:** `PrintJobStatus.StateReasons` gives the IPP `job-state-reasons` keywords that network printers and CUPS report, such as `job-canceled-at-device`, `aborted-by-system` or `printer-stopped`. When the printer gives no message of its own, `StateMessage` now takes CUPS's own message for the job, such as "Printer cannot print with supplied options.", or else explains the most telling reason, so a job CUPS cancels because its printer can't carry out an option no longer ends with no explanation. `WatchStatusAsync` reports a change of reasons as a change of status ([ADR 0043](docs/decisions/0043-job-state-reasons.md)).

### Changed

- **`WatchStatusAsync` is a member of `IPrintJob`,** with a default implementation that polls as the extension did, so existing code keeps working. On Windows, jobs and printers are now followed by the spooler's change notifications, so a change is reported as it happens, rather than at the next poll, and an idle watch sends no requests. CUPS and network printers are still polled: CUPS answers IPP notification requests at once with "ask again in a minute", so notifications wouldn't be quicker. Classes of your own that implement `IPrinter` or `IPrintJob` keep compiling; call the new members through the interface type ([ADR 0049](docs/decisions/0049-following-printers-and-jobs.md)).
- **`DocumentFormat`, `ColorMode`, `PageOrientation` and `VendorOptionKind` may gain values** in later versions, and their documentation says how to treat one you don't know. The other enums follow IPP's own states and modes, and won't ([ADR 0044](docs/decisions/0044-room-to-grow-after-1-0.md)).
- **A document format IPP printing doesn't know** throws `NotSupportedException` from `SubmitAsync`, as documented, rather than `ArgumentOutOfRangeException`. No format Latiprimere offers today reaches it.
- **What a printer says it asks for is read,** from `uri-authentication-supported`. A Windows queue whose IPP printer asks for a password it won't be sent, or for authentication other than a user name and password, has its PDF jobs printed through the driver, rather than failing with `PrintAuthenticationException` when the printer refuses them. When a network printer printed to directly refuses a job for want of a password, the message now gives its `ipps://` address if it lists one, or says it asks for authentication Latiprimere can't give.

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
