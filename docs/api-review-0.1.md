# API review for 0.1.0-preview

> Status: **Decided and applied** (2026-11-13), as recommended except decision 7, where the owner chose `Id`. Recorded in [ADR 0018](decisions/0018-api-review-0-1.md). This review describes the API as it was before; the additive changes listed below are still to come.

## Why now

Once `0.1.0-preview` is on NuGet, people can build on it. During 0.x breaking changes are allowed, but each one costs every early user a migration, and after 1.0 they mostly stop being possible. Renames and moves are cheapest before the first publish. Members added later are cheap for record types and classes, but not for interfaces (see [After 1.0](#after-10-evolving-interfaces)).

## What is public today

| Package | Public types |
|---|---|
| `Latiprimere.Abstractions` | 25 types. **For applications:** `IPrinterCatalog`, `IPrinter`, `IPrintJob`, `ICancellablePrintJob`, `PrinterInfo`, `PrinterState`, `PrinterCapabilities`, `MediaSize`, `InputTray`, `PrintContent`, `DocumentFormat`, `PrintOptions`, `DuplexMode`, `PageOrientation`, `ColorMode`, `PrintJobStatus`, `PrintJobState`, `PrintException`, `PrinterNotFoundException`, `PrintSystemUnavailableException`. **For adapter authors:** `IPrintSystem`, `IPdfRasterizer`, `IRasterDocument`, `PageSize`, `RasterBand` |
| `Latiprimere` | `PrinterCatalog`, `PrintJobExtensions` (`WatchStatusAsync`), `ServiceCollectionExtensions` (`AddLatiprimere`) |
| `Latiprimere.Cups` | `CupsOptions`, `CupsPrintSystemFactory`, `ServiceCollectionExtensions` (`AddLatiprimereCups`) |
| `Latiprimere.Ipp` | `IppNetworkOptions`, `IppNetworkPrintSystemFactory`, `ServiceCollectionExtensions` (`AddLatiprimereIpp`) |
| `Latiprimere.Windows` | `WindowsPrintSystemFactory`, `ServiceCollectionExtensions` (`AddLatiprimereWindows`) |
| `Latiprimere.Rendering.Pdfium` | `PdfiumRasterizer`, `ServiceCollectionExtensions` (`AddLatiprimerePdfium`) |

Everything else is `internal`. That's a good start: the surface is small, and it has no SharpIppNext, Tmds.MDns or Win32 types in it.

## Overall

The shape is sound and needs no redesign:
- a catalog over pluggable print systems;
- printers that take content plus options;
- jobs with polled status;
- records for data, exceptions under one base class;
- `PrinterDefault` values rather than nullable enums;
- "empty means not reported" for capabilities.

The findings are about how the API feels to use, and about locking in things that are hard to change later. The table of decisions comes first, then the reasoning.

## Decisions for the owner

| # | Question | Recommendation | Breaking? |
|---|---|---|---|
| 1 | Namespace of the contracts | Move them from `Latiprimere.Abstractions` to `Latiprimere` | Yes, cheap now |
| 2 | Namespace of the `Add…()` methods | `Microsoft.Extensions.DependencyInjection`, with a class name per package | Yes, cheap now |
| 3 | Rendering contracts | Move to a `Latiprimere.Rendering` namespace, same assembly | Yes, cheap now |
| 4 | Cancelling jobs | Put `CancelAsync` on `IPrintJob`, returning whether it was in time, and drop `ICancellablePrintJob` | Yes |
| 5 | `CancellationToken` parameters | Give them `= default` on the public interfaces | No for callers |
| 6 | Disposing print systems | `IPrintSystem : IDisposable` | Yes, for adapter authors |
| 7 | `ID` or `Id` in public names | Keep `ID`, as CONTRIBUTING.md says | No |
| 8 | A printer that can't take the document's format | Keep `NotSupportedException` | No |
| 9 | Guarding the API from now on | Add the public API analyzer | No |

### 1. Namespace of the contracts

**Today** an application needs `using Latiprimere.Abstractions;` for every type it touches, and `using Latiprimere;` as well for `WatchStatusAsync`. "Abstractions" describes how the packages are split, which applications don't care about.

**Recommendation:** keep the `Latiprimere.Abstractions` package and assembly, but put its types in the `Latiprimere` namespace. This is the pattern of `Microsoft.Extensions.Logging.Abstractions`, whose types are in `Microsoft.Extensions.Logging`. One `using Latiprimere;` then covers the contracts, the catalog and `WatchStatusAsync`.

**Cost:** a file-by-file namespace change in Abstractions and every `using` in the repo. Also a written exception to the CONTRIBUTING.md rule that the namespace root matches the project name.

### 2. Namespace of the `Add…()` methods

**Today** each package has its own `ServiceCollectionExtensions` in its own `…DependencyInjection` namespace. The CLI needs five `using`s just to register Latiprimere:

```csharp
using Latiprimere.Abstractions;
using Latiprimere.Cups.DependencyInjection;
using Latiprimere.DependencyInjection;
using Latiprimere.Ipp.DependencyInjection;
using Latiprimere.Rendering.Pdfium.DependencyInjection;
using Latiprimere.Windows.DependencyInjection;
```

**Recommendation:** put the extension classes in `Microsoft.Extensions.DependencyInjection`, as the `Microsoft.Extensions.*` packages and many libraries such as Polly do, so `AddLatiprimere…()` shows up wherever `IServiceCollection` is used. Give each class its own name, for example `LatiprimereServiceCollectionExtensions` and `LatiprimereCupsServiceCollectionExtensions`, so the names don't collide in one namespace.

**After 1 and 2,** the same program needs:

```csharp
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;
```

**Cost:** small; also needs the same exception in CONTRIBUTING.md.

### 3. Rendering contracts

`IPdfRasterizer`, `IRasterDocument`, `PageSize` and `RasterBand` are the plumbing between the Windows adapter and PDFium. They should stay public:
- they let someone plug in another rasteriser;
- Phase 3 will use them for IPP printers that don't accept PDF.

But they sit next to `IPrinter` and `PrintOptions` in the same namespace, and `PageSize` reads like a paper size, which `MediaSize` already is.

**Recommendation:** move them to a `Latiprimere.Rendering` namespace, in the same assembly, documented as for adapter authors. Applications then only see them if they look.

### 4. Cancelling jobs

**Today** `SubmitAsync` returns `IPrintJob`, and cancelling needs a cast to `ICancellablePrintJob`. Every job Latiprimere creates can be cancelled (CUPS, direct IPP and Windows), so the cast always succeeds. Applications still have to write it, and wonder when it might fail.

Separately, cancelling a job that has just finished throws `InvalidOperationException`. That's a race the caller can't avoid: the job can finish between reading its status and cancelling it. The CLI catches `InvalidOperationException` for this reason.

**Recommendation:**
- `IPrintJob.CancelAsync(CancellationToken)` returns `Task<bool>`: `true` if the print system accepted the cancellation, `false` if the job had already finished.
- `ICancellablePrintJob` goes.
- A print system that genuinely can't cancel throws `NotSupportedException`, the .NET convention.

**Alternative:** keep the interface and only change the finished-job case. That's less churn, but the cast remains.

### 5. `CancellationToken` parameters

Every public async method takes a `CancellationToken` with no default, so callers without one write `CancellationToken.None` everywhere (the CLI's commands have 7 of them). The .NET libraries give the token `= default` on public methods.

**Recommendation:** add `= default` on the public interface methods. CONTRIBUTING.md's rule ("accept a `CancellationToken` as the last parameter") still holds.

**Note:** defaults on interface methods apply when called through the interface, which is how applications call them. Implementations should repeat the default.

### 6. Disposing print systems

**Today** the CUPS and direct IPP print systems hold an `HttpClient` and implement `IDisposable`, but `IPrintSystem` doesn't. The dependency injection container disposes them. An application using the factories has to cast:

```csharp
IPrintSystem cups = CupsPrintSystemFactory.Create();
...
(cups as IDisposable)?.Dispose();
```

**Recommendation:** `IPrintSystem : IDisposable`; the Windows print system's `Dispose` does nothing. Then `using IPrintSystem cups = CupsPrintSystemFactory.Create();` works. `PrinterCatalog` doesn't own the print systems it is given, which stays as it is and should be documented.

**Alternative:** factories return a public disposable type. That's more public types for little gain.

### 7. `ID` or `Id` in public names

**Today:** `PrinterInfo.ID`, `IPrintJob.ID`, `PrinterID`, `JobID` and `InputTray.ID`, per CONTRIBUTING.md (abbreviations of four letters or fewer in upper case). The .NET design guidelines treat "Id" as an abbreviation, not an acronym, and write `Id`, and most .NET libraries follow them. Applications mixing Latiprimere with other libraries will see both spellings.

**Recommendation:** keep `ID`. It's the project's own standard, applied everywhere, and it's cosmetic. But it is the last cheap moment to change it, so it's listed here.

### 8. A printer that can't take the document's format

`SubmitAsync` throws `NotSupportedException` when the printer doesn't accept the content's format (the HP ENVY and PDF), and when a print system can't honour `OutputFile`. Applications catch it as well as `PrintException`.

**Recommendation:** keep it. It's the .NET convention for "this instance can't do that", and applications can avoid it by checking first. Once capabilities report document formats (an additive change, below), they can.

**Alternative:** a `DocumentFormatNotSupportedException : PrintException`, so that one `catch` covers every printing failure.

### 9. Guarding the API from now on

Nothing stops an accidental public change today.

**Recommendation:**
- Add `Microsoft.CodeAnalysis.PublicApiAnalyzers`. Each package gets `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, and any public change shows up in review as a change to those files.
- After the first publish, turn on NuGet package validation (`EnablePackageValidation` with the published version as the baseline) to catch breaking changes between versions.

## Additive changes: not needed for 0.1

These add members or types without breaking anyone, so they can come when needed.

- **`PrintOptions`:** paper size, colour mode and tray (Phase 2), and the vendor settings dictionary ([ADR 0012](decisions/0012-vendor-option-escape-hatch.md)). One choice to settle then: select paper by its PWG name, not by `MediaSize` record equality (see Documentation fixes).
- **Why a job ended:** IPP `job-state-reasons` on `PrintJobStatus`. CUPS currently cancels jobs with no message.
- **Document formats in `PrinterCapabilities`**, so applications can see a printer refuses PDF before sending one.
- **Re-attaching to a job by ID,** for example `IPrinter.GetJobAsync(string jobID)`, so a service can follow a job after a restart.
- **Refreshing a printer's state.** `IPrinter.Info` is a snapshot from the lookup.
- **Which print system a printer belongs to,** for example `PrinterInfo.PrintSystemName`, rather than parsing the ID prefix.
- **More content types:** images and plain text (plan.md, Later).
- **Checking a job before sending it** (Validate-Job, Phase 3).

Members added to the **interfaces** (`IPrinter`, `IPrintJob`, `IPrinterCatalog`, `IPrintSystem`) break anyone who implements them, including test fakes in applications. Before 1.0 that's acceptable; see below.

## Documentation fixes

No API change:
- **Thread safety:** say that the catalog, printers and jobs can be used from several threads at once. They can: the only shared state is the direct IPP print system's remembered addresses (a `ConcurrentDictionary`) and the Windows job's cancel flag (`volatile`). But nothing says so.
- **`PrinterInfo.ID`:** say it is opaque but stable. Applications can store it and look the printer up later, but shouldn't parse it. Mention that `ipp:dnssd:` IDs follow the printer's announced name ([ADR 0010](decisions/0010-direct-ipp-adapter-design.md)).
- **`PrintContent`:** say a seekable stream is needed for a job to be resent to a busy printer ([ADR 0017](decisions/0017-retry-busy-ipp-printers.md)), and that the stream is read from its current position.
- **Record equality with lists:** `PrinterCapabilities` holds lists, and C# records compare lists by reference. Two capability records with the same contents are not `Equals`. Say so. `MediaSize` equality includes the display name, which differs between drivers, so say that sizes are compared by `Name`.
- **`PrinterCatalog`:** say it doesn't dispose the print systems it is given.
- **plan.md's draft API** uses names that don't exist (`PrintCapabilities`, `MediaSize.IsoA4`, `PrintOptions.MediaSize`); bring it in line with the real API.

## Missing unit tests in Abstractions

Most types are covered. Still to add:
- `PageSize.IsLandscape`;
- `RasterBand.Create` and `ByteCount` (exercised only indirectly, by the PDFium tests);
- `PrinterCapabilities` defaults (empty lists, `null` defaults);
- `PrinterInfo`'s default `State`;
- `PrintException.NativeErrorCode`.

These are best added after decisions 1 and 3, as those move the files.

## After 1.0: evolving interfaces

Proposed policy, for when 1.0 is near:
- New members on public interfaces get a **default implementation**, for example one that throws `NotSupportedException`, so existing implementations keep compiling.
- New capabilities and options go on the **records**, where adding a property breaks nobody.
- Adapter-facing interfaces (`IPrintSystem`, `IPdfRasterizer`) may change only in a major version.

## How to apply

With decisions 1 to 6 and 9 as recommended, the work is one mechanical change across the repo:
- namespaces and `using`s;
- `CancelAsync` moved, with the CLI and tests updated;
- token defaults and `IDisposable`;
- the analyzer added, with its files generated from the result;
- the documentation fixes and missing tests above;
- an ADR recording the decisions.

The tests should pass unchanged apart from the moves.
