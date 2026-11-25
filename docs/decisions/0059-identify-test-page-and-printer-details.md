# 0059: Identifying a printer, a test page, and the printer's details

- Status: Accepted (owner, 2026-11-25: "Do 1, 2 and 3 now and add the remainder to the plan to do later")
- Date: 2026-11-25

## Context

A review of what printers offer that Latiprimere doesn't (2026-11-25) found three small additions, beyond what [plan.md](../plan.md) already lists for later:

1. **Identifying a printer:** IPP's Identify-Printer (PWG 5100.13) makes a printer flash, beep, show a message or speak, so whoever stands among five printers can tell which is "Office Laser". IPP Everywhere printers take it. Setup screens and kiosks want it.
2. **A test page:** no print system offers one that works everywhere. Windows draws its own in the shell, not through a spooler call; CUPS has one only for some PPD drivers, through `application/vnd.cups-command`; IPP has no operation for it. Latiprimere has a PDF writer ([ADR 0045](0045-pdf-writer.md)), so it can draw its own, the same on every print system.
3. **The printer's details:** IPP Everywhere printers report their web page (`printer-more-info`), a page for their supplies (`printer-supply-info-uri`), icons (`printer-icons`) and firmware (`printer-firmware-name` and `printer-firmware-string-version`). Applications that list or manage printers want them, and the UI add-ons will want icons.

Owner: "Do 1, 2 and 3 now and add the remainder to the plan to do later". The remainder is in plan.md's later items.

They are additions ADR 0044's rules allow: members on `IPrinter` with default implementations, and new records.

## Decision

### Public API (additive)

- **`IPrinter.GetDetailsAsync()`** returns a **`PrinterDetails`**:
  - `PrinterId`;
  - `WebPage`: the printer's own web page, usually where it is set up, or `null`;
  - `SuppliesWebPage`: where its supplies are shown or ordered, or `null`;
  - `Icons`: pictures of the printer, smallest first, as IPP Everywhere lists them (48, 128 and 512 pixels);
  - `Firmware`: a list of **`PrinterFirmware`**, a name and a version for each part, as the printer reports them;
  - `IdentifyActions`: the **`IdentifyAction`**s the printer can take to be identified, `Display`, `Flash`, `Sound` and `Speak`; empty if it can't be identified, or didn't say.

  The default implementation returns details with the printer's ID and nothing else, as `GetStatusAsync`'s returns the state in `Info`: details are extras, so a printer that has none isn't an error.
- **`IPrinter.IdentifyAsync(string? message = null)`** asks the printer to make itself known, in the ways it chooses by default, and to show the message if it can. The default implementation throws `NotSupportedException`.
- **`IPrinter.PrintTestPageAsync(PrintOptions options)`** prints a page Latiprimere draws, and returns its job. The default implementation throws `NotSupportedException`.
- **`IdentifyAction`** may grow, as IPP's `identify-actions` is a keyword that may, and joins the enums CONTRIBUTING.md says may.
- **The catalog's printer** forwards all three. A test page's options are checked as a job's are ([ADR 0055](0055-catalog-printers.md)), and its job is wrapped.

Identify actions are in the details, not in `PrinterCapabilities`. Capabilities are what a job can ask for; identifying isn't a job. Keeping them in the details also means reading them costs no extra request, as the details are read together.

### Identifying

- **IPP and CUPS:** Identify-Printer, with `requesting-user-name`, and `message` when one is given. No `identify-actions`, so the printer does what it does by default.
- **The message** is IPP's `text(127)`: more than 127 bytes in UTF-8 throws `ArgumentException` before anything is sent, rather than being cut short. `null` or empty sends none. A printer that can't display shows nothing, and is still identified in its other ways.
- **A printer that doesn't take the operation,** answering `server-error-operation-not-supported`, gets `NotSupportedException`. It isn't asked first, so identifying costs one request. What CUPS does depends on its version and the queue's printer; the operation's answer settles it.
- **Windows:** the spooler has no such call. A queue whose IPP printer is known, with `WindowsOptions.PrintPdfOverIpp` on ([ADR 0027](0027-direct-ipp-for-windows-queues.md)), is identified over IPP; otherwise `NotSupportedException`, whose message says so.

### The details

- **IPP and CUPS:** read with one Get-Printer-Attributes request for those six attributes, read in their raw form, as SharpIppNext models only `printer-more-info`.
- **Only `http` and `https` URIs** are given: anything else a printer reports, such as a `file:` URI, is left out. The URIs come from the printer, so the documentation says to treat them as the printer's say-so: an application that fetches an icon should expect the printer's host, not trust it.
- **Firmware:** `printer-firmware-name` and `printer-firmware-string-version` in pairs, by position; a name without a version, or a version without a name, is left out.
- **Windows:** over IPP as for identifying, when the queue's IPP printer is known; otherwise details with nothing but the ID. A queue's IPP printer that can't be reached is logged, and the details are empty, as the spooler still has the queue.
- **Not in `PrinterInfo`:** printers found on the network are listed from their announcements, which have none of these, so `PrinterInfo` would have them only sometimes.

### The test page

- **What it shows:**
  - the printer's name, ID, make and model, and location; its print system and address; the paper; when it was printed, in UTC; and Latiprimere's version;
  - what the capabilities say: paper sizes, two-sided modes, colour modes, qualities, trays and document formats;
  - **a line a quarter of an inch (6.35 mm) in from each edge.** A side that doesn't print is outside what the printer can print;
  - **two rulers, 100 mm and 4 inches,** with ticks, to measure. Shorter means the page was scaled;
  - **colour patches:** cyan, magenta, yellow, black, red, green and blue, and a grey ramp from white to black in 10% steps. The grey ramp is drawn in DeviceGray, so a colour printer prints it with black ink.
- **Courier, as text is** ([ADR 0048](0048-printing-plain-text.md)). A character outside Windows-1252, in a printer's name, say, is shown as `?`: a test page shouldn't fail over the printer's name.
- **The paper:** the job's `MediaSize`, else the printer's default, as for images and text. On paper too small for the layout, such as a 4 × 6 inch label, everything but the edge line is shrunk to fit, and the page says the rulers are shrunk.
- **A PDF, printed as any PDF is,** through the printer's own `SubmitAsync`: as it is, as raster, or through the driver. So it needs what a PDF needs: on Windows without a queue sent over IPP, and for a network printer that takes only raster, a PDF rasteriser. Without one it fails as a PDF job would, with `NotSupportedException`.
- **The capabilities are read first.** A printer whose capabilities can't be read fails the test page with that error, as it would likely fail the job.
- **Shared code:** the page is drawn by `TestPageWriter`, internal to Latiprimere.Ipp, which both the IPP printers and the Windows printers use; Latiprimere.Windows already references it. The PDF writer gains filled and outlined rectangles and lines.

## Consequences

- **Additive:** two records, an enum and three interface members with default implementations. Other `IPrinter` classes keep compiling; they report no details and throw for the other two.
- **Windows needs the IPP route** for identifying and for details. Without `PrintPdfOverIpp` they aren't available. The test page works on every queue that can print a PDF.
- **Identify actions on Windows** come from the queue's IPP printer, so they too need the route.
- **Tests:**
  - **Abstractions:** the defaults of the three members, and of the records;
  - **the catalog:** each forwarded, the test page's options checked, its job wrapped;
  - **IPP:** the request each sends; the message's limit; operation-not-supported mapped; each detail read, URIs other than `http` and `https` left out, firmware paired;
  - **the PDF writer:** rectangles, lines and colour operators;
  - **the test page:** its text read back from the content stream; the edge line at a quarter inch; the rulers' length; shrinking on small paper; `?` for characters Courier can't show; through PDFium, ink at the edge line and none outside it;
  - **Windows:** over the IPP route and without it.
- **Not done:**
  - **`printer-strings-uri`,** a printer's own names for its options in a language. Useful only with a request in each language and a reader for its `.strings` files, so it waits for the UI add-ons, in plan.md;
  - **the test page on the HP's paper,** and identifying it, in the sign-off.
