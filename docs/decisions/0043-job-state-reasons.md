# 0043: Why a job is in its state

- Status: Accepted (owner, 2026-11-20: "Accept ADR 0043")
- Date: 2026-11-20

## Context

Spike 1 found that CUPS accepts a job with an option its printer can't carry out, such as two-sided printing on a one-sided queue, then cancels it. Latiprimere reported only `Canceled`, with no message, so an application couldn't tell a job its user cancelled from one the printer refused.

- **What printers say:** IPP printers and CUPS give a job's `job-state-reasons` (RFC 8011, section 5.3.8): keywords such as `job-canceled-by-user`, `job-canceled-at-device`, `aborted-by-system`, `unsupported-attributes-or-values` or `printer-stopped`. They give a `job-state-message` less often, and CUPS gives none for a job it cancels.
- **What Latiprimere asked for:** `job-state` and `job-state-message`, not the reasons.

Item 4 of the review of what's left before 1.0. Owner: "Start item 4".

## Decision

- **`job-state-reasons` is asked for with every job status,** alongside the attributes already read. No other request is made.
- **A public addition, `PrintJobStatus.StateReasons`:**
  - the keywords as the print system gives them, in lower case, in order, once each, without `none`;
  - an `IReadOnlyList<string>`, empty when there are none, so code can act on them without parsing messages. The Windows spooler gives none, except for jobs it sends to a queue's IPP printer.
  - Strings rather than an enum: printers add their own keywords, and the PWG adds more. An enum would lose those, or need changing for each.
- **`StateMessage` explains the most telling reason when the printer gives no message of its own:**
  - a printer's own message is kept;
  - otherwise, the first of a fixed list of about 30 keywords the job has gives the message, from `DisplayStrings`. The list puts the cause before who cancelled, so options the printer refused explain more than "cancelled at the printer". Who cancelled comes before why a job waits;
  - reasons that need no explaining, such as `job-printing` or `job-completed-successfully`, and keywords Latiprimere doesn't know, give no message, as before;
  - one message, not one per reason, so it reads as a sentence and changes only when the job does.
- **A change of reasons is a change of status:** `WatchStatusAsync` reports a status when its reasons change, as when its state, message or page count does. The lists are compared by content, so a poll that reads the same reasons again reports nothing.
- **For every IPP job:** CUPS queues, printers printed to directly, and Windows queues' jobs sent over IPP, as all three go through the same mapping.

## Consequences

- **A job CUPS cancels says why,** in its reasons and its message, and code can tell a user's cancellation from the printer's.
- **Some statuses gain a message they didn't have,** such as "The job was cancelled by the user who sent it." for a job Latiprimere cancelled, and "The job is waiting: the printer is stopped." Code that took a message as a sign of trouble should look at `StateReasons` instead.
- **Not done:**
  - **the Windows spooler's job status flags,** such as paper out or offline, aren't turned into reasons: they describe the printer rather than the job, and Windows sets them unreliably.
- **Tests:**
  - unit tests of reading and explaining reasons, the mapping, `WatchStatusAsync`, and the default;
  - the harness's IPP lifecycle scenarios now check the reasons and messages of an aborted, a waiting and a cancelled job;
  - a CUPS integration test sends a two-sided job to the one-sided Virtual-IPP queue and expects reasons and a message.

## Outcome against CUPS (2026-11-20)

The integration test, run against the CUPS test container (owner: "Start Docker and run the CUPS integration test"), failed as first written:

- **CUPS gives the cancelled job a single reason,** `processing-to-stop-point`, which says only that the job stopped, so Latiprimere doesn't explain it;
- **it gives no `job-state-message`;**
- **it says why in `job-printer-state-message`,** a CUPS extension: "Printer cannot print with supplied options."

So the decision gains one step. Get-Job-Attributes asks for `job-printer-state-message` as well, and is sent in its raw form, since SharpIppNext doesn't model the attribute; its typed response is still read from the same answer. A job with no `job-state-message` takes CUPS's `job-printer-state-message` instead, and only a job with neither has its reasons explained.

`StateMessage` now comes from, in order:

1. the printer's `job-state-message`;
2. CUPS's `job-printer-state-message`;
3. Latiprimere's explanation of the reasons.

The test then passed on both target frameworks. A CUPS job's message now also follows CUPS while it prints, such as "Spooling job", so `WatchStatusAsync` reports those changes too.
