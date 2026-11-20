# 0049: Following printers and jobs

- Status: Accepted (owner, 2026-11-20: "Accept ADR 0049")
- Date: 2026-11-20

## Context

Before this, a job was followed by `PrintJobExtensions.WatchStatusAsync`, an extension that read its status every half second. A printer couldn't be followed at all: `IPrinter.Info` is a snapshot from when the printer was looked up, and looking it up again was the only way to see its state.

Phase 4's plan ([phase-4-plan.md](../phase-4-plan.md), accepted) asks for events before 1.0 (P4-5):

- **D9:** `IPrintJob.WatchStatusAsync` becomes a member of the interface, and `IPrinter.WatchStateAsync` follows a printer's state. Members, not the optional interfaces [ADR 0044](0044-room-to-grow-after-1-0.md) describes for after 1.0, as the interfaces can still change before it.
- **Where events come from:**
  - CUPS, and network printers that offer them, through IPP notifications pulled with `ippget` (RFC 3995, RFC 3996). The plan expected Get-Notifications to wait for the next event;
  - the Windows spooler's change notifications;
  - polling where neither is available.
- **D10:** printers appearing and going aren't covered.

Two things found while building it changed the plan:

- **CUPS doesn't wait.** In the CUPS 2.4 test container, a printer subscription was created and Get-Notifications sent with `notify-wait` set. CUPS answered at once, with no events and `notify-get-interval` 60: "ask again in a minute". So with `ippget`, a change on CUPS would arrive up to a minute late, or the notifications would have to be polled as often as the status is now. The HP ENVY 5640 and `ippeveprinter` list no subscription operations. So no IPP printer or server available here benefits from notifications.
- **Polling a printer needs a way to read its state.** The plan named only a watch. A watch that polls must read the state, and an application may want it once.

Owner: "Proceed with P4-5".

## Decision

### Public API

- **`PrinterStatus`,** a new record:
  - `PrinterId`, `State`, `StateMessage`, `StateReasons` (IPP `printer-state-reasons` keywords, as `PrintJobStatus.StateReasons` has the job's), `IsAcceptingJobs` and `Timestamp`.
- **`IPrinter.GetStatusAsync(cancellationToken)`:**
  - reads the printer's state now;
  - a default implementation gives `Info.State`, so classes of your own that implement `IPrinter` keep compiling.
- **`IPrinter.WatchStatusAsync(pollInterval, cancellationToken)`:**
  - reports the status, then each change, until cancelled, as a printer has no final state;
  - named after its status, not "state", to match `IPrintJob`;
  - by default it polls `GetStatusAsync`.
- **`IPrintJob.WatchStatusAsync(pollInterval, cancellationToken)`:**
  - the member the extension's main overload was;
  - by default it polls `GetStatusAsync`, as the extension did;
  - an implementation that can be told of changes follows them instead.
- **The extensions stay:**
  - `PrintJobExtensions.WatchStatusAsync(job)` and `(job, cancellationToken)` call the member with `DefaultPollInterval`;
  - the extension that took an interval stays too, hidden from IntelliSense, so code compiled against earlier versions still runs; code compiled now calls the member;
  - `PrinterExtensions.WatchStatusAsync(printer, cancellationToken)` follows a printer with its own `DefaultPollInterval` of 5 seconds, as printers change less often than jobs.
- **Duplicates:** a status is reported again only when what it says changes, never twice the same: for a job, its state, message, reasons or page count; for a printer, its state, message, reasons or whether it accepts jobs.
- **Default members** are reached through the interface type, as C# has it: code holding a class of its own, not `IPrintJob` or `IPrinter`, casts to call them.

### Where changes come from

- **Windows:**
  - `FindFirstPrinterChangeNotification`, through `LibraryImport` as the rest of the spooler code: `PRINTER_CHANGE_JOB` for a job, `PRINTER_CHANGE_PRINTER` for a printer;
  - the thread pool waits on the handle, so no thread is held while nothing changes;
  - each signal makes the status be read again, and duplicates are dropped;
  - the status is read at least every 30 seconds anyway, as a driver's own status, such as paper out, may not be signalled;
  - a queue the spooler can't signal, as some print servers' can't, is polled at the interval given.
- **CUPS and network printers:** polled at the interval given, not by IPP notifications, for the reasons above. They can be added behind the same members later, for printers that honour `notify-wait`, without changing callers.

### Printer status on each print system

- **IPP and CUPS:** Get-Printer-Attributes with `printer-state`, `printer-state-message`, `printer-state-reasons` and `printer-is-accepting-jobs`. The reasons come without `none`, each once, in lower case.
- **Windows:** the spooler has no message. Its status flags become the IPP reasons that say the same:
  - `paused`, `deactivated` (pending deletion) and `offline-report` (offline, not available, or working offline);
  - `media-jam-error`, `media-empty-error`, `media-needed-error` (paper problem) and `media-needed-report` (manual feed);
  - `door-open-error`, `toner-empty-error`, `toner-low-warning` and `output-area-full-error`;
  - `interpreter-resource-unavailable-error` (out of memory);
  - `other-error` (user intervention, page punt or error).

  A queue accepts jobs unless it is being deleted.

## Consequences

- **Applications follow printers** as they follow jobs, with one loop, whatever the print system.
- **Jobs on Windows are seen to end as soon as the spooler knows,** not at the next poll, and an idle watch costs no requests.
- **CUPS and network printers cost what they did:** a request every interval.
- **Tests:**
  - **the default members:** a printer's status from its `Info`, each change reported once, invalid intervals refused before anything is read, and the extensions;
  - **IPP:** the attributes asked for, reasons cleaned, a printer that's gone, and polling;
  - **Windows:**
    - status flags to state, reasons and accepting;
    - a printer followed by its notification, with an unchanged signal reporting nothing, the poll interval never waited, and the notification closed when the watch stops;
    - polling when the spooler can't signal;
    - a job followed to its end;
  - **harness scenarios:**
    - `cups.printer-status.paused`;
    - `windows.printer-status.paused`;
    - `ipp.printer-status.polled`;
    - `windows.jobs.watch-by-notification`, where a job on the paused test printer, followed with an hour between polls, is seen cancelled at once. All pass here.
- **Package validation passes** against 11.0.6-preview: the interface members have default implementations, and the old extension is kept.
- **Not done:**
  - IPP notifications (`ippget`), until a printer or server that waits for events is at hand;
  - printers appearing and going (D10).

## Amendment, 2026-11-20

From a review of Phase 4's changes (owner: "Fix all of them"):

- **A printer watch goes on through failures:**
  - a state that can't be read, as when the printer is off or the network drops, is reported as `PrinterState.Unknown`, with why as its message;
  - the watch goes on, and reports the printer's state again once it can be read;
  - before, one failed read ended a watch meant to run until cancelled;
  - a job watch still ends on a failure, as a job's watch ends anyway.
- **A notification that can't be reset gives way to polling.** If `FindNextPrinterChangeNotification` fails, as when a print server's connection is lost, the handle would stay signalled and the watch would read the status back to back. The notification is now closed, and the status polled at the interval given.
