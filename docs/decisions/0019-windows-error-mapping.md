# 0019: Windows error mapping

- Status: Accepted
- Date: 2026-11-13

## Context

Every failed spooler or GDI call became a `PrintException` with the Win32 error as `NativeErrorCode`. Its message gave the function, the printer and Windows' own description. That was accurate, but:

- **Windows' text often doesn't say what to do.** "The RPC server is unavailable" means the Print Spooler service is stopped. "The system cannot find the path specified" from `StartDocPrinter` means `PrintOptions.OutputFile`'s folder doesn't exist.
- **Calls that fail without setting an error** were described as "The operation completed successfully".
- **The exception type didn't follow the contracts.** A printer removed after it was looked up gave a plain `PrintException`, not `PrinterNotFoundException`. A driver refusing raw data gave a `PrintException`, although `IPrinter.SubmitAsync` promises `NotSupportedException` for a format the printer can't accept.

## Decision

The Phase 1 checklist item. A new `WindowsErrorMapper` maps the Win32 error of a failed call to an exception. It is pure, so it is tested on every operating system, and `SpoolerErrors` delegates to it.

| Win32 error | Exception | Explanation added to the message |
|---|---|---|
| 1722, 1726, 1727 (RPC failures) | `PrintSystemUnavailableException` | The Print Spooler service isn't running; how to start it |
| The same, for a `\\server\printer` connection | `PrintException` | The print server can't be reached |
| 1801, 1905, 3012 | `PrinterNotFoundException`, with `PrinterId` | The printer has been removed from Windows |
| 1804 | `NotSupportedException` | The driver doesn't accept raw printer data |
| 5 | `PrintException` | Access denied, perhaps for the account the application runs as |
| 1797 | `PrintException` | The driver isn't installed |
| 63 | `PrintException` | The job was deleted while it was being sent |
| 1223 | `PrintException` | Cancelled, for example a driver's Save dialog; virtual printers need `OutputFile` without a user interface |
| 2, 3 | `PrintException` | A file or folder is missing; check `OutputFile`'s folder |
| 39, 112 | `PrintException` | The disk is full |
| 0 | `PrintException` | "Windows gave no reason", instead of Windows' "completed successfully" |
| Any other | `PrintException` | None; Windows' text is enough |

- **What every message keeps:**
  - the explanation comes first;
  - then the failed function, the printer (left out for calls about no one printer) and Windows' own description.
- **Where the code goes:** every `PrintException` and subclass carries the Win32 code in `NativeErrorCode`. `NotSupportedException` has nowhere to put it.
- **`PrintSystemUnavailableException` now also means "this print system's service isn't running",** not only "no adapter is registered". Its documentation says so. It is a `PrintException`, so the catalog still skips the Windows print system while others answer ([ADR 0008](0008-print-system-failure-isolation.md)). When Windows is the only print system, the application gets the unavailable exception and its explanation.
- **The Win32 error codes** are in one place, `Interop/Win32ErrorCodes`, including the four that were in `WinSpoolConstants`.

## Consequences

- Applications can catch `PrinterNotFoundException`, `PrintSystemUnavailableException` and `NotSupportedException` for these cases, as the contracts promise, and still read the Win32 code.
- **Not observed live:** a stopped spooler, a deleted printer, access denied and a driver refusing raw data need admin rights or another account to produce. They are covered by unit tests only. The missing output folder (error 3) is checked live by an integration test against the XPS Document Writer.
- **Some failures raise no error at all,** so no error mapping can catch them. Raw data sent to Microsoft Print to PDF is accepted and reported Completed, but the file is empty. The XPS Document Writer, also a v4 driver, passes raw data through unchanged.

  **Owner decision, 2026-11-13: refuse raw data to drivers known to drop it.**
  - `DriverQuirks` lists them by driver name, which users can't rename, unlike printer names. It starts with "Microsoft Print To PDF".
  - `WindowsPrinter` throws `NotSupportedException` for a raw job to such a driver before anything is queued, saying to send a PDF instead.
  - Drivers are added only after being seen to drop data.

  **Owner decision, 2026-11-16: refuse PDFs to drivers that print text only** (owner: "Yes, add the check").
  - The end-to-end harness sent a three-page PDF to a Generic / Text Only queue on a file port. The job went from printing to an error and held every job behind it until it was deleted. That driver can't print the page images a PDF is printed as, and each job also spooled about 400 MB of them.
  - `DriverQuirks` lists such drivers by name too, starting with "Generic / Text Only". `WindowsPrinter` throws `NotSupportedException` for a PDF to one before anything is rendered or queued, saying to send raw data instead.
  - Only the spooler's path refuses: a PDF that goes straight to the queue's IPP printer (ADR 0027) never reaches the driver, so it isn't refused.
  - Unlike Microsoft Print to PDF, this failure isn't silent, but it stops the queue, which is worse for an application printing receipts or labels through it.
- The CUPS and IPP adapters map IPP status codes separately. If they gain explanations, this table is the pattern.
