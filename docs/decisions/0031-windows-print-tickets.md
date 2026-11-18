# 0031: Print Tickets on Windows

- Status: Accepted; reading Print Capabilities, left undone here, is decided in [0032](0032-vendor-options-in-capabilities.md)
- Date: 2026-11-18

## Context

[ADR 0012](0012-vendor-option-escape-hatch.md) planned `windows:print-ticket`: a Print Ticket merged with the driver's settings through the Windows Print Ticket API. That API reaches vendor features through their published namespaces. [ADR 0026](0026-vendor-options-in-detail.md) shipped `windows:devmode` first and left the ticket for later, because it needs `prntvpt.dll` interop. It is the last open feature of Phase 2.

The Print Ticket API is COM-based: its tickets travel in `IStream`s. The packages must stay trimmable and Native AOT compatible ([ADR 0002](0002-target-framework-and-interop.md)), so built-in COM interop isn't an option.

## Decision

- **`windows:print-ticket` is a Print Ticket in XML,** usually a delta ticket that names only what it changes, such as `psk:PageMediaSize` = `psk:ISOA5`, or a vendor feature in the driver's own namespace.
- **When submitted, the value is checked and re-encoded:**
  - it must be XML whose root is `PrintTicket` in the Print Schema Framework namespace, or it is refused with `ArgumentException`;
  - document type definitions are refused, so the parser fetches and expands nothing;
  - it is written out again as UTF-8 with a matching declaration, because a .NET string often declares `utf-16`;
  - whether its features mean anything is for the driver to say.

  It is checked for raw jobs too, as `windows:devmode` is, so a bad value is never silently ignored.
- **The job's settings are built in this order** (PDF jobs only):
  1. the saved `windows:devmode`, or the driver's defaults;
  2. the Print Ticket, merged on through the driver;
  3. the typed options;
  4. the driver's check, through `DocumentProperties`, as for every job.

  So typed options win, as they do over a saved DEVMODE. Copies always come from `Copies`, and orientation from `Orientation` or else the document's first page.
- **The merge runs on the job's thread, with one provider per job.** It calls `PTOpenProvider`, then `PTConvertDevModeToPrintTicket` for the base ticket, then `PTMergeAndValidatePrintTicket`, then `PTConvertPrintTicketToDevMode`. All of it is job scope, and the user's defaults fill anything the ticket leaves out. COM is initialised as a multithreaded apartment for the call if the thread has no apartment yet.
- **`S_PT_CONFLICT_RESOLVED` is a success:** the driver changes conflicting settings, as `DocumentProperties` does for a DEVMODE.
- **COM without COM interop:** streams come from `CreateStreamOnHGlobal`. Their `IStream` pointers are held in a SafeHandle, and only `Write`, `Seek` and `Release` are called, through unmanaged function pointers on the vtable. No COM wrappers are generated, and nothing needs reflection.
- **Errors:**

  | Failure | Exception |
  |---|---|
  | `E_PRINTTICKET_FORMAT` or `E_DELTA_PRINTTICKET_FORMAT`: the driver finds the ticket malformed | `ArgumentException`, with the driver's message |
  | An HRESULT that wraps a Win32 error | The Windows error mapping ([ADR 0019](0019-windows-error-mapping.md)), such as `PrinterNotFoundException` |
  | Anything else | `PrintException`, with the HRESULT as its native error code |
- **Direct IPP for Windows queues** ([ADR 0027](0027-direct-ipp-for-windows-queues.md)) already sends any job with a `windows:` key through the spooler, so a ticket always reaches the driver.

## Consequences

- **Vendor features are reachable on Windows,** such as stapling, punching, a driver's quality modes and booklet printing, without a typed option for each. A ticket is specific to a driver, as a DEVMODE is.
- **A driver without Print Ticket support,** an old GDI driver whose provider can't be opened, fails the job with `PrintException`. Windows supplies a provider for most drivers.
- **Tests:**
  - unit tests check the value's parsing;
  - integration tests merge a real ticket for Microsoft Print to PDF, and see the driver refuse a malformed one;
  - the end-to-end scenario `windows.output.print-ticket` prints on A5 from a ticket, and shows that a typed `MediaSize` wins. It is one of the scenarios CI's Native AOT check runs, since function pointers into COM are worth checking natively.
- **Not done:** reading the printer's Print Capabilities (`PTGetPrintCapabilities`), which would list its vendor features and their options. Callers find them in the driver's documentation, or from a ticket saved by an application.
