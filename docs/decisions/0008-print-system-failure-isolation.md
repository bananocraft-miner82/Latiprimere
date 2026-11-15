# 0008: Isolate print system failures in the printer catalog

- Status: Accepted
- Date: 2026-11-11

## Context

`PrinterCatalog` combines every supported print system. In Spike 2, with `CUPS_SERVER` set but the server unreachable, listing printers failed outright and the working Windows printers weren't shown either. Options were to keep failing, to skip a failing print system, or to return the printers found plus the errors.

## Decision

- **Listing printers and finding the default** skip a print system that throws `PrintException` and log a warning with the exception. The call fails only if every print system fails:
  - with one print system, its exception is rethrown unchanged (stack trace and `NativeErrorCode` preserved);
  - with several, a `PrintException` is thrown wrapping all of them in an `AggregateException`.
- **Finding the default:** if a print system fails and no other has a default, the result is `null` unless every print system failed.
- **Lookups by printer ID** are not skipped: an ID belongs to one print system, so its failure is the real answer.
- **Only `PrintException` is isolated.** Other exceptions, including cancellation and programming errors, propagate.

## Consequences

- One unreachable print server no longer hides the local printers.
- Callers can't tell from the result alone that a print system was skipped; they see it in the log. If that turns out to be needed, a result type carrying the failures can be added later (option c) without changing this behaviour.
- The CLI sample doesn't configure logging, so it doesn't show the warning.
