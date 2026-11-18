# 0034: Raster encoders for printers without PDF

- Status: Accepted
- Date: 2026-11-18

## Context

Many IPP printers don't take PDF; the HP ENVY is one. Every IPP Everywhere printer must take PWG raster (`image/pwg-raster`, PWG 5102.4), and every AirPrint printer Apple raster (`image/urf`). Between them they cover nearly every network printer sold in the last decade. [Phase 3's plan](../phase-3-plan.md), P3-2 and decision D1: PWG raster and Apple raster now, and PCLm only if a printer needs it.

`IRasterDocument.RenderBand` already renders a PDF page in BGRA bands, for the Windows GDI path ([ADR 0009](0009-windows-pdf-printing.md)). An A4 page at 600 dpi is about 100 MB unencoded, so the encoders mustn't hold a page whole.

## Decision

- **Internal encoders in `Latiprimere.Ipp/Raster`.** Only IPP uses these formats, and `Latiprimere.Ipp` already references Latiprimere.Abstractions, where `IRasterDocument` lives, so this needs no new package or dependency. P3-3 uses them to print.
- **Formats:**
  - **PWG raster:** the sync word `RaS2`, then for each page the 1,796-byte CUPS version 2 header, big-endian, with PWG 5102.4's values. Those include:
    - media class `PwgRaster`;
    - the paper's PWG name and its size in points;
    - the resolution, width, height and bytes per line;
    - the colour space (3 black, 18 sGray, 19 sRGB);
    - PWG's own fields in `cupsInteger`: total pages, the cross-feed and feed transforms, the image box, the "alternate primary" (white) and the print quality.
  - **Apple raster:** `UNIRAST\0` and the page count, then for each page a 32-byte header: bits per pixel, colour space (0 sGray, 1 sRGB), duplex (1 one-sided, 2 short-edge, 3 long-edge), quality, then width, height and resolution, big-endian. Apple hasn't published the format, so this follows libcups, which writes it.
  - **Lines in both:** a repeat byte per line, then runs. A control byte 0 to 127 means one pixel repeated 1 to 128 times; 129 to 255 means 2 to 128 different pixels follow. 128 is never written, since Apple raster gives it a meaning and PWG raster leaves it undefined.
- **Colour spaces:** 1-bit black (PWG raster only), 8-bit grey, 24-bit sRGB.
  - Grey uses the luma weights the Windows path uses (77, 150, 29).
  - 1-bit black is dithered with an 8×8 Bayer matrix, so pictures keep their shading. A hard threshold would lose it.
- **Pages:**
  - each raster page is the paper's whole size at the resolution;
  - the document's page is fitted inside: centred, shrunk if larger, never enlarged, as the Windows path fits pages to paper;
  - when the page's orientation, or the one asked for, differs from the paper's, the page is turned a quarter anticlockwise, the way landscape is printed.
- **Turning a page as it renders: `RasterBand.Rotation`.** It's a new, additive `RasterRotation` property on the public band type (default `None`). PDFium does the turn itself (`FPDF_RenderPageBitmap`'s rotation), so no memory is needed for it. An `IRasterDocument` that can't turn pages should throw `NotSupportedException` for any other value rather than render the page unturned.
- **Back sides of two-sided jobs** are transformed as the printer asks (`pwg-raster-document-sheet-back`, or Apple raster's `DM1` to `DM4`), with libcups' table:

  | The printer's back side | Long edge | Short edge |
  |---|---|---|
  | normal (`DM1`) | as is | as is |
  | flipped (`DM2`) | mirrored top to bottom | mirrored left to right |
  | rotated (`DM3`) | upside down | as is |
  | manual-tumble (`DM4`) | as is | upside down |

  - Mirroring left to right reverses each line.
  - Mirroring top to bottom renders the bands bottom up and writes the lines in reverse.
  - PWG raster's header records the transforms; Apple raster's has nowhere to, so only its pixels show them.
- **Memory and size:**
  - bands are as tall as a memory limit allows (16 MiB by default);
  - lines are composed one at a time;
  - identical lines, such as white margins, are written once with a count;
  - output is buffered into 64 KiB writes;
  - a blank A4 page at 600 dpi encodes to under 4 KB.
- **Cancellation** is checked between pages and bands.

## Consequences

- **P3-3 can print a PDF to any IPP Everywhere or AirPrint printer** by choosing a format, colour space and resolution from the printer's attributes, and streaming the encoder's output.
- **Public API:** `Latiprimere.Rendering.RasterRotation` and `RasterBand.Rotation` are new. Existing callers and implementations are unaffected, since the default is no rotation.
- **Testing:**
  - **Line encoding:** known byte sequences, and 200 random lines read back through a decoder written separately from the encoder: `RasterDocumentReader`, in Latiprimere.Testing, so the harness can use it.
  - **Synthetic pages:** a document of four coloured quadrants checks every turn, fit, back-side transform, band split and colour space, pixel by pixel.
  - **Real PDFs:** three test pages and a colour page, rendered by PDFium, encoded in both formats, grey and colour, and read back. Every pixel equals PDFium's direct render.
  - **`ippeveprinter`:** integration tests print PWG raster in grey at 300 dpi and black at 600 dpi to the container's printer, which aborts a job it can't read. They run in CI; Docker wasn't running on the development PC.
- **Not done:**
  - **PCLm** (D1);
  - **printer margins:** content is fitted to the whole paper, as on Windows, so a printer that can't print to the edge clips what's there. Fitting to `media-*-margin-supported` can follow if real printers need it;
  - **non-square resolutions:** Apple raster has none, and PWG raster printers rarely need them;
  - **checking on paper:** whether a real printer reads Apple raster as libcups writes it is checked on the HP in P3-3.
