# 0011: Capability model

- Status: Proposed
- Date: 2026-11-11

## Context

Applications need to know what a printer can do before choosing options: which paper sizes, whether it can print on both sides or in colour, how many copies it will make, and which trays it has. IPP printers and CUPS queues describe this in printer attributes. Windows drivers describe it through `DeviceCapabilities` and their default DEVMODE. Spike 5 had to find one model both can fill, and identify what doesn't map.

## Decision

- **`IPrinter.GetCapabilitiesAsync()` returns a `PrinterCapabilities` record:**
  - paper sizes and the default size;
  - duplex modes and the default;
  - colour modes and the default;
  - the most copies a job can ask for;
  - orientations;
  - input trays.

  New types in `Latiprimere.Abstractions`: `MediaSize`, `ColorMode` (with `PrinterDefault`, like `DuplexMode`) and `InputTray`.
- **Empty means "not reported", not "not supported".** An empty list, or a `null` default, means the print system didn't say. Lists never contain the `PrinterDefault` values.
- **Paper sizes are named by their PWG 5101.1 self-describing name on every print system**, such as `iso_a4_210x297mm`, so the same paper has the same name everywhere. Each size also carries a display name and its width and height in millimetres, in portrait.
  - **IPP:** names come from `media-supported` and `media-default`, and the dimensions are parsed from the name. Display names are derived from the name ("A4", "Number 10", "B5 (JIS)"). Custom and roll size limits (`custom_min_…`, `roll_max_…`) are left out, because they describe ranges, not sizes.
  - **Windows:** the driver's sizes are turned to portrait and matched, within 1 mm, against a table of 27 common sizes. Unmatched sizes are named `custom_<w>x<h>mm_<w>x<h>mm`. The driver's own name is the display name. When a driver lists one size twice (for example "Letter" and "Letter Rotated"), the first is kept.
- **IPP mapping:**

  | Capability | Attributes | Notes |
  |---|---|---|
  | Duplex | `sides-supported`, `sides-default` | `one-sided`, `two-sided-long-edge`, `two-sided-short-edge` |
  | Colour | `print-color-mode-supported`, `print-color-mode-default` | All monochrome and bi-level variants become Monochrome. If the supported list is missing, `color-supported` is used, then the default alone |
  | Copies | `copies-supported` | The upper bound of the range |
  | Orientation | `orientation-requested-supported` | Portrait and landscape only |
  | Trays | `media-source-supported` | The keyword is the ID; the name is derived from it |

  The same code serves CUPS queues and direct IPP printers, since both are `IppPrinter`.
- **Windows mapping:**
  - **Lists:** from `DeviceCapabilitiesW`: DC_PAPERS, DC_PAPERNAMES and DC_PAPERSIZE for paper; DC_BINS and DC_BINNAMES for trays; DC_DUPLEX, DC_COLORDEVICE, DC_COPIES and DC_ORIENTATION for the rest.
  - **Defaults:** from the driver's default DEVMODE (`dmPaperSize`, `dmDuplex`, `dmColor`), only where its `dmFields` says they are set.
  - **Duplex:** a driver that can duplex is reported as supporting all three modes, because DC_DUPLEX is a single yes or no.
- **Tray IDs only mean something to the printer that reported them**: an IPP keyword such as `by-pass-tray`, or a Windows bin number. HP's driver, for example, uses its own numbers (257, 258).
- **Tests replay real printer responses.** Get-Printer-Attributes responses captured from the test container are stored as `tests/Latiprimere.Ipp.Tests/Data/*.ipp`. Tests run them through the real SharpIppNext decoder with a replaying `HttpMessageHandler`.

## What doesn't map cleanly

- Reverse orientations (`reverse-landscape`, `reverse-portrait`), and the `auto` and `highlight` colour modes: left out.
- **Media details:**
  - IPP `media-col-database` holds margins, media types and which sizes go in which tray.
  - Windows has media types (DC_MEDIATYPES) and custom size ranges (DC_MINEXTENT, DC_MAXEXTENT).

  None of these are in the model yet.
- Print quality, resolution and finishing (staple, punch, fold) are not modelled. They differ most between printers, and are the main case for the escape hatch proposed in [ADR 0012](0012-vendor-option-escape-hatch.md).
- **Copies on Windows** report the driver's own limit. Microsoft Print to PDF says 1, although Latiprimere renders extra copies of PDF jobs itself when the driver can't.
- **Media types:** the IPP name for a size with a media type, such as photo paper, can differ from its plain size name, and Windows drivers report media types separately. Neither is handled yet.

## Consequences

- One model fills from all three print systems. Verified live:
  - two Windows drivers: Microsoft Print to PDF, and an HP ENVY 5640 read with the printer switched off;
  - three CUPS queues;
  - one direct IPP printer.
- Applications can compare paper sizes across print systems by name.
- `PrintOptions` can't yet choose a paper size, colour mode or tray; applying those is Phase 2 work.
- The Windows size table needs to grow as real drivers show common sizes it misses. Sizes it doesn't know still work, under a `custom_` name.
