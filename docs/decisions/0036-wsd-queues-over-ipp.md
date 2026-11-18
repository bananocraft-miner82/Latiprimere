# 0036: Windows WSD queues over IPP, matched by UUID

- Status: Accepted
- Date: 2026-11-18

## Context

[ADR 0027](0027-direct-ipp-for-windows-queues.md) routes a Windows queue's PDF jobs straight to its printer over IPP, when `WindowsOptions.PrintPdfOverIpp` is on and the queue's IPP URI is known exactly:
- configured in `IppPrinterUris`;
- or the queue's port is named after the printer's URL.

It never guessed. So WSD queues, the kind Windows sets up itself for most network printers, needed `IppPrinterUris`. It also routed only printers that take PDF, so a printer like the HP ENVY 5640, which takes Apple raster but not PDF, stayed on its driver.

ADR 0027's follow-up found what makes matching safe. A WSD port's registry key, `HKLM\SYSTEM\CurrentControlSet\Control\Print\Monitors\WSD Port\Ports\<port>`, records the printer's UUID as `Printer UUID`. That UUID is the printer's IPP `printer-uuid`, which IPP Everywhere printers also announce over mDNS. On the development PC on 2026-11-18, the Canon TS7500i's WSD port records `00000000-0000-1000-8000-00117d58570e`, read without administrator rights.

[ADR 0035](0035-printing-pdfs-as-raster.md) prints PDFs as raster over direct IPP. [Phase 3's plan](../phase-3-plan.md), P3-4, joins the two.

## Decision

- **Matching a WSD queue:** when a queue has no exactly known URI, and isn't on a print server, Latiprimere:
  - reads its port's `Printer UUID`, through the spooler interface (`IWinSpool.GetWsdPrinterUuid`);
  - searches mDNS for a printer announcing that UUID. It stops as soon as one answers over plain IPP, and otherwise waits up to two seconds (`IppNetworkOptions.DefaultDiscoveryDuration`), preferring plain IPP over IPPS as discovery does;
  - asks that printer for `printer-uuid` with the formats and operations it already asks for. It routes only if the printer reports the same UUID. One that reports another UUID, or none, isn't trusted, so the driver prints the job.

  UUIDs are compared lower case, without `urn:uuid:`. Only a printer that announced the queue's UUID, and then confirmed it, is ever sent a job, so [ADR 0024](0024-known-ipp-printer-uris.md)'s rule against guessing holds.
- **The port name goes into a registry path,** so it's read only if it's `WSD-` followed by letters, digits, dots and dashes, at most 100 characters. Otherwise, or if the value is missing or can't be read, there's no UUID and no routing.
- **Remembering:**
  - **A printer found:** the route remembers where. A later job goes there without searching, and the UUID check still runs on every new `WindowsPrinter`.
  - **A remembered printer that doesn't answer, or reports another UUID:** it's forgotten, the driver prints that job, and the next job searches again.
  - **A UUID nobody announced:** it isn't searched for again for five minutes, so a queue whose printer doesn't speak IPP costs one search, not one per job.
  - **A search that fails,** for example without multicast: the job prints through the driver, logged at Information.
- **Raster:**
  - **The rasteriser:** the route's IPP context gets the Windows print system's PDF rasteriser and `WindowsOptions.MaxRenderDpi`, so a routed PDF the printer can't take goes as PWG raster or Apple raster (ADR 0035).
  - **Which printers route:** a printer routes if it takes PDF, or if there's a rasteriser and it takes a raster format. Otherwise the job stays on the driver, as before.
  - **`MaxRenderDpi`:** for raster it chooses among the resolutions the printer lists (ADR 0035), where the driver path renders at the limit and scales up.
  - **A raster job that can't be planned,** such as a printer whose default paper has no size and a job that chose none: it throws `NotSupportedException` before anything is sent, so the job goes through the driver instead, logged at Information.
- **Unchanged from ADR 0027:**
  - off by default;
  - print-server queues are never routed;
  - output files, Windows trays and `windows:` vendor options keep a job on the spooler;
  - a connect failure before the job is created falls back to the spooler.
- **Logging:** at Debug, a queue matched to its printer, with the printer's URI. At Information, a mismatch, a failed search, and a raster job sent through the driver instead.

## Consequences

- **WSD queues route without configuration:** a queue Windows set up itself for an IPP Everywhere or AirPrint printer routes once `PrintPdfOverIpp` is on, with nothing in `IppPrinterUris`. The HP ENVY's queue should send PDFs as Apple raster and report how each job ended.
- **The first PDF job to a WSD queue can wait up to two seconds** while the network is searched. Later jobs don't, whether the printer was found or not, for five minutes in the latter case.
- **Raster-only printers** behind any routed queue print over IPP instead of through their driver.
- **No public API changes.** `IWinSpool` and `IIppPrinterDiscovery` are internal.
- **Testing:**
  - **Unit tests:** matching, remembering and forgetting, the five-minute wait, print-server and port checks, a failed search, the UUID check, raster to an Apple raster printer, and a raster job that can't be planned. They use the development PC's real WSD port name and UUID.
  - **The registry:** read for every WSD port on the machine, checked against the registry itself.
  - **Harness, on the scripted printer:**
    - `windows.ipp-route.raster-to-printer`: Microsoft Print to PDF's queue, mapped to a printer with the HP's attributes, sends it three gzipped Apple raster pages at `MaxRenderDpi`;
    - `windows.ipp-route.fallback-no-pdf`: now gives its printer neither PDF nor a raster format, since one that takes Apple raster is now routed.
  - **Manual:** the harness's `manual.windows-queue-over-ipp` prints a three-page PDF to a named WSD queue with routing on. It checks the job went over IPP and completed, and asks what came out. Printing on the HP is the plan's "done when", and needs its WSD queue, which is no longer on the development PC, and the owner's go-ahead (D9).
- **Not done:**
  - **Microsoft's IPP Class Driver queues:** still unexamined, as no such queue is available. They need `IppPrinterUris` until one has been looked at with [tools/windows-queue-report](../../tools/windows-queue-report/README.md).
  - **Standard TCP/IP ports** record only a host. Their printers aren't matched, since a host alone doesn't identify a printer.
