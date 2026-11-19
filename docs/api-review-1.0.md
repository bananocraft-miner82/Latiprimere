# API review for 1.0

> Status: **Decided and applied** (2026-11-19): all seven as recommended. Recorded in [ADR 0040](decisions/0040-api-review-1-0.md). Part of P3-9 in the [Phase 3 plan](phase-3-plan.md). This review describes the API as it was before; the additions in decision 7 are still to come. 11.0.6-preview, released the same day, shipped the names this review calls unshipped; the ADR says how that was handled.

## Why now

"1.0" is the first release with `RELEASE_PREVIEW` set to `false` (decision D8 of the Phase 3 plan). After it, a breaking change needs a new major version, which under [ADR 0030](decisions/0030-release-numbering.md) means a new .NET major. So this is the last cheap moment to:

- rename anything;
- add members to the public **interfaces**, which breaks every class that implements them, test fakes in applications included.

Members added to records, options classes and factories stay cheap after 1.0.

Breaking changes now aren't free either. The preview packages are on nuget.org, and package validation compares each release with the last one (`PackageValidationBaselineVersion`). A rename of anything already shipped needs an entry in a compatibility suppression file, and a `*REMOVED*` line in `PublicAPI.Unshipped.txt`. Members not yet shipped, in each package's `PublicAPI.Unshipped.txt`, can change freely.

## What is public today

| Package | Public types |
|---|---|
| `Latiprimere.Abstractions` | **For applications:** `IPrinterCatalog`, `IPrinter`, `IPrintJob`, `PrinterInfo`, `PrinterState`, `PrinterCapabilities`, `MediaSize`, `InputTray`, `VendorOption`, `VendorOptionChoice`, `VendorOptionKind`, `PrintContent`, `DocumentFormat`, `PrintOptions`, `DuplexMode`, `PageOrientation`, `ColorMode`, `PrintJobStatus`, `PrintJobState`, `PrintException`, `PrintAuthenticationException`, `PrinterNotFoundException`, `PrintSystemUnavailableException`. **For adapter authors:** `IPrintSystem`, and in `Latiprimere.Rendering`: `IPdfRasterizer`, `IRasterDocument`, `PageSize`, `RasterBand`, `RasterRotation` |
| `Latiprimere` | `PrinterCatalog`, `PrintJobExtensions` (`WatchStatusAsync`), `LatiprimereServiceCollectionExtensions` |
| `Latiprimere.Cups` | `CupsOptions`, `CupsEncryption`, `CupsPrintSystemFactory`, `LatiprimereCupsServiceCollectionExtensions` |
| `Latiprimere.Ipp` | `IppNetworkOptions`, `IppNetworkPrintSystemFactory`, `LatiprimereIppServiceCollectionExtensions` |
| `Latiprimere.Windows` | `WindowsOptions`, `IWindowsPrinter`, `WindowsPrintSystemFactory`, `LatiprimereWindowsServiceCollectionExtensions` |
| `Latiprimere.Rendering.Pdfium` | `PdfiumRasterizer`, `LatiprimerePdfiumServiceCollectionExtensions` |

Since 0.1, Phase 2 and 3 added the capability model, vendor options, Windows print tickets (`IWindowsPrinter`), raster rotation, and the IPP options for Validate-Job, raster resolution, IPPS, credentials and the address store. Everything else stays `internal`, and no SharpIppNext, Tmds.MDns or Win32 type is public.

## Overall

The shape from 0.1 has held through two phases of features without a redesign:

- the features were added as record properties, options and one new interface (`IWindowsPrinter`);
- the 0.1 decisions (one `using Latiprimere;`, `CancelAsync` returning `bool`, `IPrintSystem : IDisposable`, the public API analyzer) are all still right.

The findings are about spellings, things applications can only get by parsing text or by using dependency injection, and the last chance to add interface members freely.

## Decisions for the owner

| # | Question | Recommendation | Breaking? |
|---|---|---|---|
| 1 | British spellings in four public names | Use the .NET spellings: `Millimeters`, `Canceled`, `Counterclockwise90` | Yes; three are shipped |
| 2 | Logging without dependency injection | A factory overload taking an `ILoggerFactory`, for each print system | No |
| 3 | A refused certificate's fingerprint, as data | `PrintCertificateException : PrintException`, with `CertificateFingerprint` | No for callers |
| 4 | Following a job after a restart | `IPrinter.GetJobAsync(string jobId)`, added before 1.0 | Yes, for implementers |
| 5 | `Min…` and `Max…` names | `MinValue`/`MaxValue` on `VendorOption`; keep the rest | Only unshipped names |
| 6 | Evolving the API after 1.0 | Adopt the 0.1 review's policy, as written below | No |
| 7 | Additions that can wait | After 1.0, as additive changes | No |

### 1. British spellings in four public names

**Today** the public names are in American English, as .NET's are (`ColorMode`, `PrintOptions.ColorMode`), except:

- `MediaSize.WidthMillimetres` and `HeightMillimetres` (shipped);
- `PrintJobState.Cancelled` (shipped), beside .NET's `TaskStatus.Canceled`, `OperationCanceledException` and our own `CancelAsync`;
- `RasterRotation.Anticlockwise90` (not shipped yet), beside `Clockwise90`.

An application then writes `ColorMode.Color` and `PrintJobState.Cancelled` in one statement, and IntelliSense doesn't find `Canceled` or `Millimeters`.

**Recommendation:** rename them to `WidthMillimeters`, `HeightMillimeters`, `PrintJobState.Canceled` and `RasterRotation.Counterclockwise90`, keeping the enum values' numbers. CONTRIBUTING.md then says: identifiers in American English as .NET's are; prose, comments and messages in British English as now.

**Cost:** a rename across the repository, a suppression for each shipped member, and a line in the changelog. **Alternative:** keep them; after 1.0 they can't change.

### 2. Logging without dependency injection

**Today** the print systems log only when created through `AddLatiprimere…()`, which passes the container's `ILoggerFactory` to an internal `Create` overload. An application that calls `CupsPrintSystemFactory.Create()` and the others gets no logs, and can't, though `PrinterCatalog`'s constructor does take a logger. The logging guide has to say so.

**Recommendation:** make public the overloads the dependency injection code already uses:

```csharp
CupsPrintSystemFactory.Create(CupsOptions? options, ILoggerFactory? loggerFactory)
IppNetworkPrintSystemFactory.Create(IPdfRasterizer? pdfRasterizer, IppNetworkOptions? options, ILoggerFactory? loggerFactory)
WindowsPrintSystemFactory.Create(IPdfRasterizer? pdfRasterizer, WindowsOptions? options, ILoggerFactory? loggerFactory)
```

The IPP one replaces `Create(IPdfRasterizer?, IppNetworkOptions?)`, which hasn't shipped. The existing overloads stay. Without defaults on the new ones, the public API analyzer is satisfied (RS0026, RS0027).

**Cost:** small. Could come after 1.0, but the logging guide is written now.

### 3. A refused certificate's fingerprint, as data

**Today** a printer certificate that isn't trusted is refused with a `PrintException` whose message gives its SHA-256 fingerprint. The [secure printers](site/print-systems/secure-printers.md) guide tells applications to pin that fingerprint "from the PrintException", but the only way to get it is to parse the message, which is meant for people and may change. An application that offers "trust this printer?" can't do it properly.

**Recommendation:** a `PrintCertificateException : PrintException` in Abstractions with:

- `CertificateFingerprint` (`string`): the fingerprint, as `3A:7F:…`, ready for `TrustedCertificateFingerprints`;
- `PrinterUri` (`Uri?`): the address refused.

Thrown by direct IPP where it now throws the plain `PrintException` with a fingerprint. Because it derives from `PrintException`, existing `catch` blocks still catch it. CUPS's own certificate refusal could use it too, later.

**Cost:** one exception type, one throw site, tests, and the guide's example. Additive, so it could come after 1.0, but then the guide's advice stays awkward until it does.

### 4. Following a job after a restart

**Today** an `IPrintJob` is only available from `SubmitAsync`. A web service or worker that stores the job's `Id` and then restarts has no way back to the job's status, though every print system could look it up: IPP and CUPS by `job-id` with Get-Job-Attributes, Windows with `GetJob`. The 0.1 review listed this as an additive change, but it's a new member of `IPrinter`, which every implementation of `IPrinter` would have to add.

**Recommendation:** add it before 1.0, while that's free:

```csharp
Task<IPrintJob?> IPrinter.GetJobAsync(string jobId, CancellationToken cancellationToken = default);
```

It returns `null` for a job the print system no longer knows. CUPS and IPP printers forget finished jobs after a while, and Windows removes them unless the queue keeps printed documents, which the documentation would say.

**Cost:** medium: three adapters, the Windows route over IPP, tests on the virtual printers, an example and a guide section. **Alternative:** add it after 1.0 with a default implementation that throws `NotSupportedException`, which works but leaves it optional for every implementer.

### 5. `Min…` and `Max…` names

**Today:** `PrinterCapabilities.MaxCopies`, `WindowsOptions.MaxRenderDpi` and `IppNetworkOptions.MaxRenderDpi` abbreviate; `PrintOptions.MinimumCopies`, `VendorOption.MinimumValue` and `VendorOption.MaximumValue` don't.

**Recommendation:** rename `VendorOption.MinimumValue` and `MaximumValue` to `MinValue` and `MaxValue`, as `int.MaxValue` has it. They haven't shipped, so this costs nothing. Leave `PrintOptions.MinimumCopies`, which has: a suppression for one constant isn't worth it. **Alternative:** rename it to `MinCopies` too, for full consistency.

### 6. Evolving the API after 1.0

The 0.1 review proposed a policy for after 1.0. **Recommendation:** adopt it, written into CONTRIBUTING.md:

- new members on public interfaces get a **default implementation**, so existing implementations keep compiling, typically one that throws `NotSupportedException`;
- new capabilities and options go on the **records and options classes**, where adding a property breaks nobody;
- the adapter-facing interfaces (`IPrintSystem`, `IPdfRasterizer`, `IRasterDocument`) change only in a major version;
- the public API analyzer and package validation stay on, and a release with a breaking change doesn't ship without one of these decisions recorded.

### 7. Additions that can wait

These are additive, so they don't need to be in 1.0:

- **Why a job ended:** IPP `job-state-reasons` on `PrintJobStatus`, beyond `StateMessage`.
- **Document formats in `PrinterCapabilities`,** so an application can see before submitting that a printer can't take a PDF without a rasteriser.
- **Which print system a printer belongs to,** such as `PrinterInfo.PrintSystemName`, rather than reading the ID's prefix.
- **A response timeout for direct IPP** in `IppNetworkOptions`: queries already give up after a fixed 100 seconds.
- **Refreshing a printer's state:** needs no new member. `IPrinter.Info` is a snapshot, and looking the printer up again gives a new one; the documentation should say so (below).

## Documentation fixes

No API change:

- **`IPrinter.Info`:** it says it was read at the lookup; add that `GetPrinterAsync` again gives the current state.
- **`IppNetworkOptions.UserName` and `CupsOptions.UserName`:** say this is the name the job is listed under (`requesting-user-name`), not a login, and point to `Credentials` for that.

## How to apply

Once the decisions are made, in one change:

1. the renames (1 and 5), with the suppression file generated by packing with `ApiCompatGenerateSuppressionFile`, and `*REMOVED*` lines;
2. the factory overloads (2), with the logging guide and the examples;
3. `PrintCertificateException` (3), with the secure printers guide;
4. `GetJobAsync` (4), if chosen, with its own tests, example and guide section, as it's the largest;
5. the policy (6) in CONTRIBUTING.md, the documentation fixes, and the API reference pages regenerated.

ADR 0040 then records the decisions, and P3-9 goes on to the stable switch.
