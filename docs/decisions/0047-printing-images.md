# 0047: Printing JPEG and PNG images

- Status: Accepted (owner, 2026-11-20: "Accept ADR 0047")
- Date: 2026-11-20

## Context

Latiprimere printed two kinds of document, PDF and raw printer data. Applications also print images: photos, scans, charts, labels made as pictures. Phase 4's plan ([phase-4-plan.md](../phase-4-plan.md), accepted) brings JPEG and PNG in before 1.0 (P4-3), with these decisions:

- **D4:** PNG with a decoder written here, for every PNG, rather than a dependency or JPEG only;
- **D5:** `PrintOptions.Scaling`, with IPP's `print-scaling` values: printer default, fit, fill, or none;
- **D7:** the PDF writer of [ADR 0045](0045-pdf-writer.md) stays internal, in Latiprimere.Ipp;
- **D8:** `PrinterCapabilities.DocumentFormats` lists what a printer can be sent, itself or converted.

What the print systems take:

- IPP Everywhere and AirPrint printers are expected to take JPEG. The HP ENVY 5640 and `ippeveprinter` do. Printers that list PNG are fewer, and PNG support among them is uneven.
- CUPS converts JPEG and PNG itself, as it converts PDFs.
- The Windows spooler takes neither: Latiprimere draws PDFs through the driver from PDFium's rendering, and can do the same for an image once it is a PDF.

Owner: "Start P4-3".

## Decision

### Public API (additive)

- **`DocumentFormat.Jpeg` and `DocumentFormat.Png`,** and `PrintContent.FromJpeg(stream)` and `PrintContent.FromPng(stream)`. `DocumentFormat` was documented as gaining values ([ADR 0044](0044-room-to-grow-after-1-0.md)).
- **`ScalingMode`:** `PrinterDefault`, `Fit`, `Fill` and `None`, with `PrintOptions.Scaling`. It applies to images only; PDF pages keep their size.
- **`PrinterCapabilities.DocumentFormats`:** the formats a printer can be sent. A format that needs a PDF rasteriser is listed only when one is registered.

### Where each image goes

- **Direct IPP:** a JPEG to a printer that lists `image/jpeg` goes as it is, with `print-scaling` from `Scaling`. Anything else (a PNG, even to a printer that lists `image/png`, or a JPEG to one that doesn't take JPEG) is placed on a page as a PDF. That PDF then goes as any PDF does: as it is, or as raster in the formats [ADR 0046](0046-pclm-and-raster-format-order.md) allows. A printer that can take neither is refused before anything is sent, with a message naming the image's format and what a rasteriser would add.
- **CUPS:** sent as it is, as `image/jpeg` or `image/png`, with `print-scaling`. CUPS converts it.
- **Windows:**
  - a queue sent over IPP (`PrintPdfOverIpp`) takes images by the IPP route above;
  - otherwise the image is placed on a page as a PDF and printed through the driver as PDFs are, which needs a PDF rasteriser. The job is printed in the page's orientation, so the driver's paper is turned to match it.

### The page

- **Paper:** the job's `MediaSize`. Otherwise:
  - for IPP, the printer's `media-default`, read when needed;
  - for Windows, the driver's default paper, or A4 or US Letter as the region uses if the driver gives none.
- **Orientation:**
  - turned as EXIF says: a JPEG's APP1 segment, or a PNG's `eXIf` chunk;
  - landscape when the image is wider than tall, unless `Orientation` says otherwise.
- **Size,** as PWG 5100.13 defines `print-scaling`:
  - `Fit` fills the paper within 5 mm margins, which most printers' unprintable edges fit in;
  - `Fill` covers the whole paper, cutting off what overhangs;
  - `None` prints at the image's own size, centred;
  - `PrinterDefault` shrinks an image larger than the paper to fit, and prints a smaller one at its own size.
- **Resolution:** from EXIF, JFIF or `pHYs`. An image that gives none is printed at 96 dpi, as Windows and web browsers show it.

### JPEG: never decoded

- Only the headers before the first scan are read, for the size, components, resolution and orientation. The file goes into the PDF as it is, with `DCTDecode`, so the PDF reader or the printer decodes it.
- **What can be printed:** baseline, extended and progressive Huffman coding, at 8 bits a sample, in grey, colour or CMYK. Lossless, hierarchical and arithmetic coding, and 12-bit samples, are refused with a `PrintException` saying what to save it as. PDF readers' JPEG decoders needn't support those, and a printer that took the PDF would fail on them later.
- **CMYK** marked with Adobe's APP14 segment is stored inverted, so it gets an inverting `/Decode` array.

### PNG: a decoder written here

- **Everything the specification has:** every colour type and bit depth, the five row filters, Adam7 interlacing, palettes and `tRNS` transparency. Each chunk's CRC is checked.
- **Output:** 8-bit grey or colour, with transparency laid over white, as the paper is.
- **Skipped:** ancillary chunks, so gamma and colour profiles are ignored and an animated PNG prints its default image. An unknown critical chunk is refused, as the specification asks.
- **Streaming:** rows are decoded and written into the PDF one at a time. An interlaced image is put together in memory first, as its passes cover the whole image.
- **Passed through:** an opaque, 8-bit grey or colour PNG that isn't interlaced isn't decoded at all. Its image data goes into the PDF as it is, read with PDF's PNG predictors.
- **Limits:** images up to 100,000 pixels across and down, as for raster pages. Interlaced ones are limited to the largest .NET array once decoded, about 2 GB.
- **Errors:** a damaged PNG (a wrong signature or CRC, data that ends early or can't be decompressed, an invalid header) is refused with a `PrintException` naming the fault, before anything is sent.

## Consequences

- **Applications print photos and pictures on every print system** with one call. Only Windows, and IPP printers that take neither JPEG nor PDF, need PDFium.
- **No new dependency:** the JPEG header reader, EXIF reader and PNG decoder are a few hundred lines, in `Latiprimere.Ipp.Documents`.
- **Tests:**
  - **the PNG decoder** against PNGs written by a test encoder made apart from it, of every colour type and bit depth, interlaced and not, with every row filter: each pixel checked. Also palette and colour-key transparency, pass-through, `pHYs` and `eXIf`, and each kind of damage;
  - **JPEG and EXIF headers,** including each refused coding, JFIF densities, and Adobe CMYK;
  - **through PDFium:** a JPEG in each of the eight EXIF orientations shows its quadrants where EXIF puts them, and PNGs show their exact colours with transparency white;
  - **the layout:** each scaling, the margins, the landscape rule, and missing or one-sided resolutions;
  - **the scripted IPP client, the fake spooler and the IPP route:** each routing above, its refusals, and the formats capabilities list;
  - **harness scenarios:** `ipp.images.jpeg-as-is`, `ipp.images.png-as-pdf`, `ipp.images.png-as-raster`, `windows.images.print-to-pdf` and `cups.images.cups-pdf-output`, all passing here. The Native AOT check runs `ipp.images.png-as-raster`.
- **Not done:**
  - **the PNG test suite's images** (PngSuite), which the plan named: they need downloading. The test encoder covers the same colour types, depths, filters and interlacing, but not hand-made oddities such as zero-length `IDAT` chunks in odd places;
  - **gamma and colour profiles,** ignored as most viewers ignore them on paper;
  - **a photo on the HP's paper,** in the sign-off.

## Amendment, 2026-11-20

From a review of Phase 4's changes (owner: "Fix all of them"):

- **A cap on pixels in all:** 300 million, an A3 page at 1200 dpi, for PNGs and JPEGs alike, as well as 100,000 a side. A few megabytes of compressed data could declare 10 gigapixels, which would take minutes to decode and gigabytes to render. Images over it are refused before anything is decoded. That also bounds an interlaced PNG's memory, so its own limit is gone.
- **A network printer that lists no formats:**
  - it gets a JPEG as it is, as IPP Everywhere and AirPrint printers must take JPEG, and a PNG laid out as a PDF;
  - before, it was treated as CUPS and sent everything as it was;
  - whether images are sent as they are now depends on the print system, CUPS, rather than on missing formats.
- **Reading an image:** an image from a stream that can say its length is read once, straight into an array of its size, rather than copied twice.
- **Text-only Windows drivers:** they are refused before the image is read, as PDFs are.
- **Paper:** a printer's default paper is read once every five minutes at most, rather than for every image job.
- **Windows capabilities:** with the IPP route on, `PrinterCapabilities.DocumentFormats` adds what the queue's IPP printer takes.
