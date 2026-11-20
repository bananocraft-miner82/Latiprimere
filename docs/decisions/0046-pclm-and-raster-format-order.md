# 0046: PCLm, and the order of raster formats

- Status: Accepted (owner, 2026-11-20: "Accept ADR 0046")
- Date: 2026-11-20

## Context

A PDF sent to an IPP printer that can't take PDF is rendered by PDFium and sent as raster ([ADR 0035](0035-printing-pdfs-as-raster.md)). Phase 3 wrote PWG raster and Apple raster, and left PCLm for later. PCLm is Mopria's format, and many printers that take neither of the others take it. It's a restricted PDF: a `%PCLm 1.0` comment on the second line, and each page the paper's whole size, drawn as image strips from the top down.

The HP ENVY 5640 takes Apple raster and PCLm: 300 or 600 dpi (600 by default), strips of 32 lines, compression `rle`, `jpeg` and `flate` in that order of preference, and back sides `rotated`.

Phase 4's plan ([phase-4-plan.md](../phase-4-plan.md), accepted) decided:

- **D2:** strips compressed with Flate or run-length, whichever the printer prefers of the two. Not JPEG: .NET has no JPEG encoder, and JPEG would blur text;
- **D3:** `IppNetworkOptions.RasterFormats` sets the order raster formats are tried in, PWG raster, Apple raster, then PCLm by default, and Windows queues sent over IPP use the same order.

The PDF writer, [ADR 0045](0045-pdf-writer.md), was P4-1. This is P4-2. Owner: "Start P4-2".

## Decision

- **`PclmWriter`,** a third `RasterWriter` beside the PWG and Apple raster writers, built on the PDF writer:
  - a PDF 1.7 file whose second line is `%PCLm 1.0`;
  - each page the paper's size in points, from its size in pixels at the resolution;
  - lines laid out, converted and turned for back sides by the same encoder as the other formats, gathered into strips of the chosen height and written as each fills. The last strip of a page may be shorter. Each strip is an 8-bit `DeviceGray` or `DeviceRGB` image, compressed on its own, drawn at its place from the top;
  - so a page is never held whole: a strip of 32 lines of A4 in colour at 600 dpi is under half a megabyte;
  - PCLm has no 1-bit form, so 1-bit black is refused with an `ArgumentException`. The planner never asks for it.
- **The planner** reads the printer's PCLm attributes:
  - **resolution:** from `pclm-source-resolution-supported`, the default from `pclm-source-resolution-default` and then `printer-resolution-default`, capped by `MaxRenderDpi`, as for the other formats;
  - **colour:** sRGB or grey, chosen as for Apple raster;
  - **strip height:** `pclm-strip-height-preferred`, else the lowest `pclm-strip-height-supported`, else 16, Mopria's default. A height above 256 lines is ignored, so a printer can't ask for strips the size of a page;
  - **compression:** the first of `flate` and `rle` in `pclm-compression-method-preferred`, else Flate;
  - **back sides:** `pclm-raster-back-side`;
  - **gzip** when `compression-supported` lists it, as for the other formats.
- **The order of raster formats:**
  - **`IppNetworkOptions.RasterFormats`,** a list of the new public enum `IppRasterFormat` (`PwgRaster`, `AppleRaster`, `Pclm`), PWG raster, Apple raster, then PCLm by default. A PDF goes as the first of these the printer takes. Repeats and values that aren't in the enum are ignored;
  - **`WindowsOptions.IppRasterFormats`,** the same for Windows queues whose PDFs are sent over IPP;
  - **an empty list** turns raster off. For direct IPP, a PDF to a printer that can't take PDF is then refused with `NotSupportedException`. When the printer takes a raster format the list leaves out, the message names the formats and the option. A Windows queue prints such PDFs through its driver instead, as it does for any printer that takes no raster format Latiprimere writes.
- **Not done:**
  - **JPEG strips** (D2);
  - **Mopria's optional parts,** such as PCLm's own job-level comments: the HP's sign-off will show whether a printer needs them.

## Consequences

- **Printers that take PCLm and neither other raster format can now print PDFs.** The HP still gets Apple raster by default; `RasterFormats` with PCLm first sends it PCLm, which is how its sign-off tries it.
- **New public API:** `IppRasterFormat`, `IppNetworkOptions.RasterFormats` and `WindowsOptions.IppRasterFormats`. It's additive, and its default keeps today's behaviour.
- **Tests:**
  - **the strips decoded as a printer decodes them,** from their Flate or run-length streams rather than by rendering, and compared line by line with PWG raster of the same document: grey and colour, Flate and run-length, strips that don't divide the page, a strip taller than the page, and two-sided pages with rotated backs;
  - **PDFium reads the PCLm back** and draws the strips where they belong. PDFium smooths images it scales, so its pixels are within 2.6 levels on average rather than exact. Strips 32 lines out of place would be 5 to 19 levels off;
  - **the planner:** the HP's attributes, the fallbacks for strip height and compression, the 256-line cap, and the format order;
  - **the scripted printer:** a PCLm-only printer, PCLm first in the order, and an empty order refused with nothing rendered or sent;
  - **two harness scenarios:** `ipp.raster.pclm-pages` (the HP's profile less Apple raster: three US Letter pages at 600 dpi, each 207 run-length strips) and `ipp.raster.format-order`;
  - **Native AOT:** the native check runs `ipp.raster.pclm-pages` on every runner.
- **A printer may read PCLm more strictly than PDFium does.** The writer follows Mopria's layout (the comment, strips from the top, each its own image and compressed on its own), but only the HP's paper will show that a printer accepts it. That's in the sign-off.
