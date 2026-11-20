# 0044: Room to grow after 1.0

- Status: Accepted (owner, 2026-11-20: "Accept ADR 0044")
- Date: 2026-11-20

## Context

From 1.0, the public API changes by additions only, and a breaking change waits for the next .NET major ([ADR 0040](0040-api-review-1-0.md); "Changing the Public API" in CONTRIBUTING.md). Most notably:

- `IPrintSystem`, `IPdfRasterizer` and `IRasterDocument`, the interfaces adapters implement, don't change even with default implementations;
- other interfaces gain members only with a default implementation.

[plan.md](../plan.md) lists four things for later: UI add-ons, image and plain-text content helpers, printer event subscriptions, and Android and iOS. If any of them needed a change the rules forbid, the time to make room is before 1.0.

Item 6 of the review of what's left before 1.0. Owner: "Start items 5 and 6".

## Decision

Each of the four can be added after 1.0 without breaking anything. The routes:

- **UI add-ons** (print dialogs and preview for Avalonia, MAUI, WPF and WinForms): new packages that only use the public API.
  - A dialog's choices come from `PrinterCapabilities`.
  - A preview is rendered with `IPdfRasterizer` and `IRasterDocument`, already public.
  - No core package gains a UI type, as CONTRIBUTING.md requires.
- **Image and plain-text content:**
  - new `DocumentFormat` values, such as `Jpeg`, `Png` and `Text`;
  - `PrintContent` factories for them, beside `FromPdf` and `FromRaw`. Its constructor is private, so only Latiprimere makes content;
  - `PrinterCapabilities` may gain the formats a printer takes, as a new property.

  A print system that doesn't take a new format throws `NotSupportedException`, as `IPrinter.SubmitAsync` already documents for a format a printer can't accept. So adapters, ours and others', keep working before they learn it. IPP printers take JPEG themselves, and CUPS converts images. The Windows spooler would need images drawn through GDI, as PDFs are.
- **Printer and job events** (IPP notifications, Windows change notifications):
  - **an optional interface,** such as `IPrintSystemNotifications`, that a print system may implement besides `IPrintSystem`, rather than new members on `IPrintSystem`;
  - **new members on `IPrinterCatalog`, `IPrinter` or `IPrintJob`,** with default implementations, such as one that watches for changes by polling, as `WatchStatusAsync` does now;
  - **the catalog uses events** from print systems that offer them;
  - **`WatchStatusAsync`,** an extension method, may use a job's events when the job offers them, and keep polling otherwise.
- **Android and iOS:** new adapter packages implementing `IPrintSystem`, with platform target frameworks added to the packages that need them. Adding target frameworks breaks nobody. Direct IPP already runs wherever .NET does, though Android needs a multicast lock for discovery.
- **Enums that may gain values say so.** Adding a value to an enum doesn't break compiled code, but can surprise code that switches over every value. So the enums most likely to grow say in their documentation that they may, and what code should do with a value it doesn't know:
  - `DocumentFormat`, for image and text content;
  - `ColorMode`, as IPP also has bi-level and process colour;
  - `PageOrientation`, as IPP also has reverse portrait and reverse landscape;
  - `VendorOptionKind`, for kinds of value printers may add.

  The others follow the states and modes of IPP itself, which are fixed: `PrinterState`, `PrintJobState`, `DuplexMode` and `RasterRotation`. They don't gain values. CONTRIBUTING.md says which enums may grow.

## Consequences

- **Nothing in the API changes before 1.0,** except the documentation of the four enums.
- **The rules hold:** none of the four needs a change to `IPrintSystem`, a removal or a rename.
- **Code that switches over the four enums** is told to handle values it doesn't know. Latiprimere's own code does: adapters throw `NotSupportedException` for a document format they don't know, and send no colour mode or orientation they don't know, leaving the printer's default. IPP printing threw `ArgumentOutOfRangeException` for an unknown document format, contrary to `SubmitAsync`'s documentation; it now throws `NotSupportedException`. No format Latiprimere offers today reaches it.
- **CONTRIBUTING.md** says which enums may grow, and where new features go.
- **If something unforeseen does need a breaking change,** it waits for the next .NET major, under [ADR 0030](0030-release-numbering.md)'s numbering: one a year, each November.
