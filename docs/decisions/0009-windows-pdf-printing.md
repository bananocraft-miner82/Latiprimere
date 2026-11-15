# 0009: PDF printing on Windows through PDFium and GDI

- Status: Accepted
- Date: 2026-11-11

## Context

The Windows spooler can't take PDF. Spike 3 tested rasterising PDF pages with PDFium and drawing them to the printer through GDI, the highest-risk route in the plan.

## Decision

- **Rasteriser contract in Abstractions:** `IPdfRasterizer` opens a document and `IRasterDocument` renders a horizontal **band** of a page into a caller-supplied buffer (32-bit BGRx, top-down, white background), plus `PageSize` and `RasterBand`. Adapters depend on the contract, not on PDFium.
- **`Latiprimere.Rendering.Pdfium`** implements it with PDFium (bblanchon/pdfium-binaries 158.0.8125; Windows, Linux and macOS natives). All PDFium calls are serialised behind one process-wide lock, because PDFium isn't thread-safe. The document bytes are copied to native memory for the document's lifetime. The current page stays loaded between bands. `AddLatiprimerePdfium()` registers it.
- **Optional dependency:** the Windows adapter takes the rasteriser from DI (or `WindowsPrintSystemFactory.Create(rasterizer)`). Without one, PDF throws `NotSupportedException` explaining how to add it; raw printing works regardless.
- **GDI path:**
  - DEVMODE comes from the driver via `DocumentProperties`; copies (collated), duplex and orientation are applied and merged back through the driver.
  - Orientation follows the first page when the caller leaves it at the default.
  - Copies are rendered manually if the driver reports it can't make them.
  - Each page is drawn actual size if it fits, otherwise shrunk to fit, centred, and offset by the unprintable margin.
  - Pages are rendered in bands of at most 16 MB and drawn with `StretchDIBits`.
  - Cancellation is checked between pages, and a failed or cancelled job is aborted.
- **Render resolution:** device resolution by default. `GdiRasterPrinter` accepts an internal cap (`maxRenderDpi`); GDI scales the bands up, with band edges mapped so they meet exactly.
- **`PrintOptions.OutputFile`:** prints to a file instead of the device. Needed for virtual printers such as Microsoft Print to PDF in applications without a user interface. The Windows adapter honours it for raw and PDF jobs. IPP/CUPS throw `NotSupportedException` rather than ignore it.

## Measurements (Microsoft Print to PDF, 600 DPI, A4 mixed-content pages)

| Run | Time | Peak working set | Output |
|---|---|---|---|
| 1 page | 2.0 s (includes start-up) | 225 MiB | 2.2 MB |
| 100 pages, device resolution | 66 s (0.66 s/page) | 225 MiB | 13 MB |
| 100 pages, render capped at 300 DPI | 36 s | 130 MiB | 44 MB |
| PDFium rendering alone, 100 pages at 600 / 300 DPI | 4.6 s / 1.2 s | 45 MiB | — |

Memory stays flat as page count grows. About 93% of the time and most of the memory belong to the driver (Microsoft Print to PDF converts GDI output in-process), not to PDFium or Latiprimere.

## Consequences

- Phase 0 target met: visually correct output, and a 100-page document well under 500 MB peak.
- Real printer drivers will perform differently from Print to PDF; measure on real hardware in Phase 2.
- Pages with mixed orientation all use the first page's orientation; switching per page needs `ResetDC`, which isn't implemented.
- Output is raster: text isn't selectable in PDFs produced this way, and files are larger than vector output.

## Owner decisions (2026-11-11)

1. **`PrintOptions.OutputFile` stays in the cross-platform options.** Print systems that can't honour it throw `NotSupportedException`.
2. **Full device resolution stays the default.** The render cap becomes configurable later; tracked on the Phase 2 checklist in [plan.md](../plan.md). For reference: a 300 DPI cap halved time and memory with Microsoft Print to PDF, at the cost of slightly softer small text and, from that driver, 3.4× larger files.
