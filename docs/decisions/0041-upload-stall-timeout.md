# 0041: A time limit on a printer that stops taking a job

- Status: Accepted (owner, 2026-11-20: "Keep 5 minutes")
- Date: 2026-11-20

## Context

The bug and performance sweep of 2026-11-19 left this open: a Print-Job upload had no time limit if a printer stalled, unless the caller passed a cancellation token.

- **The HTTP client has no overall timeout** ([ADR 0010](0010-direct-ipp-adapter-design.md)). HttpClient's default of 100 seconds would also cut short a large job to a printer that reads it only as fast as it prints.
- **Every request other than Print-Job is limited** to 100 seconds by `SharpIppClientAdapter`. Print-Job is left out for the same reason.
- **Opening the connection is limited** by `ConnectTimeout`.

So a printer that accepts the connection and then stops reading held `SubmitAsync` for as long as the connection stayed open: one that jams, sleeps, crashes or loses the network without closing it. So did one that read the whole job and never answered. A web request or background service waiting on it waited forever, and the busy retries ([ADR 0017](0017-retry-busy-ipp-printers.md)) never started.

Raster printing ([ADR 0035](0035-printing-pdfs-as-raster.md)) makes an overall limit even less suitable. Pages are rendered as they are sent, so a long job's upload takes as long as its rendering.

## Decision

- **A stall timeout, not an overall one.** A Print-Job fails when either of these takes longer than `IppNetworkOptions.UploadStallTimeout`:
  - **one write of its body:** the printer has stopped reading what it was sent;
  - **the wait for the answer once the whole body is sent.**

  The time between writes isn't counted, so rendering pages is never mistaken for a stall.
- **5 minutes by default,** `IppNetworkOptions.DefaultUploadStallTimeout`. That covers a printer that pauses before it starts, to warm up or clean its heads, and one that pauses between pages, as an inkjet printing both sides does while the ink dries. `Timeout.InfiniteTimeSpan` turns it off. 2 minutes would also have covered the HP ENVY's pauses that we know of; 5 leaves room for printers we haven't seen (owner).
- **How:**
  - an `IppStallGuardHandler` wraps the request's body in a `StallGuardedContent`, whose stream times each write and flush with an `IppStallWatch`;
  - the timer starts again after the body's final flush, for the answer;
  - when it runs out, the request is cancelled, and whatever the cancelled connection throws is replaced by an internal `IppTransferStalledException`;
  - `SharpIppClientAdapter` turns that into a `PrintException`, whose message says which of the two happened.
- **Large writes are split into 64 KiB pieces.** SharpIppNext writes a seekable document, such as a `MemoryStream` or a file, to the connection in a single write. Windows reports a single large write to a socket as finished once it has queued it, even while the other end reads nothing: in the first run of the harness scenario, 64 MB was "sent" in 24 ms to a printer that had stopped reading. That would have reported a stalled upload as a missing answer. Worse, it would have cut off a slow printer still reading a large PDF 5 minutes after it started. In pieces, each finished write means the network took it, and only the last few hundred kilobytes in the network's buffers can be counted as waiting for the answer.
- **The stall isn't a connect failure.** The `PrintException`'s inner exception is the `IppTransferStalledException`, which `IppConnectionErrors.IsConnectFailure` doesn't match. So a Windows queue routed over IPP ([ADR 0027](0027-direct-ipp-for-windows-queues.md)) doesn't fall back to the spooler and print a job twice that may have partly printed. Nor is it resent as busy: some of it may be on paper.
- **Where it applies:**
  - direct IPP, with the option;
  - Windows queues routed over IPP, with the default, as they take the other direct IPP defaults;
  - **not CUPS,** whose scheduler spools the whole job before printing it, so it doesn't stall as a printer does. `CupsOptions` has no such option, and behaves as before.

## Consequences

- **A stalled printer fails the job within the stall timeout,** with a `PrintException` that says the printer stopped taking the document, or was sent it all and didn't answer, so it's unknown whether it will print.
- **No change for a printer that keeps reading,** however slowly, or for the time spent rendering.
- **A public addition:** `IppNetworkOptions.UploadStallTimeout` and `DefaultUploadStallTimeout`. Under the rules for after 1.0 (CONTRIBUTING.md, "Changing the Public API"), a later default could still be changed only as a behaviour change. Hence settling it before 1.0.
- **Tests:**
  - unit tests of the watch, the stream's pieces and the handler, with fakes that stop reading, never answer, or write slowly;
  - two over real TCP through `SharpIppClientAdapter`: a printer that never reads, with a seekable and a forward-only document, and one that never answers;
  - the harness scenario `ipp.faults.stalled-upload`, through the public API, against the scripted printer, which gained `FakePrinterBehaviour.StallPrintJobUpload`.
- **Not checked on a real printer.** Making the HP stall on purpose isn't practical; the scripted printer stands in.
