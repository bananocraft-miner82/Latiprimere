# 0032: Vendor options in the capabilities, and Windows Print Capabilities

- Status: Accepted
- Date: 2026-11-18

## Context

Vendor options ([ADR 0026](0026-vendor-options-in-detail.md)) and Print Tickets ([ADR 0031](0031-windows-print-tickets.md)) let a job set anything a printer supports. But nothing said what that is: callers had to know the IPP attribute names, or the driver's Print Schema, from elsewhere. ADR 0031 left reading a driver's Print Capabilities undone.

Owner: "Build both, but also include 2B and 2C rather than waiting for someone to ask for it. Let's go as fully featured as we can." That covered three things:

- **2A:** a tool to dump a driver's Print Capabilities;
- **2B:** a Windows-only public API for them;
- **2C:** a cross-platform model of vendor settings in `PrinterCapabilities`.

Drivers and printers examined while designing it:

- **Microsoft Print to PDF:** its vendor namespace is `…/Microsoft_Print To PDF/10.0.26100.9974/`, so it changes with each driver update. The Print Ticket API's prefix for it, `ns0000`, doesn't change.
- **XPS Document Writer:** offers image compression and interleaving.
- **The HP ENVY 5540 series class driver:** offers output quality, pages per sheet, resolution and HP's media types. Its pages-per-sheet options have no names, only a `psk:PagesPerSheet` scored property.
- **Captured IPP responses** from the HP ENVY 5640, `ippeveprinter` and CUPS 2.4 queues: they encode collections flattened, with member values named after their members, so `media-type` appears as if it were a printer attribute.

## Decision

### The model (Latiprimere.Abstractions)

- **`PrinterCapabilities.VendorOptions`** lists `VendorOption`s. Each has:
  - `Key`, to set in `PrintOptions.VendorOptions`;
  - `DisplayName`;
  - `Kind`: `Choice`, `Number` or `Text`;
  - `Choices`: each a `VendorOptionChoice` with a `Value` and a `DisplayName`;
  - `DefaultValue`;
  - `MinimumValue` and `MaximumValue`, for numbers;
  - `AllowsSeveralValues`.
- **Every listed key and value can be sent as it is.** That is the contract: what is listed is what `PrintOptions.VendorOptions` accepts.
- **Settings the typed options cover aren't listed:** paper, duplex, colour, trays, copies and orientation. Empty means "not reported", as for every capability.

### IPP (CUPS and network printers)

- **The capabilities read makes two requests, one after the other.** The typed request stays as it was. Then a new raw Get-Printer-Attributes request (`IIppClient.GetPrinterAttributeListAsync`) asks for the `job-template` group and `job-creation-attributes-supported`. The typed response drops what SharpIppNext doesn't model, which is why the raw request is needed.
  - **Not at the same time,** because some printers take one connection at a time.
  - **Vendor options are an extra:** if the raw request is refused or its answer can't be read, the capabilities list none, and the failure is logged as a warning. A cancellation still ends the call.
  - **A refusal is remembered:** a printer that answers with an IPP client error, or with something unreadable, isn't asked again by the same `IPrinter`, so it costs one round trip and one log entry rather than one per read. A failure to connect may pass, so it is tried again next time.
- **Which attributes are listed:** those in `job-creation-attributes-supported`, or, for a printer that doesn't send it, those with both `-supported` and `-default` attributes.
- **Which are left out:**
  - what the typed options or every request send;
  - operation attributes;
  - collections (`-col`) and `page-ranges`, which vendor options can't express.
- **Values:**
  - keywords, names, integers and enums become choices; text values don't, since the encoder would send them back as names, which a strict printer refuses;
  - `rangeOfInteger` becomes a number with limits, and `job-priority` is 1 to 100;
  - booleans are listed only when both values are offered.
  - Resolutions are written `600dpi` or `600x1200dpi`. The vendor option encoder now sends values in that form as `resolution`, so `ipp:printer-resolution` works, but only for attributes whose names end in `-resolution`. Elsewhere, such as CUPS's own `resolution` option, `600dpi` is still sent as a name, as before.
  - `finishings` allows several values.
  - `print-quality` and the common `finishings` enum values have names; keywords are made readable ("text-and-graphics" becomes "Text and graphics").
- **Collections are skipped** by tracking their nesting, so a member's name isn't mistaken for an attribute.

### Windows

- **The capabilities read also reads the Print Capabilities document and the default Print Ticket,** in one Print Ticket session (`PTGetPrintCapabilities`). A driver without Print Ticket support, or a document that can't be read, gives an empty list. The typed capabilities don't depend on it.
- **Keys:**
  - each top-level feature becomes `windows:feature:<name>`, and each parameter `windows:parameter:<name>`;
  - names use the document's own prefixes, and always `psk:` for the Print Schema keywords;
  - prefixes are used rather than namespace URIs because a URI can change with the driver's version.
  - An option is named by its own name, or, when it has none, by its scored properties, such as `psk:PagesPerSheet=4`.
- **Left out of the list:**
  - options the device or an administrator rules out (`psk:DeviceSettings`, `psk:AdminSettings`). Those constrained only by the current ticket (`psk:PrintTicketSettings`) stay listed, since another setting could allow them;
  - base64 parameters, such as the DEVMODE snapshot;
  - secrets, parameters whose `psf:UnitType` starts with `Encrypted`, such as Microsoft Print to PDF's PDF passwords. The driver expects them encrypted, and a plaintext value would sit in the job's settings and spool files. They can still be set by key, by an application that knows how to encrypt them;
  - features the typed options cover.
- **When a PDF job is printed,** its `windows:feature:` and `windows:parameter:` keys are checked against the driver's Print Capabilities before anything is rendered. The Print Capabilities are read for the job's own starting point: its saved `windows:devmode`, or the driver's defaults, with its `windows:print-ticket` merged on.
  - A feature or parameter the printer lacks, an option it doesn't offer, or a number out of range is refused with `ArgumentException`, which says what the printer does offer.
  - So is an option the device or an administrator rules out: it isn't listed, and the driver would only swap it for another without saying.
  - The chosen option is copied from the document, scored properties and all, into a delta ticket. It carries any namespace declarations made below the document's root, so its names still resolve.

  - a key for a feature a typed option covers is accepted unless the job sets that typed option; copies and orientation are always set, so their keys are always refused.
- **The order a job's settings are built in:**
  1. `windows:devmode`, or the driver's defaults;
  2. `windows:print-ticket`;
  3. the feature and parameter keys;
  4. the typed options;
  5. the driver's check.
- **Raw jobs** check the keys' form only, and ignore them, as they ignore `windows:devmode`.

### The Windows API (Latiprimere.Windows)

- **`IWindowsPrinter : IPrinter`,** public, which every `windows:` printer implements. It offers:
  - `QueueName`;
  - `GetPrintCapabilitiesAsync(string? printTicket)`: the driver's document, for its defaults with an optional ticket merged on;
  - `GetDefaultPrintTicketAsync()`: the driver's defaults as a ticket.

  Both return XML text. A ticket that isn't one is refused before the driver is asked.

### Tools

- **The CLI sample:**
  - `caps` lists vendor options with their values and defaults;
  - `print-capabilities <printer> [--ticket FILE | --default-ticket]` writes the driver's documents;
  - `print --ticket FILE` sends a ticket file.
- **The end-to-end harness:**
  - `ipp.vendor-options.listed-and-sent` checks the HP and `ippeveprinter` profiles on the scripted printer, and that a listed value reaches it;
  - `windows.vendor-options.listed-and-sent` checks Microsoft Print to PDF, and that a feature it lacks is refused;
  - CI's Native AOT check runs both;
  - manual mode saves the vendor options, and on Windows the driver's two documents.

## Consequences

- **What a printer offers can be shown and chosen in an application** without knowing IPP or the Print Schema. One example in the docs picks the best print quality on either print system.
- **Each IPP capabilities read is two requests,** one after the other: one more round trip than before.
- **Each Windows capabilities read opens a Print Ticket provider,** and a PDF job with feature keys opens one more to read the Print Capabilities. Both are milliseconds for the drivers examined. For a queue shared from a print server, the driver may ask the server, which can take longer. The default DEVMODE is read once, for both the typed capabilities and the Print Capabilities.
- **A driver without Print Ticket support is found out once:** the same `IPrinter` doesn't try its Print Capabilities again, or log about them again. Capability reads made only to look up a job's paper and tray don't open a Print Ticket session at all.
- **The adapters now log** what they carry on without: Print Capabilities that couldn't be read, at Information for a driver without Print Ticket support and Warning for an unreadable document, and IPP vendor options that couldn't be read, at Warning. They log through the `ILoggerFactory` that dependency injection provides, under `Latiprimere.Windows` and `Latiprimere.Ipp`. The public factories log nothing, as before. `Latiprimere.Ipp`, `Latiprimere.Cups` and `Latiprimere.Windows` now reference `Microsoft.Extensions.Logging.Abstractions`.
- **A Windows without the Print Ticket API** (no `prntvpt.dll`) is a `PrintException` at the interop boundary, so its capabilities list no vendor options rather than failing.
- **Prefixes like `ns0000` are the Print Ticket API's.** They have stayed the same across calls and driver versions so far. If a driver ever renumbered them, a saved key would be refused with a message naming what the printer offers, rather than misread.
- **Vendor options stay unportable,** and the docs say so. The typed options remain the first choice.
- **Not done:**
  - nested features (a feature inside an option), which `windows:print-ticket` still reaches;
  - IPP collections such as `media-col` and `finishings-col`;
  - PPD-only CUPS options, which CUPS doesn't expose over IPP.
- **Verified:**
  - against the four captured IPP responses; every listed value round-trips with the tag it was listed with;
  - against Microsoft Print to PDF, XPS Document Writer and the HP class driver. Through the HP's driver, pages per sheet 4, Best quality and a photo paper merged and read back from the DEVMODE.
  - XPS Document Writer keeps the image setting in its DEVMODE but stores GDI page images the same way whichever is chosen, so its output doesn't show it.
  - Nothing has been printed on paper with vendor options yet.
