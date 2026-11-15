# 0018: Public API for 0.1.0-preview

- Status: Accepted
- Date: 2026-11-13

## Context

Before the first preview package, the public API was reviewed in [api-review-0.1.md](../api-review-0.1.md). Its shape was sound. Nine questions were about how it feels to use and what is hard to change once published, and were put to the owner.

## Decision

Owner decision, 2026-11-13: all nine as recommended, except the seventh, where the owner chose `Id` over keeping `ID`.

1. **The contracts are in the `Latiprimere` namespace.** The `Latiprimere.Abstractions` package and assembly keep their name; its `RootNamespace` is `Latiprimere`, as `Microsoft.Extensions.Logging.Abstractions` puts its types in `Microsoft.Extensions.Logging`. `using Latiprimere;` covers the contracts, `PrinterCatalog` and `WatchStatusAsync`.
2. **The `Add…()` methods are in `Microsoft.Extensions.DependencyInjection`.** Each package's class has its own name:
   - `LatiprimereServiceCollectionExtensions`;
   - `LatiprimereCupsServiceCollectionExtensions`;
   - `LatiprimereIppServiceCollectionExtensions`;
   - `LatiprimereWindowsServiceCollectionExtensions`;
   - `LatiprimerePdfiumServiceCollectionExtensions`.
3. **Rendering contracts are in `Latiprimere.Rendering`:** `IPdfRasterizer`, `IRasterDocument`, `PageSize` and `RasterBand`. They are in a `Rendering/` folder of the Abstractions project and documented as for adapter authors.
4. **`IPrintJob.CancelAsync` returns `Task<bool>`,** and `ICancellablePrintJob` is gone.
   - `true` means the print system accepted the cancellation.
   - `false` means the job had already finished. This replaces the `InvalidOperationException`, which callers couldn't avoid, as a job can finish between reading its status and cancelling it.
   - A print system that can't cancel throws `NotSupportedException`.
5. **Cancellation tokens default to `default`** on the public interfaces (`IPrinterCatalog`, `IPrinter`, `IPrintJob`, `IPrintSystem`) and their implementations. `WatchStatusAsync` uses overloads instead (`()`, `(token)`, `(interval, token)`), because the public API analyzer allows optional parameters only on the longest overload (RS0026, RS0027).
6. **`IPrintSystem : IDisposable`.**
   - The CUPS and IPP print systems release their HTTP client; the Windows print system has nothing to release.
   - `PrinterCatalog` never disposes the print systems it is given.
   - Applications using the factories write `using IPrintSystem cups = CupsPrintSystemFactory.Create();`.
7. **`Id`, not `ID`,** in every identifier, public or not: `PrinterInfo.Id`, `IPrintJob.Id`, `PrinterId`, `JobId`, `InputTray.Id`, `printerId`, `DeletedJobIds`. This follows the .NET design guidelines, which treat "Id" as an abbreviation, not an acronym. CONTRIBUTING.md now says so. In prose and messages it stays "ID".
8. **`NotSupportedException` stays** for a document format a printer refuses and for `OutputFile` where a print system can't honour it.
9. **The public API analyzer** (`Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0) runs on every package in `src/`.
   - Each package has `PublicAPI.Shipped.txt` (empty until the first publish) and `PublicAPI.Unshipped.txt`.
   - A public change that isn't recorded there fails the build, so every API change shows up in review.
   - When a version is published, its lines move to the shipped file.
   - NuGet package validation, against the published version as a baseline, is to be turned on after the first publish.

## Consequences

- An application needs `using Latiprimere;` and `using Microsoft.Extensions.DependencyInjection;`, down from seven `using`s.
- Every caller of the old names had to change; there are no callers outside this repository yet.
- CONTRIBUTING.md records two exceptions to "the namespace root matches the project": the Abstractions project, and the dependency injection classes.
- **Inside `Latiprimere.*` namespaces, Latiprimere's types now win over same-named types brought in by `using`.** Examples are `PrinterState`, `DocumentFormat` and `MediaSize` from SharpIppNext. Code that means SharpIppNext's type uses an alias such as `IppPrinterState`, as the IPP mappers already did.
- **Adding members to the public interfaces is still breaking.** Additive changes go on records where possible. After 1.0, new interface members get default implementations (see the review's "After 1.0" section).
- The additive changes and documentation fixes listed in the review are not affected. The documentation fixes were made with this change.
