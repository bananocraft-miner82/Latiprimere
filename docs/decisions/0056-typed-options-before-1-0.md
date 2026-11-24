# 0056: Typed options before 1.0

- Status: Accepted (owner, 2026-11-23: "Accept all", for [phase-4-plan.md](../phase-4-plan.md)'s D16 and D17; built as P4-10, "Start P4-10")
- Date: 2026-11-23

## Context

Some settings were reachable only as vendor options, whose keys differ by print system: print quality, finishings, output bin and media type. Others were not reachable at all: page ranges (`page-ranges` is refused as a vendor option, [ADR 0032](0032-vendor-options-in-capabilities.md)), collation, and custom or roll paper, whose limits capabilities dropped ([ADR 0011](0011-capability-model.md)). Label and receipt printers, and the Documents companion ([ADR 0053](0053-document-generation-companion.md)), need the last.

They are additions, which ADR 0044's rules allow after 1.0. They come before it because each needs mapping to IPP and the DEVMODE together, and because an option added later could be ignored by an adapter that doesn't know it: [ADR 0055](0055-catalog-printers.md)'s `AppliedOptions` now guards that, and this is its first use.

Phase 4's plan, P4-10, with decisions D16 and D17 (owner: "Accept all"; then "Start P4-10").

## Decision

### Public API

- **`PageRange`,** a record struct of the first and last page, counted from 1, both included. `ToString` writes `3` or `2-5`.
- **`PrintOptions`** gains:
  - `PageRanges`: empty for every page; in order, not overlapping, as IPP requires, or `ArgumentException`; copied when set;
  - `Collate`: `bool?`, `null` for the printer's default;
  - `Quality`: `PrintQuality`, `PrinterDefault`, `Draft`, `Normal` or `High`, as IPP's `print-quality` has them;
  - `Finishings`: a list of `Finishing`, without repeats;
  - `OutputBin` and `MediaType`: records of an ID and a name, like `InputTray`.
- **`Finishing`** names the common ones (D17): a staple, four corner staples, two staples left and top, a punch, two holes left and top, and a fold. It may grow, and joins the enums CONTRIBUTING.md says may. The rest of IPP's finishings stay vendor options.
- **`MediaSizeRange`,** custom or roll limits in millimetres, with `IsRoll` and `Contains(size)`, to a tenth of a millimetre.
- **`MediaSize.Custom(width, height)`,** a size named `custom_<w>x<h>mm_<w>x<h>mm`, as printers and Windows name sizes without a standard name, to the hundredth of a millimetre.
- **`PrinterCapabilities`** gains `CustomMediaSizes`, `MediaTypes`, `PrintQualities` and `DefaultPrintQuality`, `Finishings`, `OutputBins` and `SupportsPageRanges`.
- **`PrintOptionKinds`** gains `PageRanges`, `Collate`, `Quality`, `Finishings`, `OutputBin` and `MediaType`, and `IPrinter.AppliedOptions`' default every one of them, as the 1.0 set.
- **Paper stays only a size** (D16): margins, borderless printing, type and tray stay off `MediaSize`, whose equality compares every property.

### IPP and CUPS

- **Job attributes:** `page-ranges`; `multiple-document-handling`, collated or uncollated copies; `print-quality`; `finishings`; `output-bin`.
- **`media-col`** now carries `media-type`, and a custom size as `media-size` dimensions, as a printer may not know a custom name. A listed size alone still goes as the `media` keyword.
- **Raster:** the pages are chosen as they are rendered, so `page-ranges` isn't sent with raster. Two-sided sheets alternate by the pages printed, not by their place in the document, so pages 2 and 3 are a front and a back. The quality goes in the raster page headers too.
- **Raw data** with page ranges is refused with `NotSupportedException`: its pages can't be seen.
- **Vendor options:** a key for an attribute the typed options now send, such as `ipp:print-quality` with `Quality`, is refused, as for the others; a key alone works as before. The capabilities still list `ipp:print-quality`, `ipp:finishings` and `ipp:output-bin` among vendor options: `finishings` holds more than `Finishing` names, and the other two are left for P4-6's API review to decide, as removing them would change what callers read today.
- **Capabilities:** `media-type-supported`, `output-bin-supported`, `print-quality-supported` and `-default`, `finishings-supported` (only those `Finishing` names), `page-ranges-supported`, and the `custom_min_`, `custom_max_`, `roll_min_` and `roll_max_` names among `media-supported`, a range for each class that has both ends.

### Windows

- **Page ranges:** only the pages chosen are rendered. Ranges that leave none fail with `ArgumentException` before anything is spooled; raw data with page ranges is refused.
- **Collation:** `dmCollate` false when the job says so; copies Latiprimere renders itself, for a driver that caps them, come page by page then.
- **Quality:** `dmPrintQuality`: `DMRES_DRAFT`, `DMRES_MEDIUM` for normal, `DMRES_HIGH`. Windows reports no qualities in the capabilities.
- **Media types:** `DC_MEDIATYPES` and `DC_MEDIATYPENAMES`, IDs being the `DMMEDIA_*` numbers; sent as `dmMediaType`. A type the driver doesn't list is refused, as for trays.
- **Custom sizes:** `DC_MINEXTENT` to `DC_MAXEXTENT`, reported only when the driver lists `DMPAPER_USER` among its papers, as drivers that take no custom size may still give extents. A size the driver doesn't list but takes goes as `DMPAPER_USER` with `dmPaperWidth` and `dmPaperLength`.
- **Finishings and output bins aren't applied.** The driver reaches them only through its Print Capabilities, as `windows:feature:` vendor options already do. So `WindowsPrinter.AppliedOptions` leaves them out, and the catalog refuses a job that sets them, rather than printing without staples. A Windows queue sent over IPP could apply them; that is a later addition.

## Consequences

- **Additive:** nothing existing changes, so package validation needs no suppressions for this package.
- **Tests:**
  - **Abstractions:** ranges' bounds and text, order and overlap refused, copies; defaults of every new setting; finishings without repeats; custom sizes' names and their refusal of zero, negative, infinite and NaN dimensions; a roll's range.
  - **IPP:** each attribute; collated copies; media type and custom size in `media-col`; the typed and vendor quality refused together and the vendor one alone sent; every new capability, with custom and roll ranges, and none for a minimum alone; pages selected, past the end left out, none refused; raster rendering only the chosen pages without sending `page-ranges`; a PDF sent as it is with them; raw refused.
  - **Raster encoder:** pages 2 and 3 of three encoding as two pages, front and back, alike; ranges past the end refused before anything is rendered.
  - **Windows:** each DEVMODE field; the order of pages collated and not; the driver's types and custom range, only with `DMPAPER_USER`; extents unpacked; lists combined; the GDI request given only the chosen pages, a custom size's dimensions and the type; a size outside the range and an unlisted type refused; raw refused; finishings and output bins left out of `AppliedOptions`.
  - **The harness:** `windows.output.page-ranges` prints pages 2 and 3 of a three-page PDF, at draft quality, to Microsoft Print to PDF, and gets two pages with text on each.
- **The sign-off** gains page ranges with collated copies over IPP and through the HP's Windows queue, and draft against high quality (P4-7).
- **For P4-6's review:** the names `PageRange`, `Finishing`, `MediaSizeRange` and `SupportsPageRanges`; whether IPP still lists vendor options the typed ones now cover; and finishings and output bins on Windows through Print Tickets.
