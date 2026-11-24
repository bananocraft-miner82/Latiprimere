# API review of Phase 4's additions

> Status: **Decided and applied** (2026-11-23): all six as recommended (owner: "Accept all"). Part of P4-6 in the [Phase 4 plan](phase-4-plan.md), as an addendum to the [1.0 review](api-review-1.0.md) ([ADR 0040](decisions/0040-api-review-1-0.md)). Recorded in [ADR 0057](decisions/0057-api-review-phase-4.md).

## Why now

The 1.0 review's reasons still hold: after 1.0, a rename or a change to an interface waits for a major version, and the adapter-facing interfaces are frozen ([ADR 0044](decisions/0044-room-to-grow-after-1-0.md)). Phase 4 added to the API twice:

- **P4-2 to P4-5,** shipped in 11.0.9-preview: renaming those needs a compatibility suppression, as the 1.0 review's renames did;
- **P4-8 to P4-10,** not shipped yet, in `PublicAPI.Unshipped.txt`: those can change freely.

## What Phase 4 added

| Package | Shipped in 11.0.9-preview | Not shipped yet |
|---|---|---|
| `Latiprimere.Abstractions` | `DocumentFormat.Jpeg`, `Png`, `Text`; `PrintContent.FromJpeg`, `FromPng`, `FromText`, `TextEncoding`; `ScalingMode` and `PrintOptions.Scaling`; `PrinterCapabilities.DocumentFormats`; `PrinterStatus`, `IPrinter.GetStatusAsync` and `WatchStatusAsync`; `IPrintJob.WatchStatusAsync` as a member | `PageRange`, `PrintQuality`, `Finishing`, `OutputBin`, `MediaType`, `MediaSizeRange`, `MediaSize.Custom`; `PrintOptions.PageRanges`, `Collate`, `Quality`, `Finishings`, `OutputBin`, `MediaType`; the matching `PrinterCapabilities` members and `SupportsPageRanges`; `PrintContent.FromPdf(stream, password)` and `Password`; `PrintOptionKinds`, `IPrinter.AppliedOptions`, `IPrinter.GetFeature<T>()` and `IPrintJob.GetFeature<T>()`. In `Latiprimere.Rendering`: `RasterPixelFormat`, `RasterBand.PixelFormat`, `RasterRenderOptions`, `PdfOpenOptions`, and the options parameters of `IPdfRasterizer.Open` and `IRasterDocument.RenderBand` |
| `Latiprimere` | `PrinterExtensions.WatchStatusAsync`, `PrintJobExtensions` kept beside the new member | — |
| `Latiprimere.Ipp` | `IppRasterFormat`, `IppNetworkOptions.RasterFormats` | — |
| `Latiprimere.Windows` | `WindowsOptions.IppRasterFormats` | — |
| `Latiprimere.Rendering.Pdfium` | — | `PdfiumRasterizer.Open(stream, options)` |

## Overall

The shape holds again. Everything new is a record property, a record, an enum, or an interface member with a default implementation, so applications' test fakes compile unchanged. The rasteriser's options are records, so the frozen interfaces needn't change again. No SharpIppNext, PDFium or Win32 type is public.

Checked and fine as they are:

- **`ScalingMode.None`** for an image at its own size, as WPF's `Stretch.None` means, and IPP's `print-scaling` `none`.
- **`IppRasterFormat.Pclm`,** cased as .NET cases acronyms of three letters or more.
- **Following a printer needs a token** (`PrinterExtensions.WatchStatusAsync(printer, cancellationToken)`), where following a job doesn't: a printer never reaches a final state. Printers are polled every five seconds and jobs every half second, as each changes at its own pace.
- **The new options follow the old ones' names:** `Quality` and `PrintQuality` as `Duplex` and `DuplexMode`; `PrintQualities` and `DefaultPrintQuality` in the capabilities as `DuplexModes` and `DefaultDuplexMode`; `OutputBin` and `MediaType` as `InputTray`, an ID and a name.
- **`PrintContent.Password`** is public because adapters of other people's need it. `PrintContent` is a class with no generated `ToString`, so the password doesn't show in logs or the debugger's summary, and `PdfOpenOptions` prints only whether one is set.

## Decisions for the owner

| # | Question | Recommendation | Breaking? |
|---|---|---|---|
| 1 | The new names | Keep them all | No |
| 2 | Room in `PrintOptionKinds` | Make it a `long` before 1.0 | Only unshipped |
| 3 | IPP vendor options the typed options now cover | Stop listing `print-quality`, `output-bin`, `media-type` and `multiple-document-handling`; keep `finishings` | Behaviour only |
| 4 | Asking for no finishing | `Finishing.None` after 1.0, as an addition; 0 stays free for it | No |
| 5 | Finishings and output bins on Windows | After 1.0, through the driver's Print Capabilities, with no API change | No |
| 6 | Additions that can wait | After 1.0, as additive changes | No |

### 1. The new names

**The names the P4-9 and P4-10 plans left to this review:**

- **`GetFeature<T>()`,** after ASP.NET Core's `IFeatureCollection.Get<TFeature>()`, which reaches a server's optional features the same way. `As<T>()` reads like a conversion that always succeeds; `GetService<T>()` suggests dependency injection.
- **`AppliedOptions`** says what the printer does with the options. `SupportedOptions` would be read as the capabilities, which say which *values* a printer takes.
- **`PrintOptionKinds`,** plural as .NET names `[Flags]` enums.
- **`PageRange`,** IPP's `page-ranges`, with `First` and `Last` counted from 1. `System.Range` is zero-based and exclusive at the end, so a name of its own avoids mixing them up. An open range is `new PageRange(3, int.MaxValue)`, which the guide shows.
- **`Finishing`,** singular as .NET names enums that aren't flags, holding IPP's `finishings` names.
- **`MediaSizeRange`,** beside `MediaSize`, with `IsRoll` for a roll.
- **`SupportsPageRanges`,** `bool?` as `null` means the printer didn't say.
- **`MediaType`,** IPP's and the Windows driver's name for it. ASP.NET Core MVC has a `MediaType` struct, in `Microsoft.AspNetCore.Mvc.Formatters`, which a controller rarely imports; a file that does can write `Latiprimere.MediaType`.

**Recommendation:** keep them all.

### 2. Room in `PrintOptionKinds`

**Today** `PrintOptionKinds` is an `int` with 16 of its 32 flags used, one for each `PrintOptions` property. The catalog refuses an option a printer doesn't list, so every option added after 1.0 needs a flag. Plan.md's later tiers alone name printable area, borderless printing, resolution, pages per sheet, holding a job until later and job priority, and changing an enum's underlying type after 1.0 breaks every compiled caller.

**Recommendation:** `[Flags] public enum PrintOptionKinds : long`, with CA1028 suppressed and the reason given: 64 flags is room for the life of the major version. Nothing has shipped, so nothing needs a suppression.

**Alternative:** keep `int`, and when the flags run out, add a second property for the rest, which every adapter would then have to fill in.

### 3. IPP vendor options the typed options now cover

**Today** a printer's capabilities list, among its vendor options, every IPP job attribute the typed options don't send ([ADR 0032](decisions/0032-vendor-options-in-capabilities.md)): `copies`, `sides`, `media` and the others are left out. P4-10 made `print-quality`, `output-bin`, `media-type` and `multiple-document-handling` typed, but they are still listed. Setting one beside its typed option is refused already, as for `copies`.

**Recommendation:** leave out those four, as ADR 0032 does for the rest; a key on its own still works as now, for code that already sets it. Keep `finishings` listed, as it holds many more values than `Finishing` names: covers, binding, trimming and more.

**Cost:**

- the vendor options guide's example, which picks the best print quality from the vendor options, becomes one that picks two pages per sheet, and the typed `Quality` takes its place in the options guide;
- the harness scenarios that read `ipp:print-quality` from the list read `ipp:printer-resolution` (`ipp.vendor-options.listed-and-sent`) or pages per sheet (`samples.examples.vendor-options`) instead;
- callers that read `ipp:print-quality` from the list find it gone. There are none yet.

**Alternative:** keep listing them, as ADR 0056 left them. Then a printer offers two ways to set its quality, and only one of them works on every print system.

### 4. Asking for no finishing

**Today** an empty `Finishings`, the default, sends nothing, so the printer uses its own default, which is usually nothing. A queue set up to staple every job staples it. IPP's `finishings` has `none` for that, and `Finishing` leaves 0 free.

**Recommendation:** add `Finishing.None = 0` after 1.0, if someone asks: it's an addition, as `Finishing` is among the enums that may grow (CONTRIBUTING.md). The docs now say that an empty list means the printer's default.

### 5. Finishings and output bins on Windows

**Today** Windows printers leave `Finishings` and `OutputBin` out of `AppliedOptions`, so the catalog refuses jobs that set them. The DEVMODE has no fields for either; drivers offer them in their Print Capabilities, as `psk:JobStapleAllDocuments`, `psk:JobHolePunch` and `psk:JobOutputBin`, which Latiprimere already reads and merges for vendor options ([ADR 0031](decisions/0031-windows-print-tickets.md)).

**Recommendation:** after 1.0, map them to Print Ticket features and add both to Windows printers' `AppliedOptions`. That needs no API change, only the adapter, and the `windows:feature:` vendor options reach them meanwhile. It needs a driver with a stapler to test against, which the HP isn't.

### 6. Additions that can wait

After 1.0, as additive changes:

- **`PageRange.Parse("1-3,5")`,** for applications taking ranges as text, as the CLI sample does;
- **options checked against the capabilities** in the catalog's layer, before anything is sent;
- **printers appearing and going** (decision D10 of the Phase 4 plan).
