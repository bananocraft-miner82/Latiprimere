# 0054: Options for the PDF rasteriser

- Status: Accepted (owner, 2026-11-23: "Accept all", for [phase-4-plan.md](../phase-4-plan.md)'s D11 to D13; built as P4-8, "Start P4-8 and P4-9")
- Date: 2026-11-23

## Context

`IPdfRasterizer` and `IRasterDocument` are the interfaces a renderer implements. After 1.0 they change only in a major version, not even with default implementations (CONTRIBUTING.md, "Changing the Public API"; [ADR 0044](0044-room-to-grow-after-1-0.md)). Before this they took no options:

- **every band was 32-bit BGRA** (`RasterBand.BytesPerPixel`, a constant 4), even for a monochrome laser, whose job needs a quarter of the memory in 8-bit grey;
- **text, lines and images were always smoothed.** On a 203 dpi thermal printer, small type and barcodes need it off, or their edges turn grey and blur when the band is made black and white, and barcodes fail to scan. The Documents companion's stage 3 needs it ([ADR 0053](0053-document-generation-companion.md));
- **`Open(Stream)` took no password,** so a password-protected PDF was refused wherever Latiprimere rendered it, though PDFium opens such files with the password.

Phase 4's plan, P4-8, with decisions D11 to D13 (owner: "Accept all"; then "Start P4-8 and P4-9").

## Decision

### Public API

- **`RasterPixelFormat`,** a new enum: `Bgra32` (0) and `Gray8` (1). It may gain values, so it joins the enums CONTRIBUTING.md says may grow; code meeting a value it doesn't know throws `NotSupportedException`.
- **`RasterBand.PixelFormat`,** `Bgra32` by default, so a band made as before renders as before.
  - `RasterBand.BytesPerPixel` becomes an instance property from the format, and `RasterBand.GetBytesPerPixel(format)` gives it for any format. The static field of the same name is removed.
  - `RasterBand.Create(pageWidth, pageHeight, top, height, pixelFormat)` makes a band with the tightest stride for its format; the overload without it stays, for `Bgra32`.
- **`RasterRenderOptions`,** a record: `SmoothText`, `SmoothLines` and `SmoothImages`, each `true` by default, and `RasterRenderOptions.Default`.
- **`PdfOpenOptions`,** a record: `Password`, and `PdfOpenOptions.Default`. Its `ToString` says only whether a password is set, so a log or a debugger never shows it.
- **`IPdfRasterizer.Open(Stream content, PdfOpenOptions? options = null)`** and **`IRasterDocument.RenderBand(int pageIndex, RasterBand band, Span<byte> pixels, RasterRenderOptions? options = null)`.** The options are optional parameters, so every call written before still compiles.
- **`PrintContent.FromPdf(Stream content, string password)`** and **`PrintContent.Password`.**

The options are records so that, after 1.0, they can gain properties freely, each with a default that keeps today's rendering, while the interfaces stay as they are (D11).

### PDFium

- **Grey:** an `FPDFBitmap_Gray` bitmap over the caller's buffer. PDFium converts colours to grey as it draws.
- **Smoothing:** `FPDF_RENDER_NO_SMOOTHTEXT`, `FPDF_RENDER_NO_SMOOTHPATH` and `FPDF_RENDER_NO_SMOOTHIMAGE`, beside the annotations and printing flags used before.
- **Passwords:** given to `FPDF_LoadMemDocument64`. A document that needs one and was given none says how to give it (`PrintContent.FromPdf(stream, password)`); a wrong password says so, without the password.
- **1-bit black stays in the encoders,** which dither pictures ([ADR 0034](0034-raster-encoders.md)). PDFium has no 1-bit output (D12).

### Passwords, per print system (D13)

- **Windows spooler:** passed to the rasteriser.
- **Direct IPP, a printer that can't take PDF:** rendered as raster, as before, with the password.
- **Direct IPP, a printer that takes PDF:** Latiprimere asks for `document-password-supported` (PWG 5100.13, the longest password it takes, in octets; 0 for none).
  - If the password fits, it goes as the `document-password` operation attribute of Print-Job. Validate-Job, which has no document, isn't sent it.
  - Otherwise the PDF goes as raster, rendered here with the password, if the printer takes a raster format and a rasteriser is registered.
  - Otherwise it is refused with `NotSupportedException` before anything is sent. The message names the printer, never the password.
- **CUPS:** as a printer that takes PDF. Found while building it: the CUPS adapter has no rasteriser and doesn't know a queue's raster formats, so a queue that doesn't take passwords refuses the job, where the plan said such PDFs would go as raster. Giving CUPS a rasteriser is a later addition, if asked for.
- **Windows queues sent over IPP:** as direct IPP. A refusal there falls back to the spooler, which renders the PDF with the password, as for any job the IPP route can't take ([ADR 0036](0036-wsd-queues-over-ipp.md)).
- **Found while building it:** SharpIppNext writes an octetString given as a string in ASCII, turning any other character into `?`. The password is given as its UTF-8 bytes, which it writes as they are.

### Grey inside Latiprimere

- **IPP raster in grey and in 1-bit black** renders bands in `Gray8`, a quarter of the memory. Grey lines are copied as they are, and black is dithered from them. The memory limit on a band (`MaxBandBytes`) therefore holds four times the rows.
- **The Windows spooler, for a monochrome job,** renders in `Gray8` and draws each band as an 8-bit bitmap whose colour table is the 256 greys, rows padded to whole 4-byte words as GDI needs. `GreyscaleConverter`, which turned colour bands grey in place, is gone.

## Consequences

- **Breaking, before 1.0, for renderers only.** A class implementing `IPdfRasterizer` or `IRasterDocument` must take the new parameters; code calling them doesn't change. Code that read the removed static `RasterBand.BytesPerPixel` uses `band.BytesPerPixel` or `RasterBand.GetBytesPerPixel(RasterPixelFormat.Bgra32)`. Package validation's suppressions for the two interface methods are in `CompatibilitySuppressions.xml` of `Latiprimere.Abstractions` and `Latiprimere.Rendering.Pdfium`, and go at the next release's post-release step.
- **Grey output moves slightly.** PDFium weighs colours into grey a little differently from Latiprimere's luma, and smooths the edges of text and scales images a little differently in grey. Solid colour matches within a few levels; the raster tests now compare with PDFium's own grey rendering.
- **Tests:**
  - **Abstractions:** a grey band's stride and size; a band made without a format being `Bgra32`; an unknown format refused; the options' defaults; the password kept out of `PdfOpenOptions.ToString`; `FromPdf` with a password, and a null one refused.
  - **PDFium:** a grey rendering matching the colour one's luma in solid colour and on average; black line art without smoothing having no grey at all, and with it, edges of grey; an unknown format refused; a password-protected PDF opening with its password or its owner password, refused without one, with a message saying how to give it, and refused with a wrong one, without the password in the message.
  - **IPP:** the password sent as `document-password` with Print-Job only, as UTF-8 octets on the wire; raster with the password for a printer taking no passwords, or a shorter one; a queue without known formats refusing, before anything is sent; no extra request for a PDF without a password.
  - **Windows:** the password reaching the rasteriser, and none without one.
  - **The harness:** the Windows output and rendering scenarios pass, Microsoft Print to PDF's monochrome page among them, drawn from the 8-bit grey bitmap.
- **New test documents:** `line-art.pdf` and `line-art-password.pdf`, written by `tools/test-documents/make-line-art.py` with Python's standard library, the second encrypted with 128-bit RC4.
- **The sign-off** gains a password-protected PDF and grey against colour on the HP (P4-7).
