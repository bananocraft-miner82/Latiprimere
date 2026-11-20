# 0042: IPPS for Windows queues, and what printers say they ask for

- Status: Accepted (owner, 2026-11-20: "Accept ADR 0042")
- Date: 2026-11-20

## Context

[ADR 0037](0037-ipps-and-authentication.md) gave direct IPP pinned certificates, a callback and passwords, and left three things out.

**The Windows route trusted only the system.** Windows queues whose PDFs go straight to their IPP printer ([ADR 0027](0027-direct-ipp-for-windows-queues.md), [ADR 0036](0036-wsd-queues-over-ipp.md)) built their client with no trust settings and no credentials. So:

- an `ipps://` address in `WindowsOptions.IppPrinterUris` with the self-signed certificate printers nearly always have was refused at its first question;
- the job then went through the driver without a word, and nothing said which certificate to trust;
- a printer that asked for a password could never be sent one;
- a WSD queue's printer was always reached over plain IPP, even when it announced IPPS too.

**`uri-authentication-supported` wasn't read.** Printers say, for each address in `printer-uri-supported`, what they ask for there: `none`, `requesting-user-name`, `basic`, `digest`, `certificate` or `negotiate`. Captured printers show what to expect:

- the HP ENVY says `none` on both its addresses;
- CUPS queues say `requesting-user-name`;
- the scripted printer says `basic` when it asks for a password.

Most printers that ask for a password answer questions freely, Get-Printer-Attributes among them, and ask only to print. For the Windows route that meant this:

- the printer was matched, and the queue's jobs went to it;
- Validate-Job was then refused with HTTP 401;
- `PrintAuthenticationException` reached the caller, instead of the driver printing the job. The harness's `windows.ipp-route.fallback-password` didn't show it, as the scripted printer asked for its password for every request.

Items 2 and 3 of the review of what's left before 1.0. Owner: "Start items 2 and 3".

## Decision

### Trust and passwords for the Windows route

- **Four settings on `WindowsOptions`,** named after the existing `IppPrinterUris` and working as their `IppNetworkOptions` counterparts do:
  - `IppTrustedCertificateFingerprints`
  - `IppServerCertificateValidationCallback`
  - `IppCredentials`
  - `IppPrinterCredentials`

  The alternative, a whole `IppNetworkOptions` on `WindowsOptions`, would have carried a dozen settings the route doesn't use, such as discovery, `PrinterUris`, `AddressStorePath` and a second `MaxRenderDpi`. Which ones applied would have been unclear.
- **The same rules as direct IPP,** built by the same code, now shared (`IppCertificateTrust.CreateForPrinters`, `IppConnectionSecurity.CreateForPrinters`):
  - credentials go only over TLS;
  - with `IppCredentials` set, the system's trust is enough only for hosts in `IppPrinterUris` or `IppPrinterCredentials`. A WSD queue's printer must be pinned, as anyone on the network could announce a printer with the queue's UUID.
- **Checked when the print system is created,** or registered, with `PrintPdfOverIpp` on. A bad fingerprint, user name or printer credentials key throws `ArgumentException`, naming `WindowsOptions`.
- **IPPS first for a WSD queue's printer** that announces both addresses, once trust beyond the system's is configured (decision D4 of the Phase 3 plan, as for direct IPP). If the encrypted connection can't be opened, its plain IPP address is used.
- **Nothing is silent any more:**
  - a refused certificate is logged as a **Warning** with its fingerprint, naming `WindowsOptions.IppTrustedCertificateFingerprints`, since that's the one thing only the application can fix;
  - a printer that can't be reached is logged at Information.

  The job still goes through the driver, as before.
- **An encrypted connection that fails at Print-Job,** as when the printer's certificate changed after it was matched, sends the job through the driver, as a failure to connect does. Nothing was sent.

### What printers say they ask for

- **Read with the attributes already asked for:** `printer-uri-supported`, `uri-security-supported` and `uri-authentication-supported`, by direct IPP's lookups and the route's first question.
- **Matched by scheme, port and path, not host:** printers name themselves, by a `.local` name or `localhost`, not by the address they're reached at. An address without a port has IPP's 631, and a trailing slash or a difference of case doesn't count. With no match, nothing is assumed.
- **The Windows route sends a queue's jobs through the driver,** without Validate-Job or Print-Job, when the printer says it asks for either of these:
  - a password it won't be sent: `basic`, with none for that address, or over plain IPP;
  - authentication Latiprimere can't give: `digest`, `certificate`, `negotiate` or a keyword it doesn't know.

  It's logged at Information, and not asked again while the printer object lives. The driver always prints, so a printer that misreports costs nothing worse than today.
- **Direct IPP doesn't refuse early.** A printer that says `basic` but prints without a password would otherwise stop printing. The cost of asking is already small: Validate-Job comes before the upload ([ADR 0033](0033-validate-job.md)). Instead, when the printer does refuse for authentication, the `PrintAuthenticationException` keeps its message and status and adds what the printer said:
  - **for a printer reached over plain IPP that lists an `ipps://` address,** that address, at the host it was reached by, to print to instead;
  - **for authentication Latiprimere can't give,** that it sends a user name and password with HTTP Basic only.

## Consequences

- **A public addition:** the four `WindowsOptions` settings. They do nothing until set, and nothing changes for applications that set nothing.
- **A WSD queue in front of a printer that asks for a password to print** now prints through its driver, rather than failing with `PrintAuthenticationException`.
- **Refusals by the printer are clearer** for direct IPP, which now says where its encrypted address is.
- **Not done:**
  - **Sending a shared password only to printers that ask for one.** `Credentials` still goes to every printer reached over trusted TLS. Withholding it from printers that say `none` would expose it less, but a printer that misreports would then be refused, with no second attempt: credentials are sent up front, not in answer to a challenge (ADR 0020).
  - **The Windows route's other direct IPP settings,** such as `ConnectTimeout` and `UserName`, stay at their defaults.
- **Tests:**
  - unit tests of the reader, the hint on refusal, the route's IPPS first with fallback, its authentication checks, logging and the TLS fallback at Print-Job, and the factory's checks;
  - harness scenarios through the public API, on the scripted printer:
    - `windows.ipp-route.password-over-ipps`: an `ipps://` printer with a self-signed certificate and a password, pinned and configured in `WindowsOptions`. It runs wherever Microsoft Print to PDF is installed;
    - `windows.ipp-route.fallback-password-for-jobs`: a printer that answers questions freely and asks for a password only to print. It needs the Windows test printers, as the other fallback scenarios do. The scripted printer gained `FakePrinterOptions.RequiresCredentialsForJobsOnly` for it.
- **Not checked on a real printer:** the HP asks for no password, and its WSD queue isn't on this PC.
