# 0053: Document generation as a companion product

- Status: Accepted (owner, 2026-11-23: answered its five questions, each as recommended)
- Date: 2026-11-23

## Context

A review of what Latiprimere supports (2026-11-23) asked about five capabilities. Two of the gaps are by design: a document and layout API, and making PDFs. [ADR 0001](0001-headless-ipp-first-scope.md) and plan.md leave layout and document generation out of v1: callers bring PDF, images or raw printer language. The owner asked whether document generation could be "a viable compatible side-product that could compliment Latiprimere and work well with it".

### What exists already

Phase 4 built more of a document pipeline than its public API shows. All of it is internal to `Latiprimere.Ipp`:

- **A streaming PDF writer** ([ADR 0045](0045-pdf-writer.md)): objects, Flate and run-length streams, images passed through or compressed, pages written as they end, nothing held whole;
- **images placed on a page** ([ADR 0047](0047-printing-images.md)): a JPEG reader, a PNG decoder and EXIF orientation, fitted or filled to the paper;
- **text laid out** ([ADR 0048](0048-printing-plain-text.md)): Courier at 10 points, wrapped and paginated, Windows-1252 characters only;
- **1-bit dithering** ([ADR 0034](0034-raster-encoders.md)): an 8 × 8 Bayer matrix, for black-only raster.

Public already:

- `IPdfRasterizer` and `IRasterDocument`, which render a PDF's pages into bands of pixels, through PDFium;
- `PrinterCapabilities.MediaSizes`, each with its size in millimetres;
- `PrintContent.FromPdf` and `FromRaw`, which any document ends as.

### What's missing

- **A page model** a caller can fill: text in more than one font and size, lines, boxes, images, barcodes, tables, and content that flows across pages with headers and footers.
- **Fonts beyond the standard 14,** which is the hard part. Any script outside Western Europe needs a font embedded, which means reading TrueType or OpenType and subsetting it. Arabic, Hebrew and the Indic scripts also need shaping and bidirectional text, the work HarfBuzz does.
- **Label and receipt output.** Today a ZPL label or an ESC/POS receipt is written by hand ([labels and receipts guide](../site/guides/labels-and-receipts.md)).

### Other libraries

General document generation for .NET is well served:

- **QuestPDF:** a fluent layout API with a native Skia build behind it. Its Community licence is free for organisations under US$1 million in revenue, like Latiprimere's ([ADR 0015](0015-split-licence.md)), and paid above it;
- **PDFsharp and MigraDoc:** managed and MIT licensed;
- **iText:** AGPL or commercial.

All of these make PDFs that `PrintContent.FromPdf` already prints. None of them makes ZPL or ESC/POS, none starts from what a printer reports, and the label and receipt libraries that do exist each cover one printer language without a page model. That gap is where a companion product would fit.

## Decision

With the owner's answers to its questions (below), each as recommended. Nothing is built before the 1.0 sign-off.

### What it is

- **A companion product, not a core feature:**
  - new packages named `Latiprimere.Documents.*`;
  - they reference `Latiprimere.Abstractions` at most, for `PrintContent`, `MediaSize` and `PrinterCapabilities`;
  - no core package or adapter references them, so printing never takes their size or dependencies. ADR 0001's scope for the core is unchanged.
- **Print-first:** labels, receipts, tickets, packing slips, delivery notes and simple invoices. Not reports, charts or a general typesetting engine; for those, the docs show printing a QuestPDF or MigraDoc PDF.
- **One layout, several outputs:** a document is laid out once and rendered to PDF for any printer, or to ZPL or ESC/POS for label and receipt printers.
- **Starts from the printer:** a page can take its size from `PrinterCapabilities` (a 102 × 152 mm label, a roll 80 mm wide), and a label or receipt is laid out at the printer's own resolution (203 or 300 dpi), so lines and barcodes land on whole dots.
- **Same rules as the core:** Native AOT and trimming, no UI types, streaming output, the split licence, and the conventions in CONTRIBUTING.md. The Commercial Licence already covers "the software libraries Provider publishes under that name", so the new packages fall under it.

### In stages, each useful alone

| Stage | What | Package | Depends on | Rough size |
|---|---|---|---|---|
| 0 | A guide: printing PDFs made with QuestPDF, PDFsharp/MigraDoc and HTML-to-PDF tools | none | none | a day |
| 1 | ZPL and ESC/POS builders: text in the printer's fonts, barcodes and QR codes in the printer's own commands, images as raster commands, cut, feed and the cash drawer | `Latiprimere.Documents.Commands` | Abstractions | 1–2 weeks |
| 2 | The page model and PDF: text in the standard 14 fonts, lines, boxes, images, Code 128, EAN-13 and QR codes as vector shapes, flowing pages with headers and footers, simple tables | `Latiprimere.Documents` | Abstractions | 3–4 weeks |
| 2b | Embedded TrueType and OpenType fonts, subset, for Latin, Greek, Cyrillic and CJK text | `Latiprimere.Documents` | none new | 2–3 weeks |
| 3 | Documents to label and receipt printers: barcodes and plain text as printer commands where they can be, the rest rendered through `IPdfRasterizer` and sent as ZPL `^GF` or ESC/POS `GS v 0` raster | `Latiprimere.Documents.Rendering` | Abstractions; PDFium at run time | 1–2 weeks |

The sizes are rough, for comparison, and come before any design review.

- **Stage 1 first:** it is the smallest, fills the gap the review found for specialist printers, and stages 2 and 3 build on its commands.
- **Stage 2 keeps its own copy of the PDF writer:** the source in `Latiprimere.Ipp.Documents` is shared and compiled, internal, into both assemblies. Neither package depends on the other, and the PDF writer doesn't become a public API that couldn't change after 1.0.
- **Stage 2b also serves the core:** with embedded fonts, `PrintContent.FromText` could print text in any script that needs no shaping, the item plan.md lists for later.
- **Complex scripts stay out** unless asked for: shaping and bidirectional text would need HarfBuzz, a native library.
- **Stage 3 reuses PDFium** rather than writing a rasteriser for glyphs. For pictures it dithers, as raster printing does. For text and lines it thresholds, because dithering blurs small type and barcodes, which then fail to scan.

### A sketch of the API

For illustration only. The real shape needs its own API review, as the core's did ([ADR 0040](0040-api-review-1-0.md)).

```csharp
PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

Document label = Document.Create(capabilities.DefaultMediaSize, page =>
{
	page.Margin(Length.FromMillimeters(4));
	page.Text($"Order {orderNumber}", TextStyle.Default.WithSize(18).Bold());
	page.Barcode(BarcodeKind.Code128, orderNumber, height: Length.FromMillimeters(20));
	page.Image(logo, width: Length.FromMillimeters(30));
});

// Any printer
IPrintJob job = await printer.SubmitAsync(label.ToPdf(), PrintOptions.Default);

// A Zebra at 203 dpi: barcodes and printer fonts as ZPL commands, the logo as a ^GF graphic
IPrintJob labelJob = await zebra.SubmitAsync(label.ToZpl(new ZplOptions { Dpi = 203 }), PrintOptions.Default);
```

### The owner's answers

1. **Go ahead:** yes, as a companion product after 1.0.
2. **Scope:** print-first, as above. Reports and rich layout stay with QuestPDF, MigraDoc and the like.
3. **Name:** `Latiprimere.Documents.*`, sharing the brand and, once it is reserved, the `Latiprimere.` prefix on nuget.org.
4. **Fonts:** the standard 14 fonts in stage 2, and embedded fonts as stage 2b after it.
5. **Timing:** stage 0 and stage 1 start straight after the 1.0 sign-off.

## Consequences

- **Nothing changes in the core packages or their API.** ADR 0001 and the 1.0 API stand; this record adds beside them and supersedes nothing.
- **plan.md** lists the companion product and the stages, and its non-goals point here.
- **The PDF writer gets a second user.** A change to it must keep both building and passing their tests; the shared source makes that one change, not two.
- **Testing follows the existing patterns:**
  - PDFs rendered through PDFium and their pixels checked, as for ADR 0045;
  - ZPL and ESC/POS checked byte for byte, and raster commands decoded by a reader written apart from the encoder, as run-length encoding is;
  - a real label printer and a real receipt printer added to the sign-off, as only an HP inkjet has been tried so far. Neither is on hand yet.
- **Risks:**
  - **scope creep** into a general layout engine, which is far larger than the stages. The print-first line guards against it;
  - **QuestPDF moving into labels and receipts.** Its licence and its native dependency would still differ, but the overlap would grow;
  - **printer languages vary by model:** ESC/POS dialects (Epson, Star, Bixolon) and ZPL firmware levels. Stage 1 targets Epson's ESC/POS and ZPL II, and names other dialects as unsupported.
