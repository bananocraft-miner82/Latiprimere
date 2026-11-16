# Changelog

What changed in each release of Latiprimere, newest first. All the packages share one version number, so each entry covers all of them. The website shows this file at https://latiprimere.com/changelog/.

Each release is a heading in the form `## <version> - <yyyy-mm-dd>`, with `###` sections such as Added, Changed, Fixed, Removed and Known limitations. Work not yet released goes under `## Unreleased`, which the website doesn't show.

## Unreleased

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
