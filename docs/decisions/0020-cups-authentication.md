# 0020: CUPS authentication

- Status: Accepted
- Date: 2026-11-13

## Context

A standard CUPS installation needs no password to print, or to check or cancel your own jobs: it trusts `requesting-user-name` (Spike 1). Print servers set up to know who prints, and queues whose backend needs the user's password, put `AuthType Basic` and `Require valid-user` in a policy. Until now Latiprimere couldn't send credentials. A refusal surfaced as "Could not communicate with the print system", with the HTTP status 401 as `NativeErrorCode`.

Checked against CUPS 2.4 in the test container:

- **CUPS checks a job's permissions only after receiving the whole document.** It answers `Expect: 100-continue` at once and refuses with 401 afterwards. So answering a challenge means uploading the document twice. A document read from a stream that can't seek can't be sent twice at all. SharpIppNext's request content rewinds seekable streams; a forward-only stream would be sent empty the second time.
- **CUPS asks for TLS before it asks for a password.** Stock CUPS has `DefaultEncryption Required`. Over plain HTTP from another machine, it answers an authenticated request with 426 Upgrade Required, expecting an in-place upgrade (RFC 2817). .NET doesn't do that upgrade.
- **CUPS accepts TLS directly on its IPP port,** spotting the TLS handshake on the same port 631. Its certificate is self-signed unless the administrator installs another, so .NET's standard check refuses it.
- **CUPS sends 401, 403 and 426 as HTML pages, not IPP.** SharpIppNext 5.0.2 reports them as `HttpRequestException` with the status. Its newer source decodes an IPP body when there is one.
- **A wrong password costs about 2 seconds** on every request that carries it, PAM's delay after a failure. CUPS then treats the request as anonymous, so requests that need no password still succeed.

## Decision

1. **`CupsOptions.Credentials` (`NetworkCredential?`).** It is sent with every request as HTTP Basic, not in answer to a challenge, so no document is sent twice. Basic is the scheme CUPS uses with passwords; `Domain` isn't used. A user name that is blank, or contains the `:` Basic separates on, is refused with `ArgumentException` when the print system is created.
2. **`CupsOptions.Encryption` (`CupsEncryption`):**
   - `Automatic`, the default, uses TLS over TCP whenever credentials are set, so a password never travels in clear text. Without credentials there's no TLS, as before.
   - `Always` uses TLS for every TCP request, which also keeps documents private on the network.
   - `Never` never uses TLS. Credentials then travel in clear text: only for servers that accept them that way, and only on a network you trust.

   The local Unix domain socket is never encrypted: CUPS doesn't ask for it there. TLS is chosen up front because .NET can't follow a 426 upgrade. The request goes to `https://` on the same port, and the `printer-uri` inside the IPP request stays `ipp://`.
3. **`CupsOptions.ServerCertificateValidationCallback` (`RemoteCertificateValidationCallback?`).** It is `null` by default, for the standard check. Applications use it to trust a server's self-signed certificate, for example by comparing its thumbprint. The documentation warns that accepting every certificate gives up what TLS is for.
4. **`requesting-user-name` defaults to the credentials' user name** when `UserName` isn't set, matching the user CUPS records once a request is authenticated.
5. **A new `PrintAuthenticationException : PrintException`, in Abstractions.** It is thrown when the print system needs credentials, doesn't accept the ones sent, or needs them over an encrypted connection: HTTP 401 or 426, or IPP `client-error-not-authenticated`. Applications catch it to ask the user for a password, then create the print system again. `NativeErrorCode` holds the HTTP or IPP status.
6. **Other refusals get explained `PrintException`s:**
   - 403, or IPP `client-error-not-authorized`: "does not allow this user to make this request";
   - a failed TLS handshake: an explanation of self-signed certificates and the validation callback.

   `PrintException.NativeErrorCode`'s documentation now also names HTTP status codes, which it already carried.
7. **The plumbing is in `Latiprimere.Ipp`,** for the direct IPP adapter's later IPPS and authentication (Phase 3):
   - `IppConnectionSecurity` and the `IppConnectionHandler` delegating handler;
   - the mapping in `SharpIppClientAdapter`.

   `IppNetworkOptions` gets nothing yet.

## Consequences

- Printing to a CUPS server that needs a password works from Windows, Linux and macOS, and over the local socket. A password is never sent in clear text unless `Encryption` is set to `Never`.
- **Connecting to a stock CUPS server with credentials needs a certificate decision from the application,** because of the self-signed certificate. The error message says so. libcups instead trusts a server's certificate the first time it sees it, and remembers it. Latiprimere leaves that policy to the application rather than keep a certificate store of its own.
- Sending credentials with every request means one PAM check per request on the server, and a wrong password slows every request by about 2 seconds until it is corrected.
- `PrintAuthenticationException` adds one public type to Abstractions after the 0.1 API review ([ADR 0018](0018-api-review-0-1.md)). It derives from `PrintException`, so callers that catch only that are unaffected.
- **Not covered:**
  - **Kerberos (`AuthType Negotiate`):** `SocketsHttpHandler.Credentials` could do it, but only by answering a challenge, which sends the document twice.
  - **Local peer credentials (`PeerCred`):** libcups proves the local user over the Unix socket without a password. Latiprimere sends the password there like anywhere else.
  - **Interactive prompting through a callback,** as libcups's password callback does: the exception covers that.
- **Verified against the test container's `Secure` queue** (policy `authenticated`, which needs a password over TLS to print):
  - printing with credentials, with the job owned by the test user;
  - 426 without credentials;
  - 401 for a wrong password;
  - 426 with credentials and `Never`;
  - the certificate error with the standard check.

  **Not verified:** credentials over the Unix socket, and a real print server with a certificate from a trusted authority.
