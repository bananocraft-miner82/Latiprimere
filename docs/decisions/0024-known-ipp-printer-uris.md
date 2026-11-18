# 0024: Look up only known IPP printer URIs and Windows printer names

- Status: Accepted
- Date: 2026-11-15

## Context

A direct IPP printer identifier can carry its URI: `ipp:ipp://192.168.1.20/ipp/print`. Until now, looking one up through the catalog connected to whatever host and port the URI named, whether or not the printer was configured or had been found on the network.

That is a server-side request forgery risk. A web application that lets a browser choose a printer by identifier, and passes it to `GetPrinterAsync`, could be made to send requests to internal services or a cloud metadata endpoint. Its error messages would also tell open, closed and filtered ports apart. The code review of 2026-11-15 found this.

## Decision

The choices are mine, for the owner to review.

- **A lookup by URI contacts only a known printer:**
  - one in `IppNetworkOptions.PrinterUris`;
  - or one whose URI a printer announced on the network, while listing or looking up by name.

  Any other URI finds no printer, so the catalog throws `PrinterNotFoundException` without connecting anywhere.
- **`IppNetworkOptions.AllowAnyPrinterUri`,** off by default, brings back the old behaviour for applications whose every identifier is trusted.
- **URIs compare with IPP's default port filled in** (631, for both `ipp` and `ipps`), so `ipp://host/ipp/print` and `ipp://host:631/ipp/print` are the same printer.
- **Name identifiers (`ipp:dnssd:`) are unchanged:** they only reach printers that announce themselves.
- **Windows printer names (added after the second review, 2026-11-15):** the same risk exists for `windows:` identifiers. `OpenPrinter` with `\\server\printer` contacts that server, and Windows may sign in to it with the process's account, sending an NTLM response that can be relayed or cracked. A comma opens special spooler handles (`name,Job 5`, `,XcvMonitor Local Port`).
  - A name with a backslash or a comma is opened only if Windows lists it: a local printer, or a connection the user already made.
  - Plain local names are opened directly, as before.
  - **`WindowsOptions.AllowAnyPrinterName`,** off by default, lets applications whose identifiers are all trusted look up printers on servers that aren't connected yet (owner request: "Being able to look up remote printers might be something that the developers will need"). It is set with `AddLatiprimereWindows(options => ...)`, like the CUPS and IPP options, or passed to `WindowsPrintSystemFactory.Create`.

## Consequences

- **Safe by default:** an identifier from outside the application can't make it connect to arbitrary hosts.
- **Breaking for one use:** an application that looks up URIs it never configured, such as one typed in by a trusted administrator, must add them to `PrinterUris` or turn on `AllowAnyPrinterUri`. Nothing has been released yet, so no published version behaves the old way.
- **Public API additions:**
  - `Latiprimere.Ipp`: the `AllowAnyPrinterUri` property;
  - `Latiprimere.Windows`: the `WindowsOptions` class with `AllowAnyPrinterName`, an optional `configure` parameter on `AddLatiprimereWindows`, and an optional `options` parameter on `WindowsPrintSystemFactory.Create`, after the rasteriser, so existing calls still compile.
- **On Windows,** a lookup of a printer name with a backslash or comma lists the printers first, which costs a spooler call. Without the option, printers on a server the user hasn't connected to can't be looked up by name.
- **`WindowsOptions` is also where Phase 2's render resolution setting goes** (the `maxRenderDpi` TODO in `WindowsPrintSystemFactory`).
