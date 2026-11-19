# 0038: Remembered printer addresses that survive a restart

- Status: Accepted
- Date: 2026-11-18

## Context

A printer found on the network is identified by its announced name, such as `ipp:dnssd:Office Laser`. A lookup tries the address last seen for that name, checking the printer there still reports the UUID it announced, then searches the network. That memory lived only as long as the print system did. A printer that stops answering mDNS while asleep, as the HP ENVY did on 2026-11-12, couldn't be found after the application restarted, though it would have answered at its address.

[Phase 3's plan](../phase-3-plan.md), P3-6, with decision D6 (opt-in, with a file path).

## Decision

- **Opt-in:** the new `IppNetworkOptions.AddressStorePath` names a file. A library shouldn't write files unless asked (D6).
- **Reading:** the file is read when the print system is created, and its printers become the remembered ones, as if just found.
  - A missing file means starting empty, logged at Debug.
  - An unreadable one also means starting empty, logged as a Warning.
  - Entries that make no sense are skipped: no name, an address that isn't an absolute `ipp` or `ipps` URI, or a bad IPPS address. So are `null` entries.
  - A file whose `version` is later than this version reads starts empty, logged as a Warning, and is never written, so an older application sharing it doesn't replace it with less. A file without a `version` is read as version 1.
  - Reading never throws.
- **Writing:**
  - **When:** whenever a listing or a lookup finds a printer at a new address, a new IPPS address, or with a new UUID. A listing that changes nothing writes nothing, and one that changes several writes once.
  - **How:** to a temporary file beside it, which then replaces it, so a reader never sees half a file. The folder is created if needed.
  - **Failures:** a failed write is logged as a Warning, and the next change tries again.
  - **Concurrency:** writes from one print system are serialised. Two processes sharing a file is last-writer-wins, which is harmless, since every entry is checked when used.
- **Format:** JSON, through a source-generated `System.Text.Json` context, so Native AOT and trimming still work:

  ```json
  {
    "version": 1,
    "printers": [
      { "name": "Office Laser", "printerUri": "ipp://192.168.1.20:631/ipp/print",
        "secureUri": "ipps://192.168.1.20:443/ipp/print", "uuid": "1c852a4d-…",
        "makeAndModel": "HP ENVY 5640 series", "location": "Floor 2" }
    ]
  }
  ```
- **Checked on every use, as before:**
  - **The UUID:** a remembered address is used only while the printer there reports the UUID it announced, so a reused address doesn't reach the wrong printer.
  - **A printer without a UUID** is taken on trust, as with remembered addresses in memory.
  - **IPPS first:** a remembered IPPS address is tried first when IPPS is preferred ([ADR 0037](0037-ipps-and-authentication.md)).
- **Remembered addresses count as found on the network,** so they can also be looked up by their `ipp:` URI identifier ([ADR 0024](0024-known-ipp-printer-uris.md)). The option's documentation says to keep the file where only the application can write.
- **Listing doesn't change:** it shows printers found now and those configured, not every printer ever remembered.
- **Entries are never removed:** a printer gone for good leaves one stale entry, which costs a lookup by its name the remembered-address timeout (three seconds) before the search.

## Consequences

- **The plan's item is met:** with `AddressStorePath` set, a printer that sleeps through mDNS is found after a restart.
- **Public API:** `IppNetworkOptions.AddressStorePath`. It's additive, and off by default.
- **Trust on first use** ([ADR 0037](0037-ipps-and-authentication.md)) could keep its pins in the same file. That's not done: it's an opt-in no one has asked for yet.
- **Testing:**
  - **Unit tests:** reading back what was written, a missing file, three unreadable ones, entries that make no sense, and a path that can't be written.
  - **A restart:** a printer listed by one print system is found by the next one without a search, and only while the printer at the address reports the remembered UUID.
  - **Harness, through the public API:** `ipp.discovery.remembered-address` writes the store as an earlier run would, then prints to a printer that announces nothing; with another UUID remembered, the printer isn't used. It also runs in CI's Native AOT check, which exercises the source-generated JSON natively.
