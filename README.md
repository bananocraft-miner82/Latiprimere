# Latiprimere

A cross-platform printing library for .NET: discover printers, read their capabilities, submit jobs and track them to completion, with the same code on Windows, Linux and macOS.

> **Status:** preview. Version 0.1.0-preview is on [NuGet](https://www.nuget.org/packages/Latiprimere) for evaluation. The API may still change before 1.0, so it isn't ready for production use yet.

## Goals

- One API for printer discovery, capabilities, job submission, status and cancellation.
- Headless first: works in console apps, services, containers and web backends.
- No UI framework dependency in the core.
- IPP-first, using native platform APIs rather than command-line tools.
- Trimming and Native AOT compatible.

## Documentation

See [docs/](docs/README.md) for the feasibility research, project plan, proof-of-concept plan, decision records and progress log.

## Licence

Latiprimere is free under the [Community Licence](LICENSE) for:
- individuals and organisations with annual revenue under US$1 million;
- non-profits and education;
- open-source projects;
- evaluation and development;
- use through someone else's product.

Larger organisations using it in production need a [Commercial Licence](COMMERCIAL.md).

Contributors sign the [Contributor Licence Agreement](CLA.md).
