# 0017: Resend jobs that a busy IPP printer turns away

- Status: Accepted
- Date: 2026-11-13

## Context

Some IPP printers take one job at a time. While one job is printing, they answer a new Print-Job with `server-error-busy` (0x0507), and RFC 8011 expects the client to try again later. `ippeveprinter` does this, and the integration tests hit it at once: the test processes for the two target frameworks printed to it together.

CUPS handles this for its own queues: its IPP backend holds the job and retries. Printing directly to a network printer has no queue in between, so before this decision Latiprimere passed the busy error to the caller as a `PrintException`. Every application printing to a shared printer would have had to write the same retry loop.

## Options

- **(a) Leave it to callers.** The `PrintException` carries the IPP status in `NativeErrorCode`, so callers can retry. Simple, but every caller has to know about the status.
- **(b) Resend inside `IPrinter.SubmitAsync` for a limited time,** with a setting to change or switch it off.
- **(c) Queue jobs inside Latiprimere** and send them when the printer is free. That is a print spooler, outside the library's scope ([ADR 0001](0001-headless-ipp-first-scope.md)).

## Decision

(b). Owner decision, 2026-11-13.

- **Direct IPP only.** `IppNetworkOptions.BusyRetryDuration` sets how long to keep resending. It defaults to 2 minutes; `TimeSpan.Zero` sends each job once. CUPS queues don't retry, as CUPS queues jobs itself.
- **Only `server-error-busy` is retried.** Every other rejection is thrown at once, as before.
- **Growing waits.** Each attempt uploads the whole document again, so the wait between attempts starts at 1 second and doubles, up to 15 seconds. No attempt starts after the duration has run out. The caller's cancellation token stops the waiting too.
- **Seekable documents only.** The document is sent again from the position it started at. A stream that can't seek can't be sent twice, so it fails on the first busy answer. Buffering it in memory could take a lot of memory for a large PDF.
- **When the time runs out,** the last busy error is thrown, unchanged.

## Consequences

- `SubmitAsync` can take up to the retry duration, plus one upload, when a printer stays busy. Applications that need a quicker answer lower the duration or cancel.
- A printer that is busy for a long time with a large job, such as a 200-page report, may still be busy after 2 minutes. That is what the setting is for.
- The integration tests print several PDFs to `ippeveprinter` at once and expect every job to complete. With retries switched off, that test failed in 5 of 6 runs.
- Other temporary statuses, such as `server-error-temporary-error` or `server-error-not-accepting-jobs`, are not retried. Add them if real printers show they are worth it.
