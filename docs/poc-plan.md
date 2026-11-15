# Phase 0: Proof-of-Concept Plan

> Status: **Draft for review** (updated 2026-11-10). Part of [plan.md](plan.md).

## Purpose

Answer one question before committing to the full roadmap: **can each technical route deliver discovery, job submission and job status well enough to support the planned architecture?**

The proof of concept is **the start of the real packages**, not throwaway code. Each spike builds a thin vertical slice inside the real `src/` projects, to the standards in [CONTRIBUTING.md](../CONTRIBUTING.md), with tests. Each spike is time-boxed and ends in a written finding in [progress.md](progress.md). Code from a route that fails its go/no-go check is removed or parked behind an issue, not left half-finished.

## Development environment

Checked on 2026-11-10:

| Resource | Status | Use |
|---|---|---|
| Windows 11, .NET SDK 10.0.401 | Available | Main development machine; Windows spooler spikes |
| Mac | Available | Spike 1 on macOS's built-in CUPS; Spike 4 discovery check. .NET 10 SDK to be installed if missing. |
| WSL Ubuntu 20.04 | Available, **CUPS not installed** | Install `cups`, `cups-ipp-utils` (for `ippeveprinter`) and `printer-driver-cups-pdf` for Spike 1. Ubuntu 20.04 ships CUPS 2.3; a newer distro or container may be needed to check CUPS 2.4+/libcups 3 behaviour. |
| Docker Desktop 29.8 | Available | Repeatable CUPS host and `ippeveprinter` virtual printers |
| Microsoft Print to PDF | Available | Headless Windows PDF/GDI target (output file set via `DOCINFO.lpszOutput`) |
| HP ENVY 5640 (network, WSD port) | Available | Real IPP printer for Spike 4. **Read-only operations by default**; any physical print needs explicit approval. |

## Repository layout

The proof of concept creates the real solution structure. Projects are added only when a spike needs them.

```
Latiprimere/
├── Latiprimere.slnx
├── global.json                        # pins the .NET 10 SDK
├── Directory.Build.props              # net8.0;net10.0, Nullable, analyzers, warnings as errors, AOT flags
├── Directory.Packages.props           # central package version management
├── .editorconfig                      # enforces CONTRIBUTING.md rules
├── docs/
├── src/
│   ├── Latiprimere.Abstractions/       # Spike 0, 5
│   ├── Latiprimere/                    # Spike 0 (platform selection, DI)
│   ├── Latiprimere.Ipp/                # Spike 1, 4 (shared IPP transport + direct IPP)
│   ├── Latiprimere.Cups/               # Spike 1
│   ├── Latiprimere.Windows/            # Spike 2, 3
│   └── Latiprimere.Rendering.Pdfium/   # Spike 3
├── tests/
│   └── Latiprimere.*.Tests/            # one per src project, mirroring its folders
├── samples/
│   └── Latiprimere.Cli/                # console harness used to drive and demo each spike
└── tools/
    └── virtual-printers/              # scripts / compose file for ippeveprinter, CUPS-PDF, Windows test queues
```

## Spikes

### Spike 0: Solution scaffold and standards enforcement (½–1 day)

- Solution, `global.json`, `Directory.Build.props` (`<TargetFrameworks>net8.0;net10.0</TargetFrameworks>`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<AnalysisLevel>latest-recommended</AnalysisLevel>`, `<IsAotCompatible>true</IsAotCompatible>` for `src/`), and central package management.
- A `.resx` resource file per project for exception messages, with the generated resource class.
- `.editorconfig` covering the enforceable parts of [CONTRIBUTING.md](../CONTRIBUTING.md): tabs, braces always, `var` prohibited, `this.` qualification, no expression-bodied members, `ConfigureAwait(false)` (CA2007, relaxed for tests), async naming.
- `Latiprimere.Abstractions` with a first cut of the core contracts: `IPrinterCatalog`, `IPrinter`, `IPrintJob`, `PrinterInfo`, `PrintOptions`, `PrintJobStatus`, `PrintJobState`, `PrintContent`.
- `Latiprimere` with DI registration and platform selection (no adapters yet).
- Test projects with GivenWhenThen tests for the contracts; the CLI sample listing "no adapter registered".
- GitHub Actions workflow building and unit-testing both target frameworks on Windows, Ubuntu and macOS runners.

**Done when:** `dotnet build` and `dotnet test` succeed with zero warnings for `net8.0` and `net10.0` on all three CI runners, and a deliberate `var` fails the build.

### Spike 1: CUPS over IPP — Linux and macOS (2–3 days)

Talk to a local CUPS scheduler using managed IPP only (SharpIppNext), with no `libcups` P/Invoke. Builds `Latiprimere.Ipp` (shared transport) and `Latiprimere.Cups`.

1. Stand up CUPS in WSL or Docker with two queues: a CUPS-PDF queue and an `ippeveprinter` virtual IPP Everywhere printer (`tools/virtual-printers/`).
2. `CUPS-Get-Printers`: list queues with name, state, make/model.
3. `Get-Printer-Attributes`: read media, sides, colour, copies, document formats. Save the dumps as test data.
4. `Print-Job` with a PDF and with a raw text payload.
5. `Get-Job-Attributes` / `Get-Jobs`: follow status to completion; `Cancel-Job` a held job.
6. Try both transports: TCP `localhost:631` and the Unix domain socket via `SocketsHttpHandler.ConnectCallback`.
7. **Repeat steps 2–5 on the Mac** against macOS's built-in CUPS, using a local CUPS-PDF-style or `ippeveprinter` queue.

**Done when:** a PDF submitted from the CLI sample is produced by CUPS on both Linux and macOS, status transitions are observed, and cancel works.
**Record:** authentication requirements per operation, SharpIppNext gaps, Unix socket vs TCP behaviour, and any Linux/macOS differences (socket path, sandboxing, permissions).

### Spike 2: Windows spooler, raw jobs and status (1–2 days)

Builds `Latiprimere.Windows` using `LibraryImport` P/Invoke to `winspool.drv`.

1. `EnumPrinters` (local + connections): name, driver, port, status, default printer.
2. Create a test queue: "Generic / Text Only" driver on a local file port (setup script in `tools/virtual-printers/`).
3. `OpenPrinter` → `StartDocPrinter` (datatype `RAW`) → `StartPagePrinter` → `WritePrinter` → `EndDocPrinter`, with `SafeHandle` wrappers.
4. `EnumJobs` / `GetJob`: follow job status; `SetJob` with the cancel command.
5. Confirm the CLI sample works when published trimmed and Native AOT.

**Done when:** raw bytes land in the output file byte-for-byte, job status is observed, and the AOT build runs.
**Record:** status fidelity (what the spooler reports vs IPP job states), error-code mapping.

### Spike 3: Windows PDF via PDFium + GDI (2–3 days) — highest risk

Builds `Latiprimere.Rendering.Pdfium` and the GDI print path in `Latiprimere.Windows`.

1. Reference maintained PDFium native binaries via NuGet; bind the minimal API (`FPDF_LoadDocument`, `FPDF_RenderPageBitmap`, page size).
2. `CreateDC` for the target printer; `StartDoc` with `DOCINFO.lpszOutput` set so "Microsoft Print to PDF" runs without a prompt.
3. Per page: render at printer DPI (`GetDeviceCaps` `LOGPIXELSX/Y`), offset by the physical margins (`PHYSICALOFFSETX/Y`), `StretchDIBits`, `EndPage`.
4. Apply `PrintOptions` through `DEVMODE` (copies, duplex, orientation) via `DocumentProperties`.
5. Measure a 1-page and a 100-page mixed-content PDF: time, peak memory, output size, visual fidelity.

**Done when:** output PDFs are visually correct at 300/600 DPI, and a 100-page document prints within acceptable memory (target < 500 MB peak, page by page).
**Record:** measurements, quality notes, and whether banding is needed for large pages.

### Spike 4: Direct IPP to a network printer (1 day)

Extends `Latiprimere.Ipp` with direct printing and discovery.

1. Discover IPP printers on the LAN via mDNS (`_ipp._tcp`, `_ipps._tcp`) using a candidate managed mDNS library. Check discovery from both Windows and the Mac.
2. `Get-Printer-Attributes` against the HP ENVY 5640 and an `ippeveprinter` instance. Save the HP dump as test data.
3. `Validate-Job` for a PDF against the real printer (no paper used).
4. Full `Print-Job` against `ippeveprinter` only. A single real test page only with explicit approval.

**Done when:** discovery finds both printers on both machines, attributes are read, validation succeeds, and a job completes on the virtual printer.
**Record:** mDNS library choice, document formats the HP accepts, IPPS/certificate behaviour.

### Spike 5: Capability model (1 day)

Fills out the capability types in `Latiprimere.Abstractions` and the mappers in each adapter.

1. Records and enums: `PrintCapabilities`, `MediaSize`, `DuplexMode`, `ColorModes`, `PageOrientation`, `InputTray`.
2. Map from IPP attributes (Spikes 1 and 4) and Windows `DeviceCapabilities`/PrintCapabilities (Spike 2).
3. Note what doesn't map cleanly; propose the vendor-specific escape hatch as an ADR.

**Done when:** the same model populates from Windows, CUPS and direct IPP sources for the 5 core properties, with unit tests over the captured attribute dumps.

## Go / no-go criteria

| Outcome | Decision |
|---|---|
| All spikes meet their "done when" | Go: continue into Phase 1 on the same code base |
| Spike 3 fails or is too slow | Go, but Windows PDF ships later; lean on direct IPP for PDF on Windows; park the Pdfium project behind an issue |
| Spike 1 shows SharpIppNext is unsuitable | Go with `libcups` P/Invoke for the CUPS adapter; re-estimate |
| Two or more routes fail | Stop and reassess; consider contributing to PrintingTools instead |

## Out of scope for Phase 0

Print dialogs, NuGet publishing, documentation site, IPPS client certificates, performance tuning beyond Spike 3's measurements.
