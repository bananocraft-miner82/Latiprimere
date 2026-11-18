# 0035: Printing PDFs as raster over IPP

- Status: Accepted
- Date: 2026-11-18

## Context

A PDF to a direct IPP printer that doesn't list `application/pdf` in `document-format-supported` was refused with `NotSupportedException`. Many printers are like that, such as the HP ENVY 5640, which takes Apple raster and PCLm but not PDF. [ADR 0034](0034-raster-encoders.md) built PWG raster and Apple raster encoders that stream an `IRasterDocument`. P3-3 in [Phase 3's plan](../phase-3-plan.md) uses them to print, with decisions D2 (format order) and D3 (resolution) accepted.

## Decision

- **When:** a PDF job, to a direct IPP printer whose formats are known and lack PDF, but include PWG raster or Apple raster. Raw jobs are never rendered. CUPS queues are unaffected: CUPS converts documents itself, so their formats aren't checked. Windows queues routed to IPP still fall back to the spooler for such printers until P3-4.
- **The rasteriser** is the application's `IPdfRasterizer`, as for Windows:
  - from dependency injection (`AddLatiprimerePdfium()`);
  - or passed to a new public overload, `IppNetworkPrintSystemFactory.Create(IPdfRasterizer?, IppNetworkOptions?)`. Both its parameters are required, so `Create(null)` stays unambiguous. The analyser's rule that the overload with optional parameters must have the most parameters (RS0027) is suppressed on the shipped `Create(IppNetworkOptions? = null)`, with that reason.

  Without a rasteriser, the job is refused with `NotSupportedException` naming Latiprimere.Rendering.Pdfium, before anything is sent.
- **Format:** PWG raster, then Apple raster (D2). PCLm isn't built (D1).
- **Settings,** read from the printer when the job is submitted, in one Get-Printer-Attributes request for:
  - `pwg-raster-document-resolution-supported`, `pwg-raster-document-type-supported` and `pwg-raster-document-sheet-back`;
  - `urf-supported`, whose keywords such as `RS300-600`, `W8`, `SRGB24` and `DM3` describe Apple raster;
  - `printer-resolution-default`, `media-default`, `sides-default`, `print-color-mode-default` and `compression-supported`.

  It's read raw, since SharpIppNext doesn't model `urf-supported`.
  - **Resolution (D3):** the printer's default when it lists it for the format and it's within the new `IppNetworkOptions.MaxRenderDpi`. Otherwise the highest it lists within that limit and 600 dpi, and if none is that low, the lowest. Only square resolutions are used. Unlike Windows, which renders below the printer's resolution and lets the driver scale up, raster must be sent at a resolution the printer lists.
  - **Colour:** sRGB when `ColorMode.Color` is asked for, or the printer's default isn't monochrome, and it takes sRGB. Otherwise 8-bit grey. 1-bit black is used when that's all it takes, or its default is bi-level. A printer that takes only sRGB gets sRGB.
  - **Paper:** `MediaSize`, or the printer's `media-default` read as a PWG name. A default without dimensions, such as `roll_current`, is refused unless `MediaSize` is set.
  - **Sides and back sides:** `Duplex`, or `sides-default`; back sides turned as the printer's `pwg-raster-document-sheet-back` or `DM1`–`DM4` says (ADR 0034).
  - **Orientation** follows the PDF's pages unless `Orientation` is set, as on Windows. `orientation-requested` isn't sent, since the pages are already turned and a printer mustn't turn them again.
  - **Copies,** sides, colour mode, paper and vendor options are sent as for a PDF; the printer makes the copies.
- **Page size in pixels and points** is worked out as libcups does it: from whole hundredths of a millimetre, rounded down, so A4 at 600 dpi is 4,960 × 7,015. ADR 0034's encoder rounded to the nearest pixel, which made A4 one pixel wider (4,961) than what CUPS and Apple send.
- **Streaming:**
  - The PDF is opened once, before Validate-Job, so a PDF that can't be read fails before anything is sent.
  - Each attempt renders on its own thread into a pipe, which is Print-Job's document. The upload is chunked, because SharpIppNext sends a stream that can't seek without a length. Rendering pauses when 1 MiB is waiting, so a job holds at most a band (16 MiB) and the pipe.
  - With `compression-supported` including `gzip`, the upload is gzipped (fastest level) and sent with `compression=gzip`. SharpIppNext doesn't compress, so Latiprimere does.
- **Failures:**
  - If rendering fails, its exception is thrown, not the upload failure it caused.
  - Cancellation stops the rendering between bands.
  - After any attempt, rendering is stopped and waited for before the next attempt or the return, so the document is never rendered on two threads at once.
- **Busy printers** ([ADR 0017](0017-retry-busy-ipp-printers.md)): each resend renders the pages again from the start, so a raster job can be resent even when the PDF's stream can't seek. `IppPrinter`'s retry loop now takes an `IppJobDocument` that knows whether it can be resent, and how: a stream rewound, or pages rendered again.
- **Logging:** each raster job logs its format, colour space, resolution and compression at Debug.

## Consequences

- **Every IPP Everywhere and AirPrint printer** can be sent PDFs by direct IPP, and the job is followed to how it ended, as any IPP job.
- **Public API:** `IppNetworkOptions.MaxRenderDpi`, and the `IppNetworkPrintSystemFactory.Create` overload with a rasteriser. `AddLatiprimereIpp()` picks up a registered rasteriser without changes.
- **Cost:** an A4 page in colour at 600 dpi is about 100 MB of pixels before compression. Rendering it takes seconds, not milliseconds. `MaxRenderDpi = 300` quarters the pixels.
- **Testing:**
  - **Planning:** the HP's real attributes, `ippeveprinter`'s, and each rule for resolution, colour, paper, sides and back side.
  - **The printer with a fake client:** the document is decoded page by page. Also checked: gzip, Validate-Job with the raster format, a busy resend carrying the same whole document, a rendering failure thrown as itself, and refusals without a rasteriser or a raster format.
  - **Harness scenarios on the scripted printer, over real HTTP:**
    - `ippeveprinter`'s profile without PDF gets three PWG raster pages at its default 600 dpi;
    - the HP's profile gets three Apple raster pages, colour, two-sided, gzipped;
    - a busy printer gets the same raster three times;
    - without PDFium, nothing is sent.
  - **The real `ippeveprinter` in CI:** a PDF through `IppPrinter`, with the printer's formats given without PDF, so the real client streams gzipped PWG raster that `ippeveprinter` must read to complete the job.
- **Not done:**
  - **The manual check on the HP** (a test page, colour and grey, A4, two-sided) waits for the printer and the owner's go-ahead, since it prints paper (D9).
  - **Printer margins** are still not applied (ADR 0034).
