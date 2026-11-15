# 0001: Headless, IPP-first scope for v1

- Status: Accepted
- Date: 2026-11-10

## Context

Existing .NET printing options are Windows-only (`System.Drawing.Printing`), tied to a UI framework (PrintingTools, Avae.Printables), dormant (PdfPrintCore) or commercial (IronPrint, Spire.Printing). The least-served need is headless printing from services, containers and edge devices. Windows, CUPS and macOS are all converging on IPP and driverless printing. See [feasibility.md](../feasibility.md).

## Decision

- v1 covers printer discovery, capabilities, job submission (raw and PDF), job status and cancellation. No dialogs, preview or layout engine.
- The core packages have no UI framework dependency.
- IPP is the primary protocol: the CUPS adapter (Linux, macOS) talks IPP to the local scheduler, and a direct IPP adapter talks to network printers. The Windows spooler adapter covers queues that are not reachable over IPP.
- Native APIs are used directly; no shelling out to `lp`, `lpr` or PowerShell.

## Consequences

- One adapter (CUPS over IPP) serves both Linux and macOS.
- Windows needs its own adapter plus a PDF rasteriser for non-IPP queues, which is the largest single piece of work.
- UI integrations can be added later as separate packages without changing the core.
- Legacy printers that only work through vendor drivers are supported via the OS spooler, but quirks there are best-effort.
