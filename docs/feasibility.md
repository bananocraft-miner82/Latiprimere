# Feasibility Assessment

> Researched: 2026-11-10. Re-check the "Landscape" and "Platform direction" sections before major milestones, as both are moving targets.

## Verdict

**Feasible, with a real but moderate gap.** A headless, UI-agnostic library that *discovers printers, reads capabilities, submits jobs and tracks job status* on Windows, Linux and macOS is achievable by a small team. A full "print anything with dialogs and preview" toolkit is not a sensible v1.

| Criterion | Assessment |
|---|---|
| Technical feasibility | **High.** Every OS exposes stable print APIs, and all three are converging on IPP. |
| Market gap | **Real, moderate.** Headless/server printing is the clearest unmet need. |
| Effort to v1 | ~2–3 months part-time. Windows PDF printing is roughly half of it. |
| Main long-term risk | Maintenance burden from printer quirks, not technology. |

## Why the gap exists

- `System.Drawing.Common` (including `PrintDocument` and `PrinterSettings`) became Windows-only in .NET 6. The `EnableUnixSupport` switch was removed in .NET 7. Microsoft's recommended replacements (SkiaSharp, ImageSharp) draw but do not print.
- Avalonia and MAUI ship no desktop printing API.
- Commercial options are partial or expensive: IronPrint (from ~$979, no Linux; silent printing is Windows-only), Spire.Printing (paid).
- Server and edge scenarios are poorly served: label and receipt printers (ZPL, ESC/POS), kiosks, warehouse systems on Linux containers or Raspberry Pi, backends printing invoices.

## Landscape

| Project | Licence | Status | Notes |
|---|---|---|---|
| PrintingTools | MIT | Alpha, ~42 stars | **Closest competitor.** Avalonia-oriented with a headless core. Linux adapter shells out to `lp`/`lpoptions`. Job monitoring mainly on Windows. |
| PdfPrintCore | Not stated | ~6K downloads, last update Dec 2024 | PDF only, native assets per platform. Appears dormant. |
| Avae.Printables | Not stated | Active | Avalonia-specific. Linux requires GtkSharp. |
| SharpIppNext | MIT | Mature (v5), no dependencies, targets netstandard2.0 + net10.0 | IPP client/server protocol library. **A building block, not a competitor.** Supports CUPS-specific operations. |
| IronPrint | Commercial | Active | Windows, macOS, iOS, Android. No Linux. |
| Spire.Printing | Commercial | Active | Windows, Linux, macOS. |

The NuGet package ID `CrossPrint`, the project's first name, was unclaimed as of 2026-11-10. The `Prodigy.` prefix appears to be reserved by an unrelated owner (see [ADR 0004](decisions/0004-naming-and-poc-approach.md)).

**Differentiation for Latiprimere:** framework-agnostic and headless/server-first; native APIs rather than shelling out to CLI tools; consistent job status on every platform; IPP-first; trimming and Native AOT friendly.

## Platform direction

All three platforms are converging on **IPP (Internet Printing Protocol)** and driverless printing (IPP Everywhere / Mopria):

- **Windows:** third-party printer drivers are being retired in stages. From July 2026 Windows prefers its inbox IPP class driver; from July 2027 third-party driver updates stop except for security fixes. Windows Protected Print mode supports only Mopria-certified printers (Microsoft cites 120M+ already sold).
- **Linux:** libcups 3.0.0 shipped as stable in 2026. CUPS 3 removes PPDs and printer drivers in favour of driverless IPP and "Printer Applications". Distributions will lag, so PPD-based queues will remain for some years.
- **macOS:** uses CUPS as its print system.

## Technical routes

| Route | Platforms | Mechanism | Notes |
|---|---|---|---|
| CUPS adapter | Linux, macOS | IPP to the local scheduler (`localhost:631` or the CUPS Unix socket), or P/Invoke to `libcups` | One adapter serves both platforms. CUPS accepts PDF and converts it for the printer. The managed IPP route avoids native binding differences between libcups 2 and 3. |
| Windows spooler adapter | Windows | P/Invoke to `winspool.drv` (`EnumPrinters`, `OpenPrinter`, `StartDocPrinter`, `WritePrinter`, `EnumJobs`, `SetJob`); PrintTicket/PrintCapabilities (`prntvpt.dll`) for capabilities | Raw jobs are straightforward. **PDF must be rasterised** (e.g. PDFium) and drawn to a printer device context via GDI. |
| Direct IPP | All | Managed IPP straight to network printers | Printers accept PDF and/or PWG Raster. Discovery needs mDNS/DNS-SD (`_ipp._tcp`, `_ipps._tcp`). |

## Hard parts

1. **PDF on Windows.** Rasterising, DPI and printable-area handling, memory use on large documents. Can be reduced by preferring direct IPP when the queue is IPP-capable.
2. **Capability normalisation.** DEVMODE/PrintCapabilities, IPP attributes and legacy PPD options describe printers differently. Needs a common model for shared settings plus an escape hatch for vendor-specific options.
3. **Printer quirks and support load.** Most issues will be "my printer does X". Support only IPP-conformant behaviour officially.
4. **Testing.** Solvable with virtual printers: `ippeveprinter` (ships with CUPS) on Linux/macOS, "Microsoft Print to PDF" and a Generic/Text Only queue on Windows.
5. **Overlap with PrintingTools.** Review its core before committing; contributing a headless/IPP layer there is a legitimate alternative.

## Sources

- [Microsoft Learn: System.Drawing.Common only supported on Windows](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/system-drawing-common-windows-only)
- [PrintingTools docs](https://wieslawsoltes.github.io/PrintingTools) / [repo](https://github.com/wieslawsoltes/PrintingTools)
- [PdfPrintCore](https://www.nuget.org/packages/PdfPrintCore)
- [Avae.Printables](https://packages.nuget.org/packages/Avae.Printables/)
- [SharpIppNext](https://www.nuget.org/packages/SharpIppNext)
- [Spire.Printing](https://www.nuget.org/packages/Spire.Printing)
- [IronPrint (ComponentSource)](https://www.componentsource.com/product/ironprint-for-net) / [ironsoftware.com/print](https://ironsoftware.com/print/)
- [Microsoft Modern Print Platform](https://learn.microsoft.com/windows/modern-print/modern-print-platform)
- [Neowin: end of servicing for third-party printer drivers](https://www.neowin.net/news/microsoft-announces-the-end-of-servicing-for-third-party-printer-drivers-on-windows/)
- [Ricoh Print Support Application (driver timeline)](https://www.ricoh.com/products/ricoh-print-support-application)
- [MyQ: Windows Protected Print mode](https://myq-solution.com/windows-protected-print-mode)
- [PWG: libcups v3.0.0 announcement](https://pwg.org/archives/ipp/2026/022015.html)
- [OpenPrinting: Printer Applications and Drivers](https://openprinting.github.io/cups/drivers.html)
