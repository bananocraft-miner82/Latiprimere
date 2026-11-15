# 0010: Direct IPP to network printers

- Status: Accepted
- Date: 2026-11-11

## Context

Spike 4 adds printing straight to IPP printers on the network, without CUPS or the Windows spooler. It needs a way to identify such printers, a way to find them on the local network, and a decision on how much to check before sending a job. In the spike, direct IPP printers behaved differently from CUPS in one important way: CUPS accepts a job with options it can't honour and cancels it later, while a printer rejects it up front with `client-error-attributes-or-values-not-supported` (status 1035).

## Decision

- **Where it lives:** an `IppNetworkPrintSystem` in `Latiprimere.Ipp`, next to the IPP layer the CUPS adapter already uses (ADR 0006). Register it with `AddLatiprimereIpp()`, or create it with `IppNetworkPrintSystemFactory.Create()`. Options (`IppNetworkOptions`): discovery on or off, discovery duration (default 2 s), printer URIs to list even if they don't announce themselves, and the user name sent with requests.
- **Printer IDs take two forms** (owner decision 2026-11-11, option (c) in [plan.md](../plan.md)):
  - **Discovered printers are identified by their announced name**, for example `ipp:dnssd:Office Laser`. The ID survives the printer getting a new address.
  - **Looking up a name tries the known address first, then searches by name** (owner decision at acceptance, 2026-11-11):
    1. The print system remembers the address and announced UUID last seen for each name, from listings and earlier lookups.
    2. A lookup tries that address first, allowing it 3 s. If the printer reports a `printer-uuid` and announced a `UUID`, they must match, so a different printer that took the old address isn't used.
    3. If nothing is remembered, or the address doesn't answer as the same printer, the lookup searches the network for the name (case-insensitive, as DNS is) and remembers the address it finds. The search stops as soon as the printer answers over IPP, and gives up after the discovery duration. A printer that answers only over IPPS is used once that time has passed.
    4. Addresses are remembered in memory only, so the first lookup after a restart searches.
  - **Configured printers, and any IPP printer, are identified by URI**, for example `ipp:ipp://192.168.1.20:631/ipp/print`. They are looked up without discovery. A configured printer that also announces itself is listed once, under its URI ID, with its announced name and details.
  - IDs for other print systems, URIs that aren't `ipp:` or `ipps:`, and `ipp:dnssd:` with no name are not ours; a lookup returns `null` without any network traffic.
  - A printer found by name keeps that name in its details, rather than the `printer-name` it reports, so the name matches its ID.
- **Discovery uses DNS-SD over mDNS with [Tmds.MDns](https://github.com/tmds/Tmds.MDns)** (MIT, managed, no dependencies). It works the same on Windows, Linux and macOS, with no need for Bonjour, Avahi or three native bindings. It browses `_ipp._tcp` and `_ipps._tcp` for the configured duration each time printers are listed.
- **Mapping an announcement to a printer:**
  - **Host:** an IPv4 address if one was announced, otherwise the host name. IPv6 link-local addresses are avoided because their scope ID only means something on this machine.
  - **TXT keys:**
    - `rp` gives the resource path (default `ipp/print`).
    - `ty` gives make and model.
    - `note` gives location.
    - `pdl` gives document formats.
    - `UUID` gives the printer's UUID, compared with its `printer-uuid` when its remembered address is tried.
  - Keys are case-insensitive, and the first occurrence of a key wins.
  - Announcements without a host or port are dropped.
- **De-duplication:** a printer announced as both IPP and IPPS is listed once, as IPP; IPPS needs certificate handling that comes in Phase 3. Renamed duplicates of the same printer, such as "Office (2)", are dropped by URI, keeping the shortest name.
- **Listing doesn't contact printers.** Discovered printers are described from their announcement; configured URIs that weren't discovered are named after their host. The state is `Unknown`, so a printer that is switched off still appears. Looking a printer up sends Get-Printer-Attributes, so it reads the current state and the supported document formats.
- **Unsupported formats are rejected before sending.** A printer from a lookup knows its `document-format-supported`. Submitting a format outside that list throws `NotSupportedException`, naming the formats the printer accepts. CUPS queues skip this check because CUPS converts most formats itself.
- **Unsupported options are not pre-checked yet.** The printer's 1035 rejection reaches the caller as a `PrintException` with the IPP status as `NativeErrorCode`. Checking options against capabilities waits for the capability model (Spike 5).
- **No default printer.** A network has no default, so `FindDefaultPrinterAsync` returns `null`.
- **`PrintOptions.OutputFile` is refused** with `NotSupportedException`, as for CUPS (ADR 0009).

Implementation notes from the spike:

- `ServiceBrowser.StopBrowse()` raises `ServiceRemoved` for every known service. It also marks the announcement objects as removed. So the browser's event handlers are detached before stopping, and each announcement is copied into an immutable `DnsSdService` when its event arrives.
- Shared IPP helpers (`IppPrinterQueries`) serve both the CUPS and the network print systems.

## Consequences

- Direct printing works with no configuration on a network with announcing printers. A printer that doesn't announce, or one on another subnet, can be added by URI.
- Listing takes at least the discovery duration (2 s by default), because mDNS has no "all answers received" signal. Applications that list often can turn discovery off, or shorten it. Turning discovery off affects listing only; name IDs are still looked up on the network.
- **IDs of discovered printers survive address changes.** A lookup by name usually costs one request to the remembered address. A search is needed only after a restart or a move; measured on Windows, it takes 0.18 s for a printer that answers.
- **A printer that has gone costs the most:** up to 3 s at the remembered address, then the full discovery duration (2 s by default) searching, before the lookup reports not found.
- **Printers without UUIDs are taken on trust** at their remembered address. If one of them moves and another printer takes its address, the other printer would be used until the next search. IPP Everywhere printers report and announce UUIDs, so this mainly affects older printers.
- **A renamed printer gets a new ID.** That includes a printer that DNS-SD renames after a name clash, such as "Office (2)". Applications that need an ID that never changes can configure the printer by URI and give it a fixed address.
- **Names may contain spaces and punctuation**, so command lines need to quote them.
- Raw jobs depend on the printer. `ippeveprinter` lists `application/octet-stream`, but only to mean "detect the format", so it rejects data it doesn't recognise. Whether raw printing to a network printer works has to be found printer by printer.
- Printers that accept neither PDF nor raw data, only PWG raster or Apple raster, can't be printed to yet. Rendering PDF to PWG raster with PDFium is a possible later addition.
- New dependency: Tmds.MDns 0.10.1. It is small, but still pre-1.0; keep it behind `IIppPrinterDiscovery` so it can be replaced.
