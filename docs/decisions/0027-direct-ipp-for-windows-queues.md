# 0027: Direct IPP for Windows queues

- Status: Accepted
- Date: 2026-11-16

## Context

On Windows, a PDF goes to the printer the hard way. Latiprimere renders each page to an image with PDFium and draws it through the printer's driver ([ADR 0009](0009-windows-pdf-printing.md)). That has three costs:

- **Time and memory:** 100 A4 pages at 600 DPI took 66 seconds and 225 MiB.
- **Quality:** the printer receives pictures of pages, so text is never sharp vector text, and a PDF saved with Microsoft Print to PDF has no text at all.
- **No outcome:** Windows forgets a job once it leaves the queue, so Latiprimere reports `Completed` without knowing whether it printed ([ADR 0007](0007-windows-spooler-adapter-design.md)).

Many of these printers speak IPP and accept PDF themselves. Printed over IPP, a PDF is uploaded as it is, and the printer reports how the job ended. [feasibility.md](../feasibility.md) expected this ("can be reduced by preferring direct IPP when the queue is IPP-capable"), and plan.md lists it for Phase 2.

Windows is also moving this way. Windows prefers its inbox IPP class driver for new printers from July 2026, and Windows Protected Print mode allows only that driver, so more and more queues will sit in front of IPP printers.

**What a Windows queue tells us** (seen on the development PC, 2026-11-16):

| Queue | Port monitor | What the port gives |
|---|---|---|
| HP ENVY 5640, vendor driver | WSD Port Monitor | Only a local GUID (`WSD-a316…`). The printer's address appears only in the queue's location, as its WSD endpoint (`http://[fe80::…]:3911/`) |
| A printer on a Standard TCP/IP port | TCPMON | A host and port 9100, raw. Not an IPP URI, and IPP paths vary by printer (`/ipp/print`, `/ipp`, `/printer`) |
| A queue added by URL (Internet Port or IPP port) | Internet Port | The port name *is* the URL, such as `http://printer:631/ipp/print` |

Microsoft doesn't document how Windows 11 names the ports of queues it sets up itself for driverless IPP printers, and this PC has none to look at. **That needs checking on a machine that has one** before relying on it.

Some limits apply whatever is chosen:

- A queue shared from a print server (`\\server\printer`) may sit in front of a printer this computer can't reach.
- A job sent straight to the printer bypasses the Windows queue. It doesn't appear in Windows' queue window, and doesn't follow the queue's pause, priority, pooling, accounting or permissions.
- Queue defaults chosen in Windows, such as a default tray or duplex, aren't the printer's IPP defaults.

## Options

- **(a) Leave it to the application.** IPP printers already appear in the catalog under their own `ipp:` IDs, found on the network or configured. An application that wants direct IPP prints to that ID. Nothing to build, but every application must work out which `ipp:` printer is which Windows queue. Discovered printers on Windows are also listed twice, once as each.
- **(b) Route PDF jobs for known queues over IPP, inside the Windows print system, when the application turns it on.**
  - The printer keeps its `windows:` ID.
  - A PDF job goes straight to the printer when Latiprimere knows the queue's IPP URI exactly and the printer accepts `application/pdf`. Every other job goes through the spooler, as today.
- **(c) Merge printers in the catalog:** recognise one physical printer listed by both print systems, by UUID or address, and prefer its `ipp:` entry. Tying a Windows queue to an IPP printer needs the same information as (b), but changes what the catalog lists and which IDs applications keep.
- **(d) Send the PDF through the spooler unchanged,** as raw data, to queues whose printer accepts PDF. The Windows queue stays in charge. But whether the port labels the data as PDF, and whether the printer recognises it, depends on the port monitor and the printer. A printer that guesses wrong prints the PDF's source as text, and the spooler still reports nothing back.

## Recommendation

**(a) now, as documentation, and (b), off by default.**

### When a PDF job goes over IPP

A job goes over IPP only if all of these hold. Otherwise it goes through the spooler exactly as today.

1. **`WindowsOptions.PrintPdfOverIpp` is on.** It is off by default, because the job leaves the Windows queue (see Context).
2. **The queue has an IPP URI Latiprimere is sure of.**
   - **Configured:** `WindowsOptions.IppPrinterUris` maps queue names to URIs, for queues whose port doesn't say, such as WSD and Standard TCP/IP ports.
   - **From the port:** the port name is an absolute `http`, `https`, `ipp` or `ipps` URI, as for queues added by URL. `http` becomes `ipp`, and `https` becomes `ipps`.
   - **Never guessed.** Latiprimere doesn't probe hosts from TCP/IP ports, or WSD endpoints, for IPP. Guessing would contact addresses nobody configured, against the spirit of [ADR 0024](0024-known-ipp-printer-uris.md), and might reach the wrong printer behind a reused address. This can be revisited once real queues have been examined, for example matching a WSD queue to a discovered printer by UUID.
   - **Queues on a print server** (`\\server\…`) are never routed.
3. **The printer accepts the document.** Get-Printer-Attributes is sent at the printer's first PDF job that could go over IPP, and the answer is kept for later jobs, so looking a printer up never waits on the network. The job goes over IPP only if the printer lists `application/pdf`. A printer that can't be asked, or demands a password, keeps its jobs on the spooler; the question is asked again at the next job.
4. **The job uses nothing only the spooler can do:**
   - `OutputFile` is set (IPP can't print to a local file);
   - an `InputTray` is chosen (Windows tray IDs are DMBIN numbers, which mean nothing over IPP);
   - a `windows:` vendor option is set (a DEVMODE only the driver reads).

   Any one of these sends the job through the spooler.

### What a routed job does

- **Options:** it is sent with `IppJobTemplateMapper`, as CUPS and network printers are. Paper sizes are matched by PWG name, which both print systems use, and copies, duplex, orientation and colour map directly. `ipp:` vendor options are sent too. That is a departure from [ADR 0026](0026-vendor-options-in-detail.md), where Windows ignores them, but it's what an application setting them for that printer means.
- **The job** is an `IppPrintJob` whose `PrinterId` stays the `windows:` ID. Its status and cancelling go to the printer, so its final state is the printer's: `Completed`, `Aborted` or `Cancelled`.
- **If the printer can't be reached** before the job is created (a connect failure or timeout), the job goes through the spooler instead, which holds it until the printer is back. Any later failure, such as a rejection or a lost connection during the upload, is reported, as for any IPP job. Retrying through the spooler could then print it twice.
- **Busy printers** are retried as direct IPP printers are ([ADR 0017](0017-retry-busy-ipp-printers.md)).

### Capabilities and packages

- **Capabilities** stay the Windows driver's, so what an application reads matches the queue the user knows.
- **`Latiprimere.Windows` gains a dependency on `Latiprimere.Ipp`,** and through it on SharpIppNext and Tmds.MDns.
  - Most applications that print on Windows will register both anyway.
  - A separate bridge package would avoid the dependency, at the cost of a seventh package, and the routing needs the Windows adapter's internals (the port name, the job submission path).

## Consequences

- **PDFs to IPP printers behind Windows queues** print as PDFs: faster, sharp, and lighter on memory. Their outcome is known.
- **Routed jobs don't appear in Windows' queue,** and the queue's policies don't apply to them. That is why it's off by default, and the option's documentation must say so.
- **Only exactly known URIs are used.** Until driverless queues on Windows 11 have been examined, many queues, including the WSD queue on the development PC, need `IppPrinterUris` to be routed.
- **One `windows:` printer can produce two kinds of job,** spooler and IPP, depending on the options. Each job's status is reported correctly for its kind. But an application that compares job IDs with Windows' queue window will find routed jobs missing.
- **PDFs to IPP printers that don't accept PDF** stay on the spooler. Phase 3's rendering to PWG raster or Apple raster could route them too later.
- **Raw jobs stay on the spooler,** which already sends them unchanged. Routing them would only add the outcome, and can follow if wanted.

## Decision

The recommendation, as written (owner: "Yes, build it", 2026-11-16):

1. Off by default.
2. `ipp:` vendor options apply to routed jobs; [ADR 0026](0026-vendor-options-in-detail.md) says so.
3. `Latiprimere.Windows` depends on `Latiprimere.Ipp`.
4. Still open: how Windows 11 names the ports of driverless IPP queues. Until someone with such a queue sends `Get-Printer` and `Get-PrinterPort` output, those queues need `IppPrinterUris`.

The printer's PDF support is checked at its first PDF job rather than at lookup, as point 3 above now says, so a lookup never waits on a printer that doesn't answer.

## To verify when built

- A queue added by URL, pointing at the test container's `ippeveprinter`, routes PDFs and reports their real outcome. It needs an admin to add the queue, so it belongs on the CI runner, which is elevated.
- A connect failure falls back to the spooler without printing twice.
- A WSD queue with an `IppPrinterUris` entry routes; without one, it doesn't.
