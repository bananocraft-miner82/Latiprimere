# Phase 4 Plan: PCLm, images and text, and events, before 1.0

> Status: **Accepted** (2026-11-20): owner, "Accept all", accepting the recommended decisions D1 to D10. Proposed with: "Let's also plan to add these now. There's no harm starting more feature-rich than the original plan." Three of plan.md's later items come forward, so 1.0 starts with them. This breaks them into work packages, says what already exists, and proposes the decisions to take.
>
> Progress:
> - **P4-5 done** (2026-11-20, [ADR 0049](decisions/0049-following-printers-and-jobs.md), accepted): `PrinterStatus`, `IPrinter.GetStatusAsync` and `IPrinter.WatchStatusAsync`, and `IPrintJob.WatchStatusAsync` as a member, all with default implementations that poll. Windows follows the spooler's change notifications. CUPS and network printers are polled: CUPS 2.4 answers Get-Notifications at once with `notify-get-interval` 60 rather than waiting, and the HP and `ippeveprinter` offer no subscriptions, so IPP notifications wait for a printer that waits. Checked with fakes, and in four harness scenarios, one showing a Windows job seen cancelled at once with an hour between polls.
> - **P4-4 done** (2026-11-20, [ADR 0048](decisions/0048-printing-plain-text.md), accepted): `PrintContent.FromText`, from a string or a stream in any encoding. CUPS lays text out itself, as UTF-8 `text/plain`; network printers and Windows get a PDF Latiprimere lays out: Courier at 10 points, half-inch margins, wrapping at spaces, tabs every 8 columns, form feeds. Characters outside Windows-1252 are refused, naming the code point, line and column. Checked by reading back each page's lines from the PDF, through PDFium, with the scripted printer, the fake spooler and four harness scenarios, CUPS's and Microsoft Print to PDF's among them.
> - **P4-3 done** (2026-11-20, [ADR 0047](decisions/0047-printing-images.md), accepted): JPEG and PNG on every print system. CUPS converts them; a printer that takes JPEG gets it as it is; otherwise the image goes on a page of the job's paper as a PDF: turned by EXIF, landscape when wider than tall, sized by `PrintOptions.Scaling`. JPEGs pass through undecoded; PNGs are decoded here, every colour type, depth and interlacing, transparency over white, or passed through when opaque 8-bit. Checked against a separate test encoder, through PDFium, with the scripted printer, the fake spooler and five harness scenarios, CUPS's and Microsoft Print to PDF's among them. PngSuite and the HP's paper wait.
> - **P4-2 done** (2026-11-20, [ADR 0046](decisions/0046-pclm-and-raster-format-order.md), accepted): `PclmWriter`, a third raster writer, writing each page's strips as they fill, as tall as the printer prefers (at most 256 lines) and compressed with Flate or run-length as it prefers; `IppRasterFormat`, with `IppNetworkOptions.RasterFormats` and `WindowsOptions.IppRasterFormats` to order or leave out formats. Checked by decoding the strips and comparing them line by line with PWG raster, through PDFium, on the scripted printer and in two harness scenarios, one of them run natively. The HP on paper waits for the sign-off.
> - **P4-1 done** (2026-11-20, [ADR 0045](decisions/0045-pdf-writer.md)): `PdfObjectWriter` and `PdfDocumentWriter`, internal in Latiprimere.Ipp: objects and streams written as they come, so the output needn't seek; Flate, run-length, and data passed through as it is, such as a JPEG; images and Windows-1252 text in Courier. Checked byte by byte and through PDFium.

## Purpose

- **Print to every raster printer:**
  - **PCLm,** the third raster format IPP printers take, after PWG raster and Apple raster;
  - **a choice of raster format,** for a printer that handles one badly.
- **Print images and plain text,** not only PDFs and raw printer data, on every print system.
- **Follow printers and jobs by events** where the print system sends them, instead of polling.
- **Keep 1.0's promise:**
  - an API review for what Phase 4 adds;
  - the checks on paper added to the sign-off.

## Where we are

| Item | What exists | What's missing |
|---|---|---|
| PCLm | Raster planning and encoding for PWG raster and Apple raster ([ADR 0034](decisions/0034-raster-encoders.md), [ADR 0035](decisions/0035-printing-pdfs-as-raster.md)): bands from PDFium, colour conversion, page geometry, back-side turns, streaming, gzip. The order PWG, Apple, PCLm was decided in Phase 3 (D2), with PCLm left for later (D1) | A PCLm writer: PCLm is a restricted PDF holding each page as strips. A way to choose the raster format |
| Images | `DocumentFormat` has `Pdf` and `Raw`, and says it may grow ([ADR 0044](decisions/0044-room-to-grow-after-1-0.md)). The HP and `ippeveprinter` take `image/jpeg` themselves, and CUPS converts images | The formats and `PrintContent` factories; for printers and the Windows spooler that can't take an image, a way to turn it into a PDF the existing PDF paths print. PNG has no decoder in .NET |
| Plain text | CUPS converts `text/plain`. Raw jobs carry text to text-only drivers | A format and factory; a PDF of the text for printers and Windows, which rarely take text |
| Events | `WatchStatusAsync` polls a job every half second. SharpIppNext models Create-Job-Subscriptions and Get-Notifications | Everything. **Neither the HP nor `ippeveprinter` lists a subscription operation,** so IPP events would come mainly from CUPS, which supports `ippget`. The Windows spooler has its own change notifications |

What the printers we can test with say:

| | HP ENVY 5640 | `ippeveprinter` (test container) |
|---|---|---|
| Formats | PCL, **JPEG**, **PCLm**, Apple raster, octet-stream | PDF, **JPEG**, PWG raster, octet-stream |
| PCLm | `pclm-strip-height-preferred` 32; compression `rle`, `jpeg`, `flate`, in that order of preference; `pclm-raster-back-side` `rotated` | — |
| Scaling | `print-scaling-supported` `auto`, `auto-fit`, `fill`, `fit`, `none` | — |
| Notifications | None: no Create-Printer-Subscriptions, Create-Job-Subscriptions or Get-Notifications | None |

## Work packages

Sizes are rough: **S** is about a day, **M** two or three days, **L** about a week.

### P4-1. A small PDF writer (S to M)

PCLm is a PDF, and images and text reach the Windows spooler, and printers that can't take them, as PDFs. So one internal writer serves all three.

- **What:** objects, a cross-reference table, page trees, streams with `FlateDecode` (`System.IO.Compression`), `RunLengthDecode` and `DCTDecode` (a JPEG passed through unchanged), image XObjects, and text in the standard Courier font, which needs no embedding.
- **Streaming:** pages are written as they are made, so a long PCLm job or text file never has to be held whole, as for raster today.
- **Where:** internal, in Latiprimere.Ipp, which Latiprimere.Windows and Latiprimere.Cups already reference (D7).
- **Tests:** PDFium, already in the tests, opens and renders what it writes; byte-level checks of the structure.

### P4-2. PCLm (M to L)

- **The format** (Mopria's PCLm): `%PCLm 1.0`, each page an image in strips of the printer's `pclm-strip-height-preferred` lines, compressed as it prefers among those Latiprimere writes (D2).
- **The rest is shared with Phase 3's raster:**
  - resolution from `pclm-source-resolution-supported`, capped by `MaxRenderDpi`;
  - grey or sRGB;
  - the back side turned as `pclm-raster-back-side` says;
  - streamed, gzipped when offered, sent again for a busy printer.
- **Choosing the format:** PDF first, then PWG raster, Apple raster and PCLm, as Phase 3 decided, with a new `IppNetworkOptions.RasterFormats` to change the order or leave one out (D3). The HP takes Apple raster before PCLm, so the option is also how its sign-off tries PCLm. Windows queues sent over IPP use the same order.
- **Tests:**
  - PDFium reads PCLm back, as it's a PDF, and the pages are compared pixel by pixel with the source, as for the other formats;
  - the scripted printer with the HP's attributes, and with a profile that takes PCLm only;
  - the HP on paper, in the sign-off.

### P4-3. Images (L)

- **Public API:**
  - `DocumentFormat.Jpeg` and `DocumentFormat.Png`;
  - `PrintContent.FromJpeg(stream)` and `PrintContent.FromPng(stream)`;
  - `PrintOptions.Scaling`: printer default, fit, fill, or actual size (D5).
- **Per print system:**
  - **Direct IPP:** a JPEG to a printer that lists `image/jpeg`, as IPP Everywhere and AirPrint printers are expected to, goes as it is, with `print-scaling`. A PNG, or a JPEG to a printer that can't take it, is placed on a page as a PDF, then sent as PDF or raster.
  - **CUPS:** sent as it is, as `image/jpeg` or `image/png`; CUPS converts images, as it converts PDFs.
  - **Windows:** placed on a page as a PDF, then printed through the driver as PDFs are. Windows queues sent over IPP take the IPP route.
- **Placing an image:**
  - on the job's paper, at its own resolution or fitted, as `Scaling` says;
  - turned as its EXIF orientation says;
  - landscape when it's wider than tall, unless `Orientation` says otherwise.
- **PNG:** no decoder in .NET, and decoding is needed for transparency, interlacing and 16 bits per sample (D4). Written in-house:
  - every colour type and bit depth, and interlacing;
  - transparency laid over white;
  - checked against the PNG test suite's images, through PDFium.

  An opaque, non-interlaced PNG of 8 bits per sample goes into the PDF without decoding, as PDF's Flate predictors read PNG's own rows.
- **Capabilities:** `PrinterCapabilities.DocumentFormats`, the formats a printer can be sent, itself or converted (D8).
- **Tests:**
  - the scripted printer receiving JPEG as it is, and PDF or raster for one that can't take it;
  - CUPS converting both in CI;
  - Microsoft Print to PDF;
  - a photo on the HP's paper.

### P4-4. Plain text (M)

- **Public API:** `DocumentFormat.Text`, `PrintContent.FromText(string)` and `PrintContent.FromText(stream, encoding)`.
- **Per print system:**
  - **CUPS:** takes `text/plain` and lays it out itself.
  - **Direct IPP printers and Windows:** sent a PDF that Latiprimere lays out: Courier, 10 points, the paper's margins, long lines wrapped, tabs every 8 columns, and a form feed starting a new page.
  - **A raw job** is still the way to a text-only driver or a receipt printer.
- **Characters:** Courier without embedding shows the Windows-1252 characters, which covers Western European languages. A character outside them is refused with `NotSupportedException` naming it (D6), rather than printed wrongly.
- **Tests:** the layout through PDFium, pages and line breaks; CUPS in CI; the scripted printer; Microsoft Print to PDF.

### P4-5. Printer and job events (L)

- **Public API, before 1.0** (D9):
  - `IPrintJob.WatchStatusAsync` becomes a member of the interface, with the polling interval as a fallback;
  - `IPrinter.WatchStateAsync` reports the printer's state and message as they change.

  Both use events where the print system sends them, and poll otherwise, so callers write the same code either way. A printer appearing or going isn't covered (D10).
- **Where events come from:**
  - **CUPS:** subscriptions and `ippget`, IPP's pull method (RFC 3995, RFC 3996): Get-Notifications waits for the next event, so a change arrives at once and an idle job costs one waiting request, not a poll every half second.
  - **Direct IPP:** the same, for printers that list the subscription operations. The HP and `ippeveprinter` don't, so they're still polled.
  - **Windows:** the spooler's change notifications (`FindFirstPrinterChangeNotification`), through `LibraryImport` as the rest of the spooler code, for jobs added, changed and gone, and the printer's status.
- **Robustness:**
  - a subscription is renewed before its lease runs out;
  - one that is lost, or refused, falls back to polling;
  - cancelling the watch cancels the subscription.
- **Tests:**
  - the scripted printer gains subscriptions and Get-Notifications;
  - CUPS in CI;
  - the Windows test printers;
  - each fallback, and that a status is never reported twice.

### P4-6. Docs, samples and the 1.0 API (M)

- **Guide pages:** images and text, raster formats, following jobs and printers by events.
- **Samples:** the CLI prints images and text, and follows jobs by events; the examples in `Latiprimere.Examples`.
- **Harness scenarios** for each package; the Native AOT check runs PCLm, an image, text and an event watch natively.
- **An API review of what Phase 4 adds,** as an addendum to [ADR 0040](decisions/0040-api-review-1-0.md), before the API freezes at 1.0.

### P4-7. The sign-off, extended

Added to [releasing.md](releasing.md#1-the-sign-off)'s list, on the HP's paper, each only when you say:

- a PDF as PCLm, with `RasterFormats` set to PCLm, one-sided and two-sided;
- a JPEG photo, sent as it is;
- a PNG with transparency, sent as raster;
- a page of text;
- a job followed through the HP's Windows queue by spooler events.

The Mac adds an image and text through its CUPS.

## Suggested order

1. **P4-1, then P4-2:** the writer, then PCLm, which tests it hardest.
2. **P4-3, then P4-4:** images, then text, both on the writer.
3. **P4-5,** which touches none of the above, so it can run alongside them in another session.
4. **P4-6,** then the extended sign-off and the stable switch.

Rough total: four to five weeks, plus the checks on paper. Previews keep being released meanwhile, so each package reaches users as it's done.

## Decisions

All accepted as recommended (owner, 2026-11-20).

| # | Question | Recommendation |
|---|---|---|
| D1 | 1.0 before or after Phase 4? | **After:** 1.0 starts with these, as you asked. Previews meanwhile; the HP's checks of Phase 3 can happen any time and are kept |
| D2 | PCLm compression? | **Flate and RLE,** whichever the printer prefers between them: both lossless, Flate from .NET and RLE a few dozen lines. Not JPEG: .NET has no JPEG encoder, and it would blur text |
| D3 | Choosing the raster format? | **`IppNetworkOptions.RasterFormats`,** in order of preference, defaulting to PWG raster, Apple raster, PCLm. Also used by Windows queues sent over IPP |
| D4 | PNG? | **Yes, with a decoder written here,** for every PNG: no new dependency, which Latiprimere has avoided so far. The alternative, JPEG only, saves about two days |
| D5 | How images are placed? | **`PrintOptions.Scaling`:** `PrinterDefault`, `Fit`, `Fill` and `None`, as IPP's `print-scaling` has them; `PrinterDefault` fits an image that's larger than the page and leaves a smaller one at its own size, as IPP's `auto` does |
| D6 | Text beyond Western European characters? | **Refused, for now,** with a message naming the character. Any script needs an embedded font and a font subsetter, adding a font file to the packages: a later addition, which ADR 0044's rules allow |
| D7 | Where the PDF writer lives? | **Internal, in Latiprimere.Ipp,** which every adapter already references; no new package. A public PDF writer is out of scope: Latiprimere doesn't make documents (plan.md's non-goals) |
| D8 | Capabilities for formats? | **`PrinterCapabilities.DocumentFormats`,** the formats a printer can be sent, whether it takes them itself or Latiprimere converts them |
| D9 | Events: optional interfaces, as ADR 0044 sets out for after 1.0, or members now? | **Members now:** before 1.0 the interfaces can still change, so the API needn't take the detour ADR 0044 describes. ADR 0044's routes stay for what comes after 1.0 |
| D10 | Printers appearing and going? | **Not in Phase 4:** mDNS announcements and the spooler's printer notifications differ, and nobody has asked. It can be added after 1.0 as a default-implemented catalog member |

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| A printer reads PCLm more strictly than PDFium does | Medium | High | Follow the Mopria layout exactly: strip heights, object order, the `%PCLm` comment. The HP's paper is the real reader, in the sign-off |
| PNG decoding bugs | Medium | Medium | The PNG test suite's images, rendered and compared; malformed files refused, never read past their end |
| Events cost more than they save on printers that support them | Low | Medium | Pull with a wait, not a listener; leases renewed by need; polling stays the fallback |
| Few printers send IPP events | High | Low | Polling is the fallback, unchanged; events help CUPS and Windows, where most queued jobs are |
| The API grows just before it freezes | Medium | Medium | P4-6's API review, as for 1.0; ADR 0044's rules after it |
| Text outside Windows-1252 | Certain | Low | Refused clearly; CUPS lays out any text it gets as `text/plain` |
