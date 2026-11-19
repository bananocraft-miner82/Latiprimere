# 0037: IPPS and authentication for direct IPP

- Status: Accepted
- Date: 2026-11-18

## Context

Direct IPP could reach a printer's `ipps://` address, but only through the system's trust. Printers nearly always present a certificate they made themselves, which no computer trusts, so in practice IPPS was refused, with a message written for CUPS servers. A printer that asks for a password was refused too: direct IPP had no way to send one ([ADR 0020](0020-cups-authentication.md) covered CUPS only). Printers found on the network were always reached over plain IPP, even when they also announced `_ipps._tcp`.

[Phase 3's plan](../phase-3-plan.md), P3-5, with decision D4 (prefer IPPS once its certificate is trusted).

## Decision

- **Certificates,** checked in this order (`IppCertificateTrust`):
  1. **The system's trust:** a certificate with no TLS errors is accepted, as before. **While `Credentials` is set,** this holds only for hosts the application named, in `PrinterUris` or `PrinterCredentials`. Anyone on the network can announce a printer at a host whose certificate a public authority issued, which the system trusts, and it would otherwise receive the shared password. Other hosts' certificates go on to the next two checks, and a refusal says the system trusts the certificate but it must be pinned.
  2. **Pinned certificates:** one whose SHA-256 fingerprint is in the new `IppNetworkOptions.TrustedCertificateFingerprints` is accepted whatever name it carries, since printers' certificates rarely name the address used. Fingerprints are hexadecimal, with or without colons, spaces or dashes, in either case.
  3. **The application's check:** the new `IppNetworkOptions.ServerCertificateValidationCallback`, as `CupsOptions` has.

  Anything else is refused.
- **Refusals say what to pin:** the check remembers each refused certificate's fingerprint by host, from the TLS stream .NET passes it. The `PrintException` then gives the printer's URI and the fingerprint, as colon-separated pairs, and says to compare it with the printer's own web page before adding it.
- **Credentials:**
  - **Configuration:** the new `IppNetworkOptions.Credentials` is sent to every printer; the new `PrinterCredentials`, keyed by printer URI and matched by host and port, is sent to one printer only.
  - **Sending:** they go with every request as HTTP Basic, as for CUPS, and **only over TLS, never in clear text** (`IppConnectionSecurity.SendsCredentialsOnlyOverTls`).
  - **Over plain IPP:** a printer that asks for a password there is refused with a `PrintAuthenticationException` saying to print to its `ipps://` address. Without configured credentials, the existing "needs a user name and password" message stands.
  - **Checks:** user names without a colon, and `PrinterCredentials` keys that are absolute `ipp` or `ipps` URIs, are checked when the print system is created, as are fingerprints. Anything else throws `ArgumentException`.
- **IPPS addresses for printers found on the network (D4):**
  - **Both addresses kept:** discovery keeps a printer's IPPS address as well (`DiscoveredIppPrinter.SecureUri`) when it announces both. The IPPS announcement must come from the same host as the IPP one, and not with another UUID: names are unique only within a service type, so anyone could announce IPPS under another printer's name.
  - **When IPPS is tried first:** when the application trusts more than the system does (a pinned fingerprint or a callback), both a remembered address and a fresh lookup try IPPS first.
  - **The fallback:** a refused handshake, or an IPPS address where nothing answers (refused, unresolved or timed out), falls back to plain IPP, logged at Information. Nothing was sent then; a refusal by the printer itself is the answer, and isn't retried over plain IPP.
  - **No trust configured:** printers are reached over plain IPP as before, so nothing changes for applications that configure nothing.
- **Configured `ipps://` URIs** in `PrinterUris` use the same trust and credentials.
- **Not done:**
  - **Trust on first use,** offered by the plan as an opt-in: it needs somewhere to keep the pins, which P3-6's address store can provide.
  - **`uri-authentication-supported`** isn't read: credentials go to every printer reached over trusted TLS, or to one printer when given in `PrinterCredentials`.
  - **The Windows route** to IPP printers (ADR 0027) still uses the system's trust only.

## Consequences

- **Public API:** `IppNetworkOptions.TrustedCertificateFingerprints`, `ServerCertificateValidationCallback`, `Credentials` and `PrinterCredentials`. All are additive, and do nothing until set.
- **Who receives a shared password:** `Credentials` is sent to every printer reached over IPPS once its certificate is trusted: by a pin or the callback, or by the system for a configured printer. A printer found on the network with a publicly issued certificate must be pinned too, which is rare for printers and the price of not sending the password to whoever announces one. An application that pins several printers but has a password for one should use `PrinterCredentials`. The documentation says so.
- **Tests:**
  - **Unit tests:** fingerprints, the order of checks, credentials only over HTTPS with per-printer overrides, discovery keeping both addresses, IPPS first with fallback, and the factory's checks.
  - **Harness scenarios on the scripted printer, over real TLS:**
    - `ipp.security.untrusted-certificate` now checks the message gives the fingerprint;
    - `ipp.security.pinned-certificate` prints;
    - `ipp.security.password-over-ipps` sends credentials with every request;
    - `ipp.security.password-not-sent-in-clear` never sends them over plain IPP.
  - **Integration test against `ippeveprinter`:** IPPS on its own port, refused with the fingerprint, then pinned, then printed. It skips, saying why, if that `ippeveprinter` doesn't offer TLS. It passed here against the scripted printer; CI runs it against the container.
- **Still to check:** the HP over `ipps://` with its certificate pinned (D9).
