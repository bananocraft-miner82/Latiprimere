# 0033: Validate-Job before Print-Job

- Status: Accepted
- Date: 2026-11-18

## Context

A network printer refuses a job that asks for something it can't do with `client-error-attributes-or-values-not-supported` (`0x040B`). It answers only once the whole Print-Job request, document included, has arrived ([ADR 0010](0010-direct-ipp-adapter-design.md)). For a large PDF, or the raster pages Phase 3 will send to printers without PDF, that is a slow way to learn about a wrong paper size.

IPP's Validate-Job (RFC 8011, section 4.2.3) carries a job's operation and job attributes without the document, and the printer answers as Print-Job would. The HP ENVY and `ippeveprinter` both list it in `operations-supported`, as IPP Everywhere requires.

[Phase 3's plan](../phase-3-plan.md), P3-1, decision D5: on by default. Owner: "Yes, start P3-1".

## Decision

- **Before each Print-Job, `IppPrinter` sends Validate-Job** with the same attributes: printer URI, user name, job name, document format, job template attributes, and the vendor attributes in their groups. It's sent in its raw form, as Print-Job with vendor attributes is, so the printer judges exactly what the job will carry.
- **Only when both hold:**
  - the print system asks for it: direct IPP printers, with `IppNetworkOptions.ValidateJobs` (default `true`), and Windows queues routed to their IPP printer ([ADR 0027](0027-direct-ipp-for-windows-queues.md)), always. CUPS doesn't, since it accepts a job and cancels it later, so validating against the queue says little;
  - the printer lists Validate-Job (`4`) in `operations-supported`, which is read when the printer is looked up, with its formats. A printer whose operations weren't read isn't asked.
- **What its answer means:**
  - success: Print-Job follows;
  - an IPP client error (`0x04xx`): the job is refused with the same `PrintException`, IPP status as `NativeErrorCode`, that Print-Job would have thrown, and no document is sent;
  - an IPP server error (`0x05xx`), such as busy: not a judgement of the job. Print-Job goes ahead, with its busy retries ([ADR 0017](0017-retry-busy-ipp-printers.md)) and its own check, and the skip is logged at Debug;
  - no connection: the failure is thrown as for Print-Job. For a routed Windows job, that is the "couldn't reach the printer before anything was sent" case, so the job goes through the spooler instead, as ADR 0027 decided.

## Consequences

- **A refused option fails before the upload,** in one round trip.
- **Each job to a direct IPP printer costs one more short request.** Measured on the scripted printer, it adds well under a millisecond on the same machine; on a LAN, a few milliseconds.
- **Behaviour seen by callers is the same exception, sooner.** The harness's `ipp.submission.unsupported-value-refused` now checks that the refusal comes from Validate-Job and that no Print-Job is sent.
- **A printer that validates more loosely than it prints** would still refuse at Print-Job, as before.
- **Not checked yet on the real HP,** which was switched off: a Validate-Job alone, with no document, prints nothing, so the check is safe whenever it's on.
