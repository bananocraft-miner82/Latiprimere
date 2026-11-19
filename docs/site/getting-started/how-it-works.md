---
title: How Latiprimere works
description: Print systems and the printer catalog, printer IDs, what happens to a document, and how a job moves through its states.
sidebar:
  order: 4
---

Latiprimere gives you one way to print, whichever operating system and printer you're printing to. This page explains the pieces behind that, so the rest of the docs make sense.

```text
your code ──▶ IPrinterCatalog ──▶ print systems ──────────▶ printers
              (one list)          Windows spooler, CUPS,
                                  network printers (IPP)
```

## The pieces

| Type              | What it is                                                                       |
| ----------------- | -------------------------------------------------------------------------------- |
| `IPrinterCatalog` | Every printer from every print system, in one place. Your code starts here.      |
| `IPrintSystem`    | One way of reaching printers: the Windows spooler, CUPS or IPP on the network.   |
| `PrinterInfo`     | A description of a printer: its ID, name and state when it was read.             |
| `IPrinter`        | A printer you can send documents to and ask about its capabilities.              |
| `PrintContent`    | A document to print: a stream, and whether it's a PDF or raw printer data.       |
| `PrintOptions`    | How to print it: copies, two-sided, orientation, paper, colour and tray.         |
| `IPrintJob`       | A job the printer has accepted, which you can follow or cancel.                  |
| `PrintJobStatus`  | What a job is doing: its state, a message from the printer and pages printed.   |

## Print systems and the catalog

A print system is how Latiprimere reaches printers. Each one is a package with an `Add…` method:

| Print system        | Package                  | Reaches                                           |
| ------------------- | ------------------------ | ------------------------------------------------- |
| Windows spooler     | `Latiprimere.Windows`    | The printers installed in Windows                 |
| CUPS                | `Latiprimere.Cups`       | The printers of a CUPS server, local or remote    |
| Network printers    | `Latiprimere.Ipp`        | Printers on the network, found or given by address |

You register the ones you want, and the catalog combines them. A print system that can't work on the machine, such as the Windows spooler on Linux, is left out, so the same code runs everywhere. The catalog never talks to printers itself: it passes each request to the print system that owns the printer.

- **Listing printers** asks every print system at once. One that fails is left out and a warning is logged.
- **The default printer** comes from the first print system, in the order you registered them, that has one.
- **Looking up a printer by ID** goes straight to the print system the ID belongs to.

Register the catalog as a singleton, as `AddLatiprimere()` does. It keeps no state of its own, and it, the printers and the jobs are all safe to use from several threads at once.

## Printer IDs

Every printer has an ID, unique across the catalog, such as `windows:Office Laser` or `cups:Office_Laser`. Store the ID when someone chooses a printer, and look the printer up with it later, even after a restart. Treat it as opaque: don't parse it or build one yourself. [Finding printers](/docs/getting-started/finding-printers/#printer-ids) lists the forms they take.

## Descriptions and printers

`GetPrintersAsync` returns `PrinterInfo` descriptions, not printers. A description is a snapshot: its `State` is the printer's state when the list was read, and it isn't updated. To print, look the printer up by ID with `GetPrinterAsync`, which reads its current details and returns an `IPrinter`.

An `IPrinter` sends documents to one printer. Its `Info` holds the description read when it was looked up. Keep it for as long as you like, but look it up again for fresh details.

## What happens to a document

Latiprimere prints two kinds of document:

- **PDF**, made with `PrintContent.FromPdf`. CUPS takes the PDF as it is and converts it for the printer. A network printer that accepts PDF gets it as it is too; one that doesn't is sent its pages as images, PWG or Apple raster, rendered by `Latiprimere.Rendering.Pdfium`, or refused with `NotSupportedException` before anything is sent if that isn't registered. The Windows spooler can't print PDFs, so `Latiprimere.Rendering.Pdfium` draws each page and the printer's driver prints the pages.
- **Raw printer data**, made with `PrintContent.FromRaw`: ZPL for label printers, ESC/POS for receipt printers, or PCL. It goes to the printer untouched, so it must be in the printer's own language. See [Labels and receipts](/docs/guides/labels-and-receipts/).

You own the stream and dispose it once the job is sent. Latiprimere reads it from its current position to the end. Prefer a stream that can seek, such as a file or memory stream: a busy network printer turns a job away, and only a stream that can seek can be sent again.

## Options and capabilities

`PrintOptions` says how to print. Anything left at its default, `PrinterDefault` or `null`, uses the printer's own setting, so `PrintOptions.Default` prints one copy the way the printer is set up.

`GetCapabilitiesAsync` says what a printer can do: its paper sizes, trays, colour modes, two-sided modes and orientations. Paper sizes and trays are values the printer reports, so choose them from its capabilities rather than making your own. A print system that doesn't report something leaves that list empty. See [Reading printer capabilities](/docs/guides/printer-capabilities/).

## How a job moves through its states

`SubmitAsync` returns once the print system has accepted the job, not once it has printed. From there the job moves through its states:

```text
Pending ──▶ Processing ──▶ Completed
   │  ▲          │
   ▼  │          ▼
  Held        Stopped        (any state) ──▶ Cancelled or Aborted
```

| State        | Meaning                                                                  | Final |
| ------------ | ------------------------------------------------------------------------ | ----- |
| `Pending`    | Queued, waiting its turn                                                 |       |
| `Held`       | Held in the queue until someone releases it                              |       |
| `Processing` | Being processed or printed                                               |       |
| `Stopped`    | Stopped partway, usually because the printer needs attention             |       |
| `Completed`  | Finished                                                                  | Yes   |
| `Cancelled`  | Cancelled, by your code or someone at the printer or queue               | Yes   |
| `Aborted`    | Ended by the print system because of an error                            | Yes   |
| `Unknown`    | The print system didn't say, or said something Latiprimere doesn't know  |       |

Print systems don't announce changes, so Latiprimere asks. `GetStatusAsync` asks once. `WatchStatusAsync` asks every half second, or at the interval you give, reports each change, and ends at a final state (`IsTerminal` is `true`).

A few things to know:

- **`Completed` means the print system has finished with the job.** Print systems forget finished jobs: the Windows spooler as soon as a job has printed, and printers with little memory soon after. Latiprimere reports a job that is no longer listed as `Completed`, or as `Cancelled` if you cancelled it.
- **A job can sit in `Unknown`.** Some printers don't report states, and `Unknown` isn't final, so `WatchStatusAsync` keeps asking. Always follow a job with a timeout.
- **Stopping following doesn't stop the job.** Cancelling the token you pass to `WatchStatusAsync` only stops your code waiting, and once `SubmitAsync` has returned, its token can't recall the job. To stop the job itself, call `CancelAsync`, which returns `false` if the job had already finished.

[Print options and following jobs](/docs/guides/print-options-and-jobs/) shows each of these in code.

## When things go wrong

Every Latiprimere exception derives from `PrintException`, and print systems are kept apart: one that fails doesn't stop the others from listing printers. `NotSupportedException` means the request itself can't work, such as raw data sent to a printer that only takes PDFs. [Errors and troubleshooting](/docs/guides/errors-and-troubleshooting/) lists each exception and what to do about it.

## Safe by default

- **Only known printers.** A printer ID can reach only printers the catalog already knows, so an ID that comes from a web request can't make your server connect somewhere else. `AllowAnyPrinterUri` and `AllowAnyPrinterName` relax this, and are off by default.
- **Passwords travel encrypted.** When you give a CUPS server a user name and password, Latiprimere connects over TLS and checks the server's certificate. A network printer gets its password only over IPPS, once you have pinned its certificate or it's one the computer trusts.

See [Network printers over IPP](/docs/print-systems/network-printers/), [Secure network printers](/docs/print-systems/secure-printers/) and [CUPS servers and the Windows spooler](/docs/print-systems/cups-and-windows/) for the settings.
