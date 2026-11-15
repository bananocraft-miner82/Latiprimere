# 0007: Windows spooler adapter design

- Status: Accepted
- Date: 2026-11-11

## Context

Spike 2 built `Latiprimere.Windows` on the Win32 print spooler API (winspool.drv) for raw jobs, job status and cancellation. PDF printing (Spike 3) will add a GDI path on the same foundation.

## Decision

- **Interop:** `LibraryImport` declarations with blittable structs (`PrinterInfo2`, `JobInfo1`, `DocInfo1`) whose string fields are pointers into spooler-owned or pinned buffers. Printer handles are wrapped in `SafePrinterHandle`. The project allows unsafe code for this. Layout tests check the struct sizes and offsets against the native definitions.
- **Testable boundary:** an internal `IWinSpool` interface with blocking, managed calls (`EnumeratePrinters`, `GetPrinter`, `GetDefaultPrinterName`, `SubmitRawJob`, `GetJob`, `DeleteJob`). `WinSpoolApi` is the real implementation; the rest of the adapter is unit-tested against a fake.
- **Async:** spooler calls block, so the public async methods run them with `Task.Run`.
- **Errors:** Win32 errors become `PrintException` with the Win32 error as `NativeErrorCode` and the Windows error text in the message. "Not found" errors (unknown printer 1801, finished job 87) become `null` / `false` rather than exceptions.
- **Printer identifiers** are the spooler printer name prefixed with `windows:`. Lookups ignore case, as Windows does.
- **Raw jobs** use the `RAW` data type, one page, written in 64 KB chunks. A job that fails part-way is deleted from the queue. `PrintOptions` such as copies and duplex are not applied to raw data; the data controls the output.
- **PDF** is rejected with `NotSupportedException` before anything is sent, until Spike 3.
- **Job status:** JOB_STATUS flags map to Latiprimere states, most decisive first: deleted, completed (printed or sent), paused (held), stopped (error, offline, paper out, intervention), processing. Windows removes finished jobs without recording the outcome, so a job that has left the queue is reported as `Cancelled` if Latiprimere cancelled it, and otherwise as `Completed` with a message explaining that the outcome isn't recorded.
- **Other platforms:** the factory returns a print system that reports itself unsupported, so `AddLatiprimereWindows()` is safe to call everywhere.

## Consequences

- A Native AOT build of the CLI produced no trimming or AOT warnings and reads the real spooler.
- "Completed" on Windows means the spooler finished sending the job to the port; it is weaker than IPP's completed state. Printers with "Keep printed documents" set give a definite final state.
- Raw jobs to v4 (XPS-based) drivers will be refused by the spooler; this has not been exercised yet.
