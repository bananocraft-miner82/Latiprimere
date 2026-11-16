# Project Plan

> Status: **Draft for review** (2026-11-10). Phase 0 (proof of concept) is detailed in [poc-plan.md](poc-plan.md).

## Vision

Latiprimere lets any .NET application, with or without a UI, find printers, learn what they can do, send them work and follow that work to completion, with the same code on Windows, Linux and macOS.

## Goals

- One API for printer discovery, capabilities, job submission, job status and cancellation on Windows, Linux and macOS.
- Headless first: usable from console apps, services, containers and web backends.
- No UI framework dependency in the core packages.
- Native platform integration (spooler APIs and IPP), not shelling out to command-line tools.
- Trimming and Native AOT compatible.
- Future-proof by being IPP-first, in line with where Windows and CUPS are heading.

## Non-goals (for v1)

- Print dialogs and print preview. These belong in optional per-framework add-on packages later.
- Layout, reporting or document-generation engines. Callers bring PDF, images or raw printer language.
- Android and iOS.
- Printer installation, driver management or print-server administration.

## Target users and scenarios

| Scenario | Example | Needs |
|---|---|---|
| Server/back-office printing | Web app prints invoices to an office printer | PDF jobs, status, no UI |
| Label and receipt printing | Warehouse app prints ZPL labels; POS prints ESC/POS receipts | Raw jobs, low latency, Linux support |
| Kiosk and edge devices | Raspberry Pi kiosk prints tickets | Linux ARM64, small footprint, AOT |
| Cross-platform desktop apps | Avalonia/MAUI app needs a "Print" button | PDF jobs, capabilities (dialogs come later) |

## Proposed architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Consumers: apps, services, optional UI add-ons (later)      │
└──────────────────────────────┬───────────────────────────────┘
                               │
┌──────────────────────────────▼───────────────────────────────┐
│  Latiprimere (services)                                       │
│  IPrinterCatalog · IPrintJobService · platform selection     │
│  DI registration (Microsoft.Extensions.DependencyInjection)  │
└───────┬──────────────────────┬──────────────────────┬────────┘
        │                      │                      │
┌───────▼────────┐  ┌──────────▼──────────┐  ┌────────▼────────┐
│ .Windows       │  │ .Cups               │  │ .Ipp            │
│ winspool, GDI, │  │ (Linux, macOS)      │  │ (direct to      │
│ PrintTicket    │  │ IPP to local CUPS   │  │ network printer)│
│                │  │ scheduler           │  │ + mDNS discovery│
└───────┬────────┘  └──────────┬──────────┘  └────────┬────────┘
        │                      └──────────┬───────────┘
        │                      ┌──────────▼──────────┐
        │                      │ IPP transport       │
        │                      │ (SharpIppNext)      │
        │                      └─────────────────────┘
┌───────▼──────────────────────┐
│ .Rendering.Pdfium            │  optional, native dependency;
│ PDF → raster for GDI path    │  only needed where PDF can't be sent as-is
└──────────────────────────────┘

Latiprimere.Abstractions: interfaces, models, enumerations only.
Referenced by every package above. No implementations, no UI types.
Names starting with "." are Latiprimere.* packages.
```

### Package plan

Namespaces, assemblies, project names and NuGet package IDs all use the `Latiprimere` root ([ADR 0004](decisions/0004-naming-and-poc-approach.md)). Every project multi-targets `net10.0` and `net11.0` ([ADR 0013](decisions/0013-target-net10-and-net11.md)).

| Project | Contents | Dependencies |
|---|---|---|
| `Latiprimere.Abstractions` | Interfaces (`IPrinterCatalog`, `IPrinter`, `IPrintJob`, …), records (`PrinterInfo`, `PrintCapabilities`, `PrintOptions`, `PrintJobStatus`), enumerations, exceptions | None |
| `Latiprimere` | Service implementations, platform selection, DI extension methods | Abstractions, M.E.DependencyInjection.Abstractions, M.E.Logging.Abstractions |
| `Latiprimere.Windows` | Spooler adapter (`LibraryImport` P/Invoke) | Abstractions |
| `Latiprimere.Cups` | CUPS adapter over IPP | Abstractions, Ipp transport |
| `Latiprimere.Ipp` | Shared IPP transport, direct IPP adapter and mDNS discovery | Abstractions, SharpIppNext, mDNS library (TBD) |
| `Latiprimere.Rendering.Pdfium` | PDF rasteriser used by the Windows GDI path | Abstractions, PDFium native binaries |

The layering follows the architecture section of [CONTRIBUTING.md](../CONTRIBUTING.md): contracts at the bottom, a strict one-way dependency rule, and no UI types in any core package.

### Public API shape (0.1)

As decided in [ADR 0018](decisions/0018-api-review-0-1.md). Choosing paper, colour and tray in `PrintOptions` comes in Phase 2.

```csharp
using Latiprimere;
using Microsoft.Extensions.DependencyInjection;

ServiceCollection services = new ServiceCollection();
services.AddLatiprimere();
services.AddLatiprimereCups();
services.AddLatiprimereWindows();
services.AddLatiprimerePdfium();
services.AddLatiprimereIpp();

using ServiceProvider serviceProvider = services.BuildServiceProvider();
IPrinterCatalog catalog = serviceProvider.GetRequiredService<IPrinterCatalog>();

IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync();
IPrinter printer = await catalog.GetPrinterAsync(printers[0].Id);

PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync();

PrintOptions options = new PrintOptions
{
	Copies = 2,
	Duplex = DuplexMode.LongEdge
};

IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(pdfStream), options);

await foreach (PrintJobStatus status in job.WatchStatusAsync(cancellationToken))
{
	// React to Pending / Processing / Completed / Cancelled / Aborted
}

bool isCancelled = await job.CancelAsync(); // false if the job had already finished
```

## Phases and milestones

Tick items off here as they complete; record detail in [progress.md](progress.md).

### Phase 0: Proof of concept (1–2 weeks)

Prove each technical route works by building thin vertical slices of the real packages. See [poc-plan.md](poc-plan.md).

- [x] Adapt CONTRIBUTING.md for Latiprimere
- [x] Review and agree this plan and resolve the open questions
- [ ] Spike 0: Solution scaffold, standards enforcement, core contracts, CI (done locally; awaiting review and first CI run)
- [ ] Spike 1: CUPS over IPP (Linux and macOS) (done on Linux; macOS manual test pending)
- [ ] Spike 2: Windows spooler, raw jobs and job status (built and unit-tested; read-only spooler and AOT checks pass; end-to-end raw/cancel test deferred)
- [x] Spike 3: Windows PDF via PDFium + GDI (100 pages at 600 DPI in 66 s, 225 MiB peak; see ADR 0009)
- [ ] Spike 4: Direct IPP to a network printer (works against `ippeveprinter` and the HP ENVY over Wi-Fi, with mDNS discovery and Validate-Job; see ADR 0010. Mac discovery pending)
- [x] Spike 5: Capability model (one model from Windows, CUPS and direct IPP; see ADR 0011, and ADR 0012 for the escape hatch)
- [x] Go / no-go review: **Go** (owner, 2026-11-12; see [go-no-go.md](go-no-go.md))

### Phase 1: Harden core and raw printing (≈2–3 weeks)

Phase 0 leaves working slices; Phase 1 completes them.

- [x] `Latiprimere.Abstractions` v0.1 API review and full unit tests ([api-review-0.1.md](api-review-0.1.md); decisions in [ADR 0018](decisions/0018-api-review-0-1.md), applied 2026-11-13)
- [ ] Windows adapter: complete discovery, raw jobs, status, cancel, error mapping. Error mapping done 2026-11-13 ([ADR 0019](decisions/0019-windows-error-mapping.md), proposed); raw jobs, status and cancel now run end to end in CI (not yet run there)
- [x] Refuse raw data to drivers that drop it silently, starting with Microsoft Print to PDF, which accepts the job and writes an empty file (owner decision 2026-11-13; [ADR 0019](decisions/0019-windows-error-mapping.md))
- [ ] CUPS adapter: complete discovery, raw and PDF jobs, status, cancel, authentication. Authentication done 2026-11-13: user name and password over TLS, and explained refusals ([ADR 0020](decisions/0020-cups-authentication.md), proposed); raw and PDF jobs, status and cancel run end to end against the test container
- [ ] Integration tests against virtual printers in CI (Windows, Ubuntu, macOS runners). Written 2026-11-13 for Ubuntu (the CUPS container: CUPS queues and direct IPP) and Windows (the spooler test printers and Microsoft Print to PDF); pass locally against the container. First Ubuntu CI run (2026-11-13): the raw-bytes checks failed because the capture backend's files were readable by `lp` only; fixed. macOS added 2026-11-13: the runner's own CUPS gets the Raw, Paused and Virtual-IPP queues and `ippeveprinter` (tools/macos-test-printers). Still to do: a green CI run on all three runners
- [x] Connect timeout for IPP requests: 10 s by default, set through `IppNetworkOptions.ConnectTimeout` and `CupsOptions.ConnectTimeout` (done 2026-11-13). Without it, the default `HttpClient` waited about 21 s on Windows for an address with nothing behind it
- [x] Connect over IPv4 and IPv6 together (Happy Eyeballs, RFC 8305; done 2026-11-13). .NET tried a name's addresses one at a time, so `localhost` on Windows spent about 2 s on IPv6 before reaching a server that listens on IPv4 only; now 250 ms
- [x] Resend jobs that a busy printer turns away with `server-error-busy`, for up to 2 minutes by default (owner decision 2026-11-13; [ADR 0017](decisions/0017-retry-busy-ipp-printers.md))
- [x] First preview package on NuGet (`0.1.0-preview`). Published 2026-11-15 by the Release workflow. Prepared 2026-11-13: version, package README, symbols and Source Link, and a tag-triggered release workflow with nuget.org trusted publishing ([ADR 0022](decisions/0022-release-process.md), proposed; steps in [releasing.md](releasing.md)). Licensing contact added to COMMERCIAL.md 2026-11-14. Still to do: the owner's one-time setup, then the release

### Phase 2: PDF and capabilities (≈3–4 weeks)

- [ ] Capability model: media sizes, duplex, colour, copies, orientation, input trays (reading them done in Spike 5)
- [x] Choose paper size, colour mode and tray in `PrintOptions`, and apply them through IPP job attributes and the DEVMODE. Done 2026-11-16 ([ADR 0025](decisions/0025-choose-paper-colour-and-tray.md), proposed); checked with Microsoft Print to PDF and against the CUPS container's `ippeveprinter`
- [ ] Windows PDF printing via `Latiprimere.Rendering.Pdfium`
- [x] **Make the Windows PDF render resolution configurable** (owner decision 2026-11-11). Default stays at full device resolution; expose `GdiRasterPrinter`'s existing `maxRenderDpi` cap, for example as a Windows adapter option. Measured trade-offs in [ADR 0009](decisions/0009-windows-pdf-printing.md). Done 2026-11-16 as `WindowsOptions.MaxRenderDpi`
- [x] Prefer direct IPP on Windows where the queue supports it. Done 2026-11-16 as `WindowsOptions.PrintPdfOverIpp` ([ADR 0027](decisions/0027-direct-ipp-for-windows-queues.md), accepted): PDF jobs to queues with a known IPP URI, off by default. Still open: how Windows 11 names driverless IPP queues' ports, which needs output from a PC with one
- [x] Vendor-specific option escape hatch: a dictionary of `ipp:`, `cups:` and `windows:` settings on `PrintOptions` (ADR 0012, accepted). Done 2026-11-16 as `PrintOptions.VendorOptions` ([ADR 0026](decisions/0026-vendor-options-in-detail.md), proposed), with `windows:devmode`
- [ ] `windows:print-ticket`: merge a Print Ticket through `prntvpt.dll` (ADR 0012); needs new interop
- [x] Trimming and Native AOT verification. Done 2026-11-16: CI publishes the CLI sample with Native AOT for both targets on all three runners, fails on any IL2xxx/IL3xxx warning, and runs the native program against the runner's test printers (`tools/aot-check/check-aot.sh`). Not yet run in CI

### Phase 3: Direct IPP and v1.0 (≈2–3 weeks)

- [ ] Direct IPP adapter with mDNS discovery (started in Spike 4)
- [ ] Validate-Job before submitting to network printers
- [ ] Print PDFs to printers that don't accept PDF, by rendering with PDFium to Apple raster (`image/urf`), PCLm or PWG raster. The HP ENVY 5640 is one: over IPP it takes PCL, JPEG, PCLm, Apple raster and octet-stream, but not PDF
- [ ] IPPS (TLS) and authentication
- [ ] Optionally persist the remembered addresses of name IDs, so a printer that stops answering mDNS while asleep is still found after a restart (the HP ENVY did this on 2026-11-12)
- [ ] Documentation site and samples (console, ASP.NET Core, worker service)
- [ ] macOS verification on real hardware
- [ ] `1.0.0` release

### Later (unscheduled)

- UI add-ons: print dialogs and preview for Avalonia, MAUI, WPF/WinForms
- Image and plain-text content helpers
- Printer event subscriptions (IPP notifications, Windows change notifications)
- Android/iOS

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Windows PDF path is slower or lower quality than expected | Low | High | Spike 3 passed (ADR 0009); measure real printer drivers in Phase 2; a render-resolution cap is available |
| macOS behaviour differs from Linux CUPS (sandboxing, socket paths) | Medium | Low | Mac available for manual testing; macOS CI runner |
| Name confusion with the commercial "Cross Print" cloud-printing product (crossprint.io) | Low | Medium | Renamed to Latiprimere before the first release ([ADR 0016](decisions/0016-rename-to-latiprimere.md)); search the trademark registers and reserve the `Latiprimere.` NuGet ID prefix before the first publish |
| A `Latiprimere` package ID is claimed by someone else before we publish | Low | Medium | Publish a placeholder preview early in Phase 1 and apply for the prefix reservation |
| PrintingTools matures and covers the same ground | Medium | Medium | Differentiate on headless/IPP/AOT; consider contributing instead |
| Printer-specific bugs swamp the issue tracker | High | Medium | Officially support IPP-conformant behaviour only; issue template asks for IPP attribute dumps |
| SharpIppNext gaps or abandonment | Low | Medium | Keep it behind an internal interface so it can be replaced |
| PDFium native packaging across RIDs | Medium | Medium | Isolate in an optional package; reuse maintained PDFium binaries |

## Open questions for review

None at present.

### Resolved

| Question | Decision | Date |
|---|---|---|
| CONTRIBUTING.md in a public repo | Adapted into a Latiprimere-specific version; ERP and company sections removed | 2026-11-10 |
| Layer mapping | Abstractions → adapters → services → consumers, defined in CONTRIBUTING.md | 2026-11-10 |
| Namespaces and package IDs | `CrossPrint.*` for both (renamed to `Latiprimere.*` on 2026-11-12). `Prodigy.CrossPrint.*` was dropped because the `Prodigy.` NuGet prefix belongs to another owner | 2026-11-10 |
| macOS hardware | A Mac is available for testing | 2026-11-10 |
| Proof-of-concept code | The start of the real packages, built in `src/` and `tests/` | 2026-11-10 |
| Localised strings | Exception messages come from `.resx` resources (English only) | 2026-11-10 |
| Target frameworks | Multi-target `net8.0` and `net10.0` (superseded 2026-11-12) | 2026-11-10 |
| One failing print system breaks the catalog | Skip it with a logged warning when listing or finding the default; fail only if every print system fails ([ADR 0008](decisions/0008-print-system-failure-isolation.md)) | 2026-11-11 |
| `PrintOptions.OutputFile` | Keep it in the cross-platform options; print systems that can't honour it throw `NotSupportedException` ([ADR 0009](decisions/0009-windows-pdf-printing.md)) | 2026-11-11 |
| Default render resolution for Windows PDF | Full device resolution; make the cap configurable later (Phase 2 checklist) | 2026-11-11 |
| Lines over 120 characters | One-off rewrap, plus a 120-column guide in `.editorconfig` | 2026-11-11 |
| Target frameworks, after .NET 8's end of support | `net10.0;net11.0`, built with the .NET 11 SDK ([ADR 0013](decisions/0013-target-net10-and-net11.md)) | 2026-11-12 |
| Vendor-specific settings | A dictionary of print-system-specific settings on `PrintOptions` (`ipp:`, `cups:`, `windows:`), with typed options added for common settings over time ([ADR 0012](decisions/0012-vendor-option-escape-hatch.md)) | 2026-11-11 |
| Licence | A split licence: a free Community Licence for small organisations, non-profits, open source, evaluation and indirect use, and a paid Commercial Licence for everyone else. Contributors sign a CLA ([ADR 0015](decisions/0015-split-licence.md)) | 2026-11-12 |
| Rename before the first release | Rename CrossPrint to **Latiprimere** ("to print broadly"), with every domain checked, the GitHub name and the NuGet ID free ([ADR 0016](decisions/0016-rename-to-latiprimere.md)) | 2026-11-12 |
| Stable IDs for discovered network printers | Identify them by DNS-SD name (`ipp:dnssd:Office Laser`). A lookup tries the known address first, then searches by name, so a new address doesn't lose the printer. Configured printers keep URI IDs ([ADR 0010](decisions/0010-direct-ipp-adapter-design.md)) | 2026-11-11 |
| Retrying a printer that answers `server-error-busy` | Direct IPP resends the job, with growing waits, for up to `IppNetworkOptions.BusyRetryDuration` (2 minutes by default; zero turns it off). Seekable documents only ([ADR 0017](decisions/0017-retry-busy-ipp-printers.md)) | 2026-11-13 |
| Public API before 0.1 | Contracts in the `Latiprimere` namespace; `Add…()` methods in `Microsoft.Extensions.DependencyInjection`; rendering contracts in `Latiprimere.Rendering`; `CancelAsync` on `IPrintJob` returning whether it was in time; token defaults; `IPrintSystem : IDisposable`; `Id` rather than `ID`; the public API analyzer ([ADR 0018](decisions/0018-api-review-0-1.md)) | 2026-11-13 |