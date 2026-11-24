# 0057: Public API review of Phase 4's additions

- Status: Accepted
- Date: 2026-11-23

## Context

Phase 4 added to the public API before it freezes at 1.0 ([ADR 0044](0044-room-to-grow-after-1-0.md)): images, text, raster formats and following printers (P4-2 to P4-5, shipped in 11.0.9-preview), then the rasteriser's options, the catalog's own printers and the typed options (P4-8 to P4-10, not shipped yet). The Phase 4 plan's P4-6 asks for a review of them, as [ADR 0040](0040-api-review-1-0.md) recorded one for 1.0. The review is in [api-review-phase-4.md](../api-review-phase-4.md); the shape held, and six questions are put to the owner. Three were left to it by [ADR 0055](0055-catalog-printers.md) and [ADR 0056](0056-typed-options-before-1-0.md): the new names, the IPP vendor options that typed options now cover, and finishings and output bins on Windows.

## Decision

Owner decision, 2026-11-23: "Accept all", each as the review recommends:

1. **Names:** keep `GetFeature<T>()`, `AppliedOptions`, `PrintOptionKinds`, `PageRange`, `Finishing`, `MediaSizeRange`, `SupportsPageRanges` and `MediaType`.
2. **`PrintOptionKinds : long`,** so options added after 1.0 have flags to spare.
3. **IPP vendor options:** stop listing `print-quality`, `output-bin`, `media-type` and `multiple-document-handling` among a printer's vendor options, as ADR 0032 leaves out the other attributes the typed options send. Keep `finishings`, which holds more than `Finishing` names. Keys on their own still work.
4. **`Finishing.None`** after 1.0, if asked for; 0 stays free for it.
5. **Finishings and output bins on Windows** after 1.0, through the driver's Print Capabilities, with no API change.
6. **`PageRange.Parse`, options checked against capabilities, and printers appearing and going** after 1.0, as additions.

## Consequences

- Decisions 1, 4, 5 and 6 change nothing now.
- Decision 2 changes an unshipped enum, so no compatibility suppression is needed. CA1028 is suppressed on it, with the reason.
- Decision 3 changes what IPP printers' capabilities list, not the API. The names the vendor options gave `print-quality`'s values went with it. The vendor options guide's example and the `samples.examples.vendor-options` scenario move from print quality to pages per sheet, which IPP and Windows drivers both list in a form an example can match; `ipp.vendor-options.listed-and-sent` reads `ipp:printer-resolution`. Windows drivers still list their own features as they give them, `psk:PageOutputQuality` among them, as ADR 0032 decided for Print Capabilities.
- The docs now say that an empty `Finishings` leaves the printer's default, rather than none.
