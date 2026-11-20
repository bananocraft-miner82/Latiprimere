# 0045: A small PDF writer

- Status: Accepted
- Date: 2026-11-20

## Context

Phase 4 ([phase-4-plan.md](../phase-4-plan.md), accepted) needs PDFs written in three places:

- **PCLm** (P4-2) is a restricted PDF, each page an image in strips;
- **images** (P4-3) reach the Windows spooler, and printers that can't take them, placed on a page as a PDF;
- **plain text** (P4-4) reaches them laid out as a PDF.

.NET has no PDF writer, and Latiprimere has avoided dependencies beyond SharpIppNext, Tmds.MDns and PDFium; PDFium's own editing API would make the IPP package depend on a native library. The plan's D7: the writer is internal, in Latiprimere.Ipp, which every adapter already references; Latiprimere doesn't make documents, so it isn't public.

P4-1. Owner: "Start P4-1", with the plan's decisions accepted ("Accept all").

## Decision

- **Two layers, internal, in `Latiprimere.Ipp.Documents`:**
  - **`PdfObjectWriter`:** objects by reserved number, in any order; stream objects; the cross-reference table and the trailer (ISO 32000-1, 7.5). Version 1.7, with optional comments after the version line, as PCLm's `%PCLm 1.0` needs, and the binary marker comment.
  - **`PdfDocumentWriter`:** pages of a given size, images drawn into rectangles, lines of text in Courier, then the page tree, the catalog and a document information dictionary naming Latiprimere as the producer.
- **Streaming:**
  - offsets are counted as bytes are written (`CountingStream`), so the output needn't seek: a pipe, an HTTP request or a file all work;
  - a stream whose length isn't known in advance gets an indirect `/Length`, an object written after its data, so its data goes out as it comes;
  - each image is written as soon as it is drawn, and each page as soon as it ends. Only the page tree waits for the end. So a job of hundreds of PCLm strips is never held whole.
- **Compression:**
  - `FlateDecode` through .NET's `ZLibStream`;
  - `RunLengthDecode` through a `RunLengthEncoderStream` written here, a few dozen lines, as PCLm printers may prefer it (the HP lists `rle` first);
  - data compressed already, such as a JPEG with `DCTDecode`, passed through as it is, with its filter and parameters named, so a JPEG is never decoded and the PNG predictors of P4-3 can be named.
- **Text:**
  - Courier, one of the standard fonts every PDF reader has, so nothing is embedded;
  - `WinAnsiEncoding`, Windows-1252, covering Western European languages, by a table written here: no `CodePagesEncodingProvider`, which would have to be registered globally;
  - a character outside it, or a control character, is refused with an `ArgumentException` naming its code point and position (the plan's D6). Laying text out, with tabs, line breaks and pages, is P4-4's.
- **Numbers and strings:**
  - numbers to four decimal places, without exponents, in the invariant culture;
  - literal strings escaped, with every byte outside printable ASCII in octal, so content streams stay ASCII.
- **The writers are `IDisposable`,** as they hold the counting stream; disposing them doesn't close the output, which belongs to the caller.

## Consequences

- **Nothing changes for users yet:** the writer is internal and unused until P4-2, P4-3 and P4-4.
- **Tests (66, on both target frameworks):**
  - **byte-level:** the header and binary marker; every cross-reference entry 20 bytes and pointing at its object, for objects written out of order; indirect lengths equal to the data, for every encoding; unwritten, twice-written and late objects refused;
  - **through PDFium:** a document is opened and rendered, and its pixels checked. That covers a grey Flate image, a colour run-length image, a JPEG passed through (a 64 × 48 test image made with Windows' `System.Drawing`), an image in part of the page, escaped text with Windows-1252 characters, and pages of three sizes;
  - **streaming:** a 2,000 × 2,000 image to a stream that can't seek, in many writes, none of them the whole image;
  - run-length encoding against a decoder written apart from it, with 200 random buffers, and the encoding's bound;
  - Windows-1252 and number and string syntax.
- **PDFium is lenient,** repairing a broken cross-reference table without saying so, so the table's offsets are checked byte by byte rather than trusted to PDFium. P4-2's PCLm needs a stricter reader still: the HP, in the sign-off.
