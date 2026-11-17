# Architecture Decision Records

Short records of significant decisions: the context, the choice, and its consequences. Once a record is **Accepted**, don't edit its decision; supersede it with a new record instead.

Statuses: **Proposed** → **Accepted** / **Rejected**, or later **Superseded by NNNN**.

| # | Title | Status |
|---|---|---|
| [0001](0001-headless-ipp-first-scope.md) | Headless, IPP-first scope for v1 | Accepted |
| [0002](0002-target-framework-and-interop.md) | Multi-target .NET 8 and .NET 10 with source-generated interop | Accepted; target frameworks superseded by 0013 |
| [0003](0003-coding-standards.md) | Adopt an adapted coding standard | Accepted |
| [0004](0004-naming-and-poc-approach.md) | Naming, and building the proof of concept as the real packages | Accepted; name superseded by 0016 |
| [0005](0005-build-and-test-tooling.md) | Build and test tooling | Accepted; resources line superseded by 0014 |
| [0006](0006-ipp-and-cups-adapter-design.md) | IPP layer and CUPS adapter design | Accepted |
| [0007](0007-windows-spooler-adapter-design.md) | Windows spooler adapter design | Accepted |
| [0008](0008-print-system-failure-isolation.md) | Isolate print system failures in the printer catalog | Accepted |
| [0009](0009-windows-pdf-printing.md) | PDF printing on Windows through PDFium and GDI | Accepted |
| [0010](0010-direct-ipp-adapter-design.md) | Direct IPP to network printers | Accepted |
| [0011](0011-capability-model.md) | Capability model | Proposed |
| [0012](0012-vendor-option-escape-hatch.md) | Vendor-specific option escape hatch | Accepted |
| [0013](0013-target-net10-and-net11.md) | Target .NET 10 and .NET 11 | Accepted |
| [0014](0014-resx-source-generator.md) | Resource classes from the ResX source generator | Accepted |
| [0015](0015-split-licence.md) | Split licence: free Community Licence, paid Commercial Licence | Accepted; licence text revised by 0021 |
| [0016](0016-rename-to-latiprimere.md) | Rename CrossPrint to Latiprimere | Accepted |
| [0017](0017-retry-busy-ipp-printers.md) | Resend jobs that a busy IPP printer turns away | Accepted |
| [0018](0018-api-review-0-1.md) | Public API for 0.1.0-preview | Accepted |
| [0019](0019-windows-error-mapping.md) | Windows error mapping | Proposed |
| [0020](0020-cups-authentication.md) | CUPS authentication | Proposed |
| [0021](0021-licence-text-before-first-package.md) | Licence text revised before the first package | Accepted |
| [0022](0022-release-process.md) | Release process and package contents | Proposed; partly superseded by 0028 |
| [0023](0023-commercial-licence-price.md) | Commercial Licence price and terms | Accepted |
| [0024](0024-known-ipp-printer-uris.md) | Look up only known IPP printer URIs and Windows printer names | Proposed |
| [0025](0025-choose-paper-colour-and-tray.md) | Choose paper size, colour mode and tray | Proposed |
| [0026](0026-vendor-options-in-detail.md) | Vendor options in detail | Proposed |
| [0027](0027-direct-ipp-for-windows-queues.md) | Direct IPP for Windows queues | Accepted |
| [0028](0028-automated-releases.md) | Automated releases from the release branch | Proposed |
| [0029](0029-end-to-end-test-harness.md) | End-to-end test harness | Accepted |

## Template

```markdown
# NNNN: Title

- Status: Proposed
- Date: YYYY-MM-DD

## Context

## Decision

## Consequences
```
