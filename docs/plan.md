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
- [x] Spike 0: Solution scaffold, standards enforcement, core contracts, CI (reviewed with the go/no-go; CI has run on every change since, green on all three runners from 2026-11-17)
- [ ] Spike 1: CUPS over IPP (Linux and macOS) (done on Linux, and against the macOS runner's own CUPS in CI; the manual test on a real Mac is P3-8)
- [x] Spike 2: Windows spooler, raw jobs and job status (built and unit-tested; read-only spooler and AOT checks pass; raw jobs, status and cancel end to end in CI since Phase 1, green on 2026-11-17)
- [x] Spike 3: Windows PDF via PDFium + GDI (100 pages at 600 DPI in 66 s, 225 MiB peak; see ADR 0009)
- [ ] Spike 4: Direct IPP to a network printer (works against `ippeveprinter` and the HP ENVY over Wi-Fi, with mDNS discovery and Validate-Job, and mDNS discovery in CI on Ubuntu; see ADR 0010. Discovery on a real Mac is P3-8)
- [x] Spike 5: Capability model (one model from Windows, CUPS and direct IPP; see ADR 0011, and ADR 0012 for the escape hatch)
- [x] Go / no-go review: **Go** (owner, 2026-11-12; see [go-no-go.md](go-no-go.md))

### Phase 1: Harden core and raw printing (≈2–3 weeks)

Phase 0 leaves working slices; Phase 1 completes them.

- [x] `Latiprimere.Abstractions` v0.1 API review and full unit tests ([api-review-0.1.md](api-review-0.1.md); decisions in [ADR 0018](decisions/0018-api-review-0-1.md), applied 2026-11-13)
- [x] Windows adapter: complete discovery, raw jobs, status, cancel, error mapping. Error mapping done 2026-11-13 ([ADR 0019](decisions/0019-windows-error-mapping.md), accepted); raw jobs, status and cancel run end to end in CI, green on 2026-11-17 (owner)
- [x] Refuse raw data to drivers that drop it silently, starting with Microsoft Print to PDF, which accepts the job and writes an empty file (owner decision 2026-11-13; [ADR 0019](decisions/0019-windows-error-mapping.md))
- [x] CUPS adapter: complete discovery, raw and PDF jobs, status, cancel, authentication. Authentication done 2026-11-13: user name and password over TLS, and explained refusals ([ADR 0020](decisions/0020-cups-authentication.md), accepted); raw and PDF jobs, status and cancel run end to end against the test container, in CI too, green on 2026-11-17 (owner)
- [x] End-to-end test harness: every package together through the public API, run locally or by hand, not in CI ([test-harness-plan.md](test-harness-plan.md), [ADR 0029](decisions/0029-end-to-end-test-harness.md)). H0 to H3 done 2026-11-17: scenario library, xUnit host, console harness with reports, run scripts, a scripted IPP printer, printed-PDF inspection, and 47 scenarios, the docs examples and both samples among them. H4 done 2026-11-17: manual mode for real printers, with attribute capture ([docs/manual-tests](manual-tests/README.md)); every manual check passed on the HP ENVY 5640. H5 built the same day: packaged and Native AOT modes pass locally; the mDNS trial passed in every CI run from 2026-11-17 to 2026-11-19, and is now a normal CI job (ADR 0029, "Outcome of the mDNS trial")
- [x] Integration tests against virtual printers in CI (Windows, Ubuntu, macOS runners). Written 2026-11-13 for Ubuntu (the CUPS container: CUPS queues and direct IPP) and Windows (the spooler test printers and Microsoft Print to PDF); pass locally against the container. First Ubuntu CI run (2026-11-13): the raw-bytes checks failed because the capture backend's files were readable by `lp` only; fixed. macOS added 2026-11-13: the runner's own CUPS gets the Raw, Paused and Virtual-IPP queues and `ippeveprinter` (tools/macos-test-printers). Green on all three runners on 2026-11-17 (owner), after the fix for the CI build failure
- [x] Connect timeout for IPP requests: 10 s by default, set through `IppNetworkOptions.ConnectTimeout` and `CupsOptions.ConnectTimeout` (done 2026-11-13). Without it, the default `HttpClient` waited about 21 s on Windows for an address with nothing behind it
- [x] Connect over IPv4 and IPv6 together (Happy Eyeballs, RFC 8305; done 2026-11-13). .NET tried a name's addresses one at a time, so `localhost` on Windows spent about 2 s on IPv6 before reaching a server that listens on IPv4 only; now 250 ms
- [x] Resend jobs that a busy printer turns away with `server-error-busy`, for up to 2 minutes by default (owner decision 2026-11-13; [ADR 0017](decisions/0017-retry-busy-ipp-printers.md))
- [x] First preview package on NuGet (`0.1.0-preview`). Published 2026-11-15 by the Release workflow. Prepared 2026-11-13: version, package README, symbols and Source Link, and a tag-triggered release workflow with nuget.org trusted publishing ([ADR 0022](decisions/0022-release-process.md), accepted; steps in [releasing.md](releasing.md)). Licensing contact added to COMMERCIAL.md 2026-11-14. Still to do: the owner's one-time setup, then the release

### Phase 2: PDF and capabilities (≈3–4 weeks)

- [x] Capability model: media sizes, duplex, colour, copies, orientation, input trays. Reading them done in Spike 5 ([ADR 0011](decisions/0011-capability-model.md), accepted 2026-11-18); choosing them in `PrintOptions` done 2026-11-16 (below). Media types, margins and finishing stay out of the model; vendor options reach them
- [x] Choose paper size, colour mode and tray in `PrintOptions`, and apply them through IPP job attributes and the DEVMODE. Done 2026-11-16 ([ADR 0025](decisions/0025-choose-paper-colour-and-tray.md), accepted); checked with Microsoft Print to PDF and against the CUPS container's `ippeveprinter`
- [x] Windows PDF printing via `Latiprimere.Rendering.Pdfium`. Spike 3 built it ([ADR 0009](decisions/0009-windows-pdf-printing.md)); shipped since 0.1.0-preview, with copies, duplex, paper, colour (grey rendering for drivers that ignore monochrome), trays and a render resolution cap. Checked on real paper 2026-11-17: every manual check passed on the HP ENVY 5640 through its own Windows driver
- [x] **Make the Windows PDF render resolution configurable** (owner decision 2026-11-11). Default stays at full device resolution; expose `GdiRasterPrinter`'s existing `maxRenderDpi` cap, for example as a Windows adapter option. Measured trade-offs in [ADR 0009](decisions/0009-windows-pdf-printing.md). Done 2026-11-16 as `WindowsOptions.MaxRenderDpi`
- [x] Prefer direct IPP on Windows where the queue supports it. Done 2026-11-16 as `WindowsOptions.PrintPdfOverIpp` ([ADR 0027](decisions/0027-direct-ipp-for-windows-queues.md), accepted): PDF jobs to queues with a known IPP URI, off by default. Examined 2026-11-18 with [tools/windows-queue-report](../tools/windows-queue-report/README.md): a WSD queue's port records the printer's IPP `printer-uuid`, so it can be matched to the printer found over mDNS (ADR 0027, "Follow-up"; built with Phase 3's raster printing). A queue on Microsoft's IPP Class Driver is still unexamined
- [x] Vendor-specific option escape hatch: a dictionary of `ipp:`, `cups:` and `windows:` settings on `PrintOptions` (ADR 0012, accepted). Done 2026-11-16 as `PrintOptions.VendorOptions` ([ADR 0026](decisions/0026-vendor-options-in-detail.md), accepted), with `windows:devmode`
- [x] `windows:print-ticket`: merge a Print Ticket through `prntvpt.dll` (ADR 0012). Done 2026-11-18 ([ADR 0031](decisions/0031-windows-print-tickets.md)): merged onto the driver's defaults or `windows:devmode` before the typed options, with COM streams called through function pointers so Native AOT still works; checked with Microsoft Print to PDF, natively too
- [x] Vendor options in the capabilities, and Windows Print Capabilities. Done 2026-11-18 ([ADR 0032](decisions/0032-vendor-options-in-capabilities.md)): `PrinterCapabilities.VendorOptions` lists IPP job attributes and Windows driver features with their values and defaults, ready to set; `windows:feature:` and `windows:parameter:` keys are checked against the driver's Print Capabilities; `IWindowsPrinter` reads the Print Capabilities and the default Print Ticket; the CLI's `print-capabilities` command writes them
- [x] Trimming and Native AOT verification. Done 2026-11-16: CI publishes the CLI sample with Native AOT for both targets on all three runners, fails on any IL2xxx/IL3xxx warning, and runs the native program against the runner's test printers (`tools/aot-check/check-aot.sh`). Since 2026-11-17 it also publishes the end-to-end harness natively and runs four of its scenarios instead of the CLI's commands (ADR 0029). Green in CI on 2026-11-17 (owner); since 2026-11-18 it also runs `windows.output.print-ticket` and the two vendor option scenarios, `windows.vendor-options.listed-and-sent` and `ipp.vendor-options.listed-and-sent`, and since P3-3 `ipp.raster.apple-raster-two-sided`, which renders, encodes, gzips and streams a PDF natively

### Phase 3: Direct IPP and v1.0 (≈2–3 weeks)

Broken into work packages P3-1 to P3-9, with what exists and the decisions taken, in [phase-3-plan.md](phase-3-plan.md) (accepted 2026-11-18).

- [x] Direct IPP adapter with mDNS discovery (started in Spike 4; hardened through Phases 1 to 3 with Happy Eyeballs, busy retries, remembered addresses, raster printing and IPPS; mDNS discovery checked in CI. Discovery on a real Mac is P3-8)
- [x] Validate-Job before submitting to network printers. Done 2026-11-18 (P3-1, [ADR 0033](decisions/0033-validate-job.md)): on by default for printers that list it, `IppNetworkOptions.ValidateJobs` to turn it off; Windows queues routed to IPP check too. Still to do: a check on the real HP, which was off
- [ ] Print PDFs to printers that don't accept PDF, by rendering with PDFium to Apple raster (`image/urf`), PCLm or PWG raster. The HP ENVY 5640 is one: over IPP it takes PCL, JPEG, PCLm, Apple raster and octet-stream, but not PDF. Encoders done 2026-11-18 (P3-2, [ADR 0034](decisions/0034-raster-encoders.md)): PWG raster and Apple raster, grey, colour and 1-bit black, with back-side transforms. Printing with them over direct IPP built 2026-11-18 (P3-3, [ADR 0035](decisions/0035-printing-pdfs-as-raster.md)); Windows WSD queues route to such printers too, matched by UUID over mDNS (P3-4, [ADR 0036](decisions/0036-wsd-queues-over-ipp.md)); the checks on the HP's paper remain
- [ ] IPPS (TLS) and authentication. Built 2026-11-18 (P3-5, [ADR 0037](decisions/0037-ipps-and-authentication.md)): pinned certificates by SHA-256 fingerprint or a callback, credentials sent only over IPPS, and IPPS preferred for discovered printers once trust is configured. Checked over real TLS on the scripted printer; the HP over `ipps://` waits for the manual check. Windows queues sent over IPP take the same settings since 2026-11-20 ([ADR 0042](decisions/0042-ipps-for-windows-queues-and-uri-authentication.md))
- [x] Optionally persist the remembered addresses of name IDs, so a printer that stops answering mDNS while asleep is still found after a restart (the HP ENVY did this on 2026-11-12). Done 2026-11-18 (P3-6, [ADR 0038](decisions/0038-remembered-addresses.md)): `IppNetworkOptions.AddressStorePath`
- [x] Documentation site and samples (console, ASP.NET Core, worker service). Done 2026-11-19 (P3-7), with the API reference ([ADR 0039](decisions/0039-api-reference.md)), and reviewed again against the 1.0 API (P3-9)
- [ ] macOS verification on real hardware
- [ ] `1.0.0` release, after Phase 4 (Phase 4's decision D1): the first stable release, `11.0.<n>` (Phase 3's decision D8). The sign-off and the switch are listed in [releasing.md](releasing.md#the-first-stable-release-10); the switch is ready to run (2026-11-20)

### Phase 4: PCLm, images and text, and events (before 1.0)

Three of the later items come forward, so 1.0 starts with them (owner, 2026-11-20). Work packages P4-1 to P4-7, what exists, and the decisions are in [phase-4-plan.md](phase-4-plan.md), accepted 2026-11-20 with every decision as recommended.

- [x] A small PDF writer, internal (P4-1). Done 2026-11-20 ([ADR 0045](decisions/0045-pdf-writer.md)): objects and streams written as they come, Flate, run-length and pass-through compression, images and Courier text, checked through PDFium
- [x] PCLm, and a choice of raster format (P4-2). Done 2026-11-20 ([ADR 0046](decisions/0046-pclm-and-raster-format-order.md)): PCLm strips written as they fill, Flate or run-length as the printer prefers, and `IppNetworkOptions.RasterFormats` and `WindowsOptions.IppRasterFormats` to order or leave out raster formats. The HP on paper is in the sign-off
- [x] Images: JPEG and PNG (P4-3). Done 2026-11-20 ([ADR 0047](decisions/0047-printing-images.md)): `PrintContent.FromJpeg` and `FromPng`, `PrintOptions.Scaling` and `PrinterCapabilities.DocumentFormats`; JPEGs passed through, a PNG decoder written here, and images placed on a page as a PDF where they can't go as they are. A photo on the HP's paper is in the sign-off
- [x] Plain text (P4-4). Done 2026-11-20 ([ADR 0048](decisions/0048-printing-plain-text.md)): `PrintContent.FromText`; CUPS lays text out, and elsewhere Latiprimere does, as a PDF in Courier, with Windows-1252 characters only. Text on the HP's paper is in the sign-off
- [x] Printer and job events (P4-5). Done 2026-11-20 ([ADR 0049](decisions/0049-following-printers-and-jobs.md)): `PrinterStatus`, `IPrinter.GetStatusAsync` and `WatchStatusAsync`, and `IPrintJob.WatchStatusAsync` as a member. Windows follows the spooler's change notifications; CUPS and network printers are polled, as CUPS doesn't wait on IPP notifications
- [ ] Docs, samples and an API review of the additions (P4-6)
- [ ] The sign-off extended to them (P4-7)

### Later (unscheduled)

Each can be added after 1.0 without breaking the API; [ADR 0044](decisions/0044-room-to-grow-after-1-0.md) sets out how.

- UI add-ons: print dialogs and preview for Avalonia, MAUI, WPF/WinForms (new packages, over the public API)
- Image and plain-text content helpers: proposed for Phase 4. Beyond it: text in any script, which needs an embedded font
- Printer event subscriptions: proposed for Phase 4, for jobs and printers' states. Beyond it: printers appearing and going (a default-implemented catalog member)
- Android/iOS (new adapter packages and target frameworks)

## Risks

Brought up to date 2026-11-19 (P3-9).

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Windows PDF path is slower or lower quality than expected | Low | High | Spike 3 passed (ADR 0009); printed on the HP ENVY 5640 through its own driver, every manual check passing (2026-11-17); `WindowsOptions.MaxRenderDpi` caps the resolution |
| macOS behaviour differs from Linux CUPS (sandboxing, socket paths) | Low | Low | The macOS runner's own CUPS runs the integration tests in CI, green since 2026-11-17; the checks on a real Mac are P3-8 |
| Another library or service is confused with Latiprimere | Low | Medium | Renamed from CrossPrint before the first release ([ADR 0016](decisions/0016-rename-to-latiprimere.md)) |
| Someone else publishes `Latiprimere.*` packages | Low | Medium | The six package IDs are ours since 0.1.0-preview. The `Latiprimere.` prefix isn't reserved yet (the packages aren't shown as verified on nuget.org, 2026-11-19): request it as [releasing.md](releasing.md) says |
| PrintingTools matures and covers the same ground | Medium | Medium | Differentiate on headless use, direct IPP, raster printing and Native AOT; consider contributing instead |
| Printer-specific bugs swamp the issue tracker | High | Medium | Officially support IPP-conformant behaviour only; the harness's manual mode captures a printer's IPP attributes for a report ([docs/manual-tests](manual-tests/README.md)) |
| Only one real printer has been tried | High | Medium | `ippeveprinter` and the scripted printer stand in for IPP Everywhere printers in CI; the HP ENVY 5640 covers PDF over its Windows driver, raster and IPPS. Reports from users with other printers, through manual mode, will widen it |
| SharpIppNext gaps or abandonment | Low | Medium | Kept behind `IIppClient`; attributes it doesn't model are read from its raw attribute list |
| Tmds.MDns is pre-1.0 | Low | Low | Small and dependency-free, behind `IIppPrinterDiscovery`; mDNS discovery runs in CI since 2026-11-17 ([ADR 0029](decisions/0029-end-to-end-test-harness.md)) |
| PDFium native packaging across RIDs | Low | Medium | Isolated in `Latiprimere.Rendering.Pdfium`; shipped since 0.1.0-preview, and published with Native AOT on all three runners in CI |
| The Windows spooler doesn't record how a job ended | Certain | Medium | Reported as `Completed` with a note, or `Canceled`; PDF jobs to queues routed over IPP report the printer's own outcome ([ADR 0027](decisions/0027-direct-ipp-for-windows-queues.md), [ADR 0036](decisions/0036-wsd-queues-over-ipp.md)) |
| A design mistake in the API is found after 1.0 | Medium | Medium | The 1.0 review ([ADR 0040](decisions/0040-api-review-1-0.md)); after it, additions only, by the rules in CONTRIBUTING.md, with breaking changes waiting for the next .NET major ([ADR 0030](decisions/0030-release-numbering.md)) |

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