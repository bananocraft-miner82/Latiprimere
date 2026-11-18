# 0025: Choose paper size, colour mode and tray

- Status: Accepted
- Date: 2026-11-16

## Context

0.1.0-preview reads a printer's paper sizes, colour modes and trays ([ADR 0011](0011-capability-model.md)), but `PrintOptions` can't choose them. The developer documentation written for 0.1 says so, and expects it to be the first thing users ask for. The [API review](../api-review-0.1.md) listed it as an addition that could wait for after the first preview, since adding init-only properties to `PrintOptions` breaks nothing.

The capability model already settles what these values are called:

- paper sizes by their PWG 5101.1 name on every print system;
- trays by an ID that only means something to the printer that reported it: an IPP `media-source` keyword, or a Windows bin number;
- colour as `ColorMode`, with `PrinterDefault`, like `DuplexMode`.

## Options

**How the caller names a size and a tray:**

- **(a) The records from `PrinterCapabilities`:** `MediaSize? MediaSize` and `InputTray? InputTray`. What `GetCapabilitiesAsync` returns can be passed straight back.
- **(b) Strings:** a PWG name and a tray ID. This is shorter for a caller who already knows the name, but a tray ID is meaningless without reading the capabilities first, and two strings next to each other are easy to swap.

**What happens with a value the printer doesn't list:**

- **(c) Let the print system decide,** as `Duplex` and `Orientation` already do.
- **(d) Check it against the capabilities before sending.**

## Decision

(a), and (c) except where Windows would silently print something else. These choices are mine, for the owner to review.

- **Three new `PrintOptions` properties,** all defaulting to the printer's own setting:
  - `MediaSize? MediaSize`: matched by `MediaSize.Name` only, so a size read from another printer, or with another display name, still works;
  - `ColorMode ColorMode`, `PrinterDefault` by default;
  - `InputTray? InputTray`: matched by `InputTray.Id`.
- **IPP (CUPS queues and network printers):**

  | Option | Sent as |
  |---|---|
  | Colour | `print-color-mode`: `color` or `monochrome` |
  | Size, no tray | `media`: the PWG name |
  | Tray | `media-col` with `media-source`, plus `media-size` (`x-dimension` and `y-dimension` in hundredths of a millimetre) when a size is chosen too |

  `media` and `media-col` can't both be sent, and `media-source` exists only inside `media-col`. Inside `media-col` the size is sent as dimensions, not as `media-size-name`, because IPP Everywhere requires printers to support `media-size` but not `media-size-name`, and libcups sends sizes that way too.

  Nothing is checked first: a printer that can't do something substitutes or refuses, as with duplex. Checking would cost a Get-Printer-Attributes request for every job.
- **Windows (PDF jobs):**
  - Colour sets `dmColor`. **Monochrome pages are also turned grey before the driver gets them** (owner decision, 2026-11-17: "Yes, render grey"). The end-to-end harness found that Microsoft Print to PDF ignores `dmColor` and kept a colour page in colour; Latiprimere renders every page itself, so it greys each band with ITU-R BT.601 weights instead of depending on the driver. `dmColor` is still set, for drivers that use it to choose ink.
  - The size is looked up in the driver's paper list, by the same PWG name `GetCapabilitiesAsync` gives it, and its DMPAPER number goes in `dmPaperSize`. `DM_PAPERLENGTH`, `DM_PAPERWIDTH` and `DM_FORMNAME` are cleared, since any of them would override it.
  - The tray's ID is the DMBIN number for `dmDefaultSource`.
  - **A size or tray the driver doesn't list is refused** with `NotSupportedException` before anything is queued. A driver given a DMPAPER or DMBIN number it doesn't know drops it during the merge and prints on its default paper, reporting nothing. The driver's lists are already at hand, and only read when a size or tray is chosen.
- **Windows raw jobs** don't apply them, as with duplex: raw data bypasses the driver, so the data itself chooses its paper.

## Consequences

- A job that names a size or tray costs Windows one more `DeviceCapabilities` round per job, much less than rendering it.
- The two print systems disagree on an unknown size or tray: Windows refuses it, IPP leaves it to the printer, which may print on another size. Checking on IPP too would make them agree, at the cost of a request per job. Worth revisiting if users are caught out.
- A caller who knows a PWG name but hasn't read the capabilities has to build a `MediaSize`, with dimensions. A factory from a PWG name, or common sizes as static members, can be added later without breaking anything.
- `print-color-mode` is sent as `monochrome`, which IPP Everywhere requires. A printer that lists only `process-monochrome` or `bi-level` may substitute.
- Raw jobs on Windows still ignore these settings silently. Refusing them would be a breaking change to how raw jobs treat `PrintOptions`, so it isn't done here.
