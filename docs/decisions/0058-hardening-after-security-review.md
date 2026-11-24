# 0058: Hardening after an outside security review

- Status: Accepted
- Date: 2026-11-24

## Context

An outside, documentation-level security review of the repository flagged printer impersonation over mDNS, the fallback from IPPS to plain IPP, server-side request forgery, PDFium as native code parsing untrusted PDFs, and a SharpIppNext CVE. Checking each against the code found:

- **The fallback from IPPS to plain IPP could be forced.** A printer found on the network that announces both addresses is reached over plain IPP whenever its IPPS address fails, because its certificate isn't trusted or nothing answers there ([ADR 0037](0037-ipps-and-authentication.md), decision D4). That held even for a printer reached over IPPS before. So a device on the network could block a pinned printer's IPPS port, or announce its name with only a plain address, and receive its documents unencrypted. The fallback was logged only at Information.
- **The UUID check could be dodged.** A remembered address is used only while the printer there reports the UUID it announced, but one that reported no UUID at all was taken on trust.
- **Announced addresses weren't checked.** An announcement could name `127.0.0.1`, a cloud metadata service at `169.254.169.254`, or `localhost`, and the printer was then contacted there. It also became a known printer, so its `ipp:` URI identifier passed the check that keeps identifiers from outside the application from reaching internal services. A `.local` name could resolve to the same, as anyone on the network can answer for one.
- **A pinned certificate was trusted at any address.** Printers of one model sometimes share a certificate made at the factory, so pinning one printer's certificate trusted every device that presents it.
- **PDFium's limits couldn't be set.** A PDF of up to 2 GB was always accepted, and a page could render for as long as its author liked while holding the process-wide PDFium lock, so one PDF could stop every other from rendering.
- **Native libraries were found by the default search,** which on Windows includes the current directory and the PATH. `winspool.drv` and `prntvpt.dll` aren't KnownDLLs.
- **The workflows used actions by tag,** which their owners can move, including `release.yml`, which logs in to NuGet to publish.

Checked and fine as they were:

- **Printers' answers are already bounded.** SharpIppNext stops an endless answer within a few megabytes, and [SharpIppResponseLimitTests](../../tests/Latiprimere.Ipp.Tests/SharpIppResponseLimitTests.cs) keeps that tested (see progress.md, 2026-11-21).
- **The CVE** the review named couldn't be found in the advisory database NuGet uses. It was said to affect SharpIppNext before 4.1.0, on the server side, and Latiprimere uses 5.0.3 as a client. `dotnet list package --vulnerable --include-transitive` reports nothing for any project.
- **Redirects, CUPS queue names and Windows printer names** were already guarded: HTTP redirects aren't followed, queue names are escaped and `..` refused, and a `\\server\printer` name is opened only if Windows lists it.

## Decision

Owner, 2026-11-24: "Fix all findings, starting at the top and working down".

**1. No forced fall back to plain IPP:**
- **`IppNetworkOptions.RequireEncryption`,** off by default. When on, a printer found on the network is reached only at its IPPS address. One that announces none, or whose certificate isn't trusted, is refused with a `PrintException`. An `ipp:ipp://` lookup is refused without connecting, and an `ipp://` URI in `PrinterUris` throws `ArgumentException` when the print system is created. `IppConnectionHandler` refuses any request that would still go over plain HTTP, as a last check. It also makes IPPS addresses preferred when only the system's trust is configured.
- **Once over IPPS, always over IPPS.** Without `RequireEncryption`, a printer found on the network that has been reached over IPPS with a trusted certificate is never reached over plain IPP again under that name, while a certificate is pinned or the callback is set. That holds even if it is later announced with only a plain address. It is remembered in the address store as `reachedOverIpps`, an optional property within format 1, as `lastSeen` was. A printer that has never been reached over IPPS still falls back, so pinning one printer doesn't break the others.
- **The fallback is logged as a warning,** naming `RequireEncryption`.
- **A printer that announced a UUID must report it** at its remembered address. One that reports none is searched for instead.

**Configurable** (owner, 2026-11-24: "Can you make each of the decision options configurable for the package end users where possible"). Each defaults to the safer choice:

| Choice | Default | Setting |
|---|---|---|
| Keep every request to IPPS | Off | `IppNetworkOptions.RequireEncryption` |
| A printer reached over IPPS may fall back to plain IPP | Not allowed | `IppNetworkOptions.AllowPlainIppAfterIpps` |
| A remembered address may report no UUID | Not allowed | `IppNetworkOptions.AllowRememberedAddressWithoutUuid` |
| The longest PDF opened | 2,147,483,647 bytes | `PdfiumOptions.MaxDocumentBytes` |
| The longest a band renders for | 2 minutes, or infinite | `PdfiumOptions.RenderTimeout` |
| Certificates pinned at one address | None | `PrinterCertificateFingerprints` |

`AllowPlainIppAfterIpps` has no effect while `RequireEncryption` is set. Whether a printer was reached over IPPS is remembered either way, so turning the allowance off again protects it at once.

Left fixed, as switches would only weaken them:
- **The address rules of decision 2:** an address no printer on the network has is never one an application needs to reach by discovery. A printer on such an address, as through ipp-usb, is configured by URI, which the rules don't apply to.
- **The native library search paths and the pinned actions** aren't runtime choices.

**2. Addresses a printer may have** (`PrinterAddressRules`):
- **Refused:** loopback (`127.0.0.0/8`, `::1`), unspecified and "this network" (`0.0.0.0/8`, `::`), multicast, `240.0.0.0/4` with broadcast, and the cloud metadata services at `169.254.169.254` and `fd00:ec2::254`, IPv4-mapped forms included. Link-local addresses are allowed, as printers without DHCP use them.
- **Announcements:** such addresses are skipped. An announcement that gives only such addresses is skipped entirely, rather than falling back to its host name, as is one naming `localhost`.
- **`.local` names:** their resolved addresses are held to the same rules when connecting, whether the name came from an announcement or from the application.
- **The address store isn't filtered.** It is the application's own file, and announcements are held to the rules before they reach it. Filtering it as it loaded, as first done, broke the end-to-end harness's `ipp.discovery.remembered-address`: Native AOT check, 2026-11-24. That scenario seeds the store with a printer on `127.0.0.1`, as an application may. What's left is that a store written by 11.0.9 or 11.0.10 could still hold such an address from an announcement. The printer there is no longer announced, so it is forgotten within 90 days (ADR 0038).
- **Not changed:** an `ipp:dnssd:` lookup still searches the network when `EnableDiscovery` is off, as documented. With the rules above, what it can reach is a printer's address on the local network, which anyone able to announce one can reach anyway.
- **Considered:** IPP over USB on Linux (ipp-usb) announces printers on `localhost`, but through Avahi's local registration, which Latiprimere's own mDNS listener doesn't see. To use one, configure its `ipp://localhost:60000/ipp/print` URI in `PrinterUris`, which the rules don't apply to.

**3. Response size:** no change. SharpIppNext's limits already bound it (see Context).

**4. PDFium** (`PdfiumOptions`, passed to `new PdfiumRasterizer(options)` or `AddLatiprimerePdfium(options => ...)`):
- **`MaxDocumentBytes`,** by default 2,147,483,647 bytes as before.
- **`RenderTimeout`,** by default 2 minutes per band, or `Timeout.InfiniteTimeSpan`. Bands render with `FPDF_RenderPageBitmap_Start`, whose pause callback returns true once the deadline has passed. A paused band is closed with `FPDF_RenderPage_Close` and refused with a `PrintException` naming the page and the setting. The lock is held throughout, as before, so nothing else can touch the page mid-render.
- **Limitation:** PDFium checks the pause between steps of drawing, about every 100 page objects and while decoding images, not while reading a page's content when it is loaded. One huge step can overrun the limit.
- **Not done:** rendering in a separate process, which would contain a crash in PDFium. That is a larger piece of work, for later.

**5. Certificates pinned at one printer's address:** `IppNetworkOptions.PrinterCertificateFingerprints` and `WindowsOptions.IppPrinterCertificateFingerprints`, by printer URI, matched by host. A certificate pinned there is refused at any other host. The global `TrustedCertificateFingerprints` are unchanged.

**6. Supply chain and loading:**
- **Native library search:**
  - `Latiprimere.Windows` searches only System32 for its native libraries (`DefaultDllImportSearchPaths`);
  - `Latiprimere.Rendering.Pdfium` searches the application's native libraries and its own folder, then Windows' safe directories, never the current directory or the PATH.
- **Workflows:** every action is pinned to a commit SHA, with its version in a comment, which Dependabot keeps up to date.

## Consequences

- Sensitive documents can be kept to IPPS with one setting, and a printer that has been reached securely can't be downgraded by a device on the network.
- A printer that changes its certificate, or turns its IPPS off, after being reached over IPPS is refused until its new certificate is pinned, or its IPPS is back. The error says why.
- A printer found on the network can't point Latiprimere at the machine it runs on, or at a cloud metadata service.
- One PDF can hold up rendering for at most `RenderTimeout`, by default 2 minutes, rather than without end.
- **Windows queues are unchanged:** their PDF jobs over IPP still fall back to the spooler, which may send them unencrypted (WSD). `RequireEncryption` isn't offered there, as the spooler path is outside Latiprimere's control.
- **New public API,** in PublicAPI.Unshipped.txt:
  - `IppNetworkOptions.RequireEncryption`, `AllowPlainIppAfterIpps`, `AllowRememberedAddressWithoutUuid` and `PrinterCertificateFingerprints`;
  - `WindowsOptions.IppPrinterCertificateFingerprints`;
  - `PdfiumOptions`, `PdfiumRasterizer(PdfiumOptions)` and `AddLatiprimerePdfium(Action<PdfiumOptions>)`.
