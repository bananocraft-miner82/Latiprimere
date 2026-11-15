# 0012: Vendor-specific option escape hatch

- Status: Accepted
- Date: 2026-11-11

## Context

The capability model ([ADR 0011](0011-capability-model.md)) and `PrintOptions` cover what every print system shares. Real jobs often need more, such as print quality, a media type like glossy photo paper, stapling, or a driver's own settings. These differ between print systems, and often between printers. Without a way to pass them through, applications that need one setting fall back to the platform API for everything.

## Options

- **(a) Nothing.** Applications that need more use the platform API directly. Simplest, but the library stops being useful exactly where printing gets hard.
- **(b) A typed option for each feature** (quality, media type, finishing) added to `PrintOptions` over time. Portable, but slow to grow, and it still can't reach a driver's own settings.
- **(c) A string dictionary of print-system-specific settings** on `PrintOptions`, keyed by print system:
  - `ipp:print-quality = 5` and `ipp:finishings = 4`: sent as IPP job attributes to CUPS queues and network printers.
  - `cups:fit-to-page = true`: sent only to CUPS, which treats its options as job attributes.
  - `windows:print-ticket = <PrintTicket XML>`: merged with the driver's settings through the Windows Print Ticket API (`PTMergeAndValidatePrintTicket`, then `PTConvertPrintTicketToDevMode`), which also reaches vendor features through their published namespaces.
  - `windows:devmode = <base64>`: a complete DEVMODE saved earlier from the driver, for settings only its private data holds.

  Each print system uses only its own keys and ignores the others, so one `PrintOptions` can carry settings for several print systems.

## Recommendation

(c), with (b) added over time for the settings that prove common, such as quality and media type.

- **Unknown keys** for a print system that is handling the job are sent as they are. An IPP printer that rejects them answers 1035, as it does for any other unsupported option.
- **The IPP attribute type** is inferred from the value: an integer, `true`/`false`, or a keyword. A suffix can override this, such as `ipp:media-col` holding a collection, if that turns out to be needed.
- **Windows Print Ticket support needs `prntvpt.dll` interop**, which is new work; the DEVMODE key is simpler and could come first.

## Decision

Option (c), as recommended (owner decision 2026-11-11):

- `PrintOptions` gets a dictionary of print-system-specific settings, keyed `ipp:`, `cups:` and `windows:`.
- Each print system uses only its own keys and ignores the rest.
- Typed options are added over time for the settings that prove common, starting with quality and media type.
- Unknown keys for the print system handling the job are passed on as they are.
- On Windows, the DEVMODE key may come before the Print Ticket key, since it needs no new interop.

## Consequences

- Applications can reach any IPP attribute or Windows driver setting without leaving Latiprimere.
- Settings in the dictionary are not portable by design. The documentation must say so, and point to the typed options where they exist.
- Phase 2 gains the Windows Print Ticket interop and IPP attribute encoding for the dictionary values.
