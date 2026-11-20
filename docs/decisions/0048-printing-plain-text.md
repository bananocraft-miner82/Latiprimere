# 0048: Printing plain text

- Status: Accepted (owner, 2026-11-20: "Accept ADR 0048")
- Date: 2026-11-20

## Context

Plain text is the simplest document applications print: logs, reports, notes, the contents of a text box. Before this, it could only go as raw data, which a printer prints only if it takes text in its own language, as text-only drivers and receipt printers do. Phase 4's plan ([phase-4-plan.md](../phase-4-plan.md), accepted) brings text in before 1.0 (P4-4):

- **CUPS** lays out `text/plain` itself, with its own fonts;
- **direct IPP printers and Windows** are sent a PDF that Latiprimere lays out, with the PDF writer of [ADR 0045](0045-pdf-writer.md);
- **D6:** Courier, which PDF readers have without embedding, shows the Windows-1252 characters. A character outside them is refused with `NotSupportedException` naming it, rather than printed wrongly. Embedding a font, for any script, is a later addition.

[ADR 0047](0047-printing-images.md) built the same route for images. Owner: "Go for P4-4".

## Decision

### Public API (additive)

- **`DocumentFormat.Text`.**
- **`PrintContent.FromText(string)`,** which holds the text as UTF-8.
- **`PrintContent.FromText(stream, encoding)`,** for text in any encoding. A byte order mark overrides the encoding given.
- **`PrintContent.TextEncoding`,** the text's encoding, or `null` for other formats.

### Where text goes

- **CUPS:** as `text/plain`, re-encoded as UTF-8 without a byte order mark, as CUPS reads it. CUPS lays it out, with any character its fonts have. Latiprimere's character limit doesn't apply.
- **Direct IPP:** always a PDF Latiprimere laid out, even to a printer that lists `text/plain`, so text looks the same on every printer. The PDF goes as any PDF does: as it is, or as raster ([ADR 0046](0046-pclm-and-raster-format-order.md)). A printer that can take neither is refused before anything is sent.
- **Windows:**
  - a queue sent over IPP takes text by that route;
  - otherwise the PDF is printed through the driver, in its pages' orientation, as images are.
- **A text-only driver** is refused, as for a PDF, as its pages would be pictures. The message points to raw data, which is how text reaches such drivers and receipt printers.
- **Paper:** the job's `MediaSize`, else the printer's or driver's default, as for images.

### The layout

- **Courier at 10 points,** 6 points a character, on lines 12 points apart.
- **Half-inch margins on every side.** A4 portrait holds 87 columns and 64 lines; US Letter 90 and 60; A4 landscape 128 and 43.
- **Orientation:** portrait, unless `Orientation` is `Landscape`. Unlike images, text has no shape to follow.
- **Line ends:** `\r\n`, `\n` and `\r` each end a line. A final line end adds no empty line.
- **Wrapping:** a line longer than the page is wrapped at its last space that fits, without carrying the space over. A line with no such space breaks where it must.
- **Tabs** stop every 8 columns.
- **Form feeds:**
  - a form feed starts a new page;
  - one straight after a page filled doesn't leave a blank page;
  - one at the start, or after another, does.
- **Empty text** prints one blank page, as a document needs a page.

### Characters

- **Allowed:** the Windows-1252 characters, and tab, line ends and form feed among control characters.
- **Refused:** any other character, or control character, with a `NotSupportedException` naming its code point, line and column. It is raised while laying out, before anything is sent.
- **Surrogate pairs** are named by the code point they make, so 😀 is U+1F600, not two halves.

## Consequences

- **Applications print text on every print system with one call,** without knowing whether the printer lays text out.
- **Text outside Western European languages** prints through CUPS, but not yet to network printers or Windows. It can be printed there as a PDF the application makes, until a font is embedded.
- **Tests:**
  - **the layout,** read back from the PDF's content streams: columns and rows for three papers and orientations, line positions, wrapping, tabs, each line end, every form-feed case, refused characters and their positions, Windows-1252 bytes, and other encodings with and without byte order marks;
  - **through PDFium:** a full line drawn from the left margin to the right, and nothing outside it;
  - **the scripted IPP client, the fake spooler and the IPP route:** each routing, refusals, CUPS's re-encoding, and the formats capabilities list;
  - **harness scenarios:** `ipp.text.as-pdf`, `ipp.text.character-refused`, `windows.text.print-to-pdf` and `cups.text.cups-pdf-output`. The Native AOT check runs `ipp.text.as-pdf`.
- **Not done:**
  - **embedding a font** for other scripts (D6);
  - **text on the HP's paper,** in the sign-off.

## Amendment, 2026-11-20

From a review of Phase 4's changes (owner: "Fix all of them"):

- **CUPS text is re-encoded as it uploads,** a buffer at a time, rather than held whole as a string and again as bytes. A text job to CUPS can no longer be sent again from its start if CUPS answers busy, which CUPS doesn't, as it queues jobs.
- **A refused character stays refused on Windows:**
  - a Windows queue sent over IPP no longer takes a refused character for a raster job its printer can't take, and so no longer tries the driver;
  - before, it laid the text out twice, and without a rasteriser it reported that one was missing instead of naming the character;
  - the refusal is still a `NotSupportedException`, of an internal type deriving from it.
- **A network printer that lists no formats** is sent text laid out as a PDF, as every network printer is, rather than as `text/plain`.
- **Text-only Windows drivers** are refused before the text is read or laid out.
