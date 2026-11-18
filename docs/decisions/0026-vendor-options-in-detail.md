# 0026: Vendor options in detail

- Status: Accepted; `windows:print-ticket`, left for later here, is decided in [0031](0031-windows-print-tickets.md)
- Date: 2026-11-16

## Context

[ADR 0012](0012-vendor-option-escape-hatch.md) (accepted) chose a string dictionary on `PrintOptions` for print-system-specific settings, keyed `ipp:`, `cups:` and `windows:`, and left the details open: how values become IPP attributes, what happens when a key clashes with a typed option, and which Windows keys come first. This ADR settles them. The choices are mine, for the owner to review.

SharpIppNext's typed requests can't carry attributes it doesn't model. It can turn a typed request into its raw form (`CreateRawRequest`), which can be extended and sent (`SendAsync`). Errors from the raw path are reported the same way as from the typed one.

## Decision

- **`PrintOptions.VendorOptions`**, an `IReadOnlyDictionary<string, string>`, empty by default.
  - It is copied when set, so changing the caller's dictionary afterwards changes nothing.
  - Each key is a prefix, `ipp:`, `cups:` or `windows:`, then a name. Any other key is refused with `ArgumentException` when set, so a mistyped prefix doesn't go unnoticed.
  - Record equality compares the dictionary by reference, as `PrinterCapabilities` does with its lists.
- **Which print system reads which keys:**
  - direct IPP reads `ipp:`;
  - CUPS reads `ipp:` and `cups:`, which are encoded the same way;
  - Windows reads `windows:`, and `ipp:` too for a PDF job it sends straight to the printer over IPP ([ADR 0027](0027-direct-ipp-for-windows-queues.md)).

  Each ignores the others' keys.
- **IPP encoding:**
  - The name after the prefix is the attribute name. It must be an IPP keyword: lower-case letters, digits, `-`, `_` and `.`, starting with a letter.
  - Values separated by commas become several values of the attribute (`1setOf`), such as `ipp:finishings = 4,5`.
  - Each value's type is inferred: an integer is an `integer`, or an `enum` for the attributes IPP defines as enums (`finishings`, `orientation-requested`, `print-quality`, `image-orientation`, `input-orientation-requested` and `media-source-feed-orientation`); `true` and `false` are a `boolean`; a value in keyword syntax is a `keyword`; anything else is a `name`.
  - The Print-Job operation attributes (`ipp-attribute-fidelity`, `job-mandatory-attributes`, `document-name`, `compression`, `document-natural-language`, `job-k-octets`, `job-impressions` and `job-media-sheets`) go in the operation group; everything else goes in the job group.
  - **A key naming an attribute Latiprimere already sends is refused** with `ArgumentException`: for example `ipp:copies` with `Copies` set, `ipp:media` with `MediaSize` set, or `ipp:printer-uri` at any time. Sending an attribute twice is malformed, and silently preferring one would hide the mistake. On CUPS, `ipp:x` and `cups:x` together are refused for the same reason.
  - **Unknown attributes are sent as they are**, as ADR 0012 decided. A printer that doesn't support one ignores it, or refuses the job with `client-error-attributes-or-values-not-supported` (1035).
  - Jobs without vendor options use SharpIppNext's typed request, as before. Only jobs with vendor options use the raw form.
- **Windows:**
  - **`windows:devmode`** is a whole DEVMODE, base64-encoded, saved earlier from the same printer's driver. For example, it can come from `PrinterSettings.GetHdevmode()` in System.Drawing after the user has chosen settings in the driver's dialog.
    - It replaces the driver's defaults as the starting point. The typed options are then applied on top, as they are to the defaults today. Copies are always set, and orientation follows the document's first page unless `Orientation` is chosen.
    - The driver then checks the result, as for every job.
    - Raw jobs ignore it, as they ignore the other settings.
    - A value that isn't base64, or isn't a DEVMODE (too short, or with sizes that don't add up), is refused with `ArgumentException`.
  - **Any other `windows:` key is refused** with `NotSupportedException`, naming the keys Windows supports. Unlike IPP, Windows has nowhere to pass an unknown setting on to. `windows:print-ticket` stays for later: it needs `prntvpt.dll` interop.

## Consequences

- Applications can reach any IPP attribute and any setting a Windows driver keeps in its DEVMODE, including its private data.
- The type inference can't express collections, ranges, resolutions, dates or text with a language. Collections such as `media-col` matter most. A suffix that names the type, as ADR 0012 suggested, can be added without breaking anything.
- An `enum` attribute IPP adds later, and not in the list, would be sent as an `integer`, which a strict printer refuses. The list can grow.
- A saved DEVMODE ties a job to one driver, and maybe one driver version: a different driver may ignore its private data. That is the nature of a DEVMODE, and is documented.
