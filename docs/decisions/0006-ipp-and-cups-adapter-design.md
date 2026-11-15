# 0006: IPP layer and CUPS adapter design

- Status: Accepted
- Date: 2026-11-10

## Context

Spike 1 built the CUPS adapter on managed IPP (SharpIppNext 5.0.2) rather than P/Invoke to libcups. Several design choices came out of it that later adapters, especially the direct IPP adapter in Spike 4, will build on.

## Decision

- **Managed IPP, no libcups.** The CUPS adapter talks IPP to the scheduler. This avoids libcups 2/3 binding differences and works identically on Linux and macOS.
- **Shared IPP layer.** `Latiprimere.Ipp` holds the IPP printer, job, mappers and transport. SharpIppNext types appear only in `internal` code, behind the internal `IIppClient` interface, so the library can be replaced. `Latiprimere.Cups` reaches these internals through `InternalsVisibleTo`; the two packages must therefore ship together with matching versions.
- **Finding the scheduler,** in libcups order: `CupsOptions.Server`, then `CUPS_SERVER`, then the local socket (`/run/cups/cups.sock` on Linux, `/private/var/run/cupsd` on macOS), then `localhost:631`. A server set explicitly also makes the adapter usable on Windows, for CUPS print servers.
- **Unix socket transport:** a `SocketsHttpHandler.ConnectCallback` that connects to the socket; HTTP and IPP run over it unchanged.
- **Printer identifiers** are the queue name prefixed with `cups:` (for example `cups:Office`), so they never clash with other print systems.
- **Printer URIs** for jobs are built from the endpoint (`ipp://<server>/printers/<queue>`), not taken from `printer-uri-supported`, which can name a host the client cannot reach.
- **Default printer** comes from CUPS-Get-Default, sent as a raw IPP message because SharpIppNext has no typed request for it. Filtering CUPS-Get-Printers on the `printer-type` default bit does not work: CUPS adds that bit only when reporting attributes.
- **Raw data** is sent as `application/vnd.cups-raw` to CUPS, and as `application/octet-stream` to IPP printers.
- **Job template attributes** left at their defaults are not sent, so printers apply their own defaults.

## Consequences

- No native dependency on Linux or macOS. A trimmed self-contained Linux build produced no trimming warnings.
- `Latiprimere.Ipp` and `Latiprimere.Cups` are versioned together.
- CUPS accepts jobs with options the printer cannot honour and cancels them later (see progress log, 2026-11-10). Option validation against printer capabilities belongs in Spike 5.
