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
| [0011](0011-capability-model.md) | Capability model | Accepted |
| [0012](0012-vendor-option-escape-hatch.md) | Vendor-specific option escape hatch | Accepted |
| [0013](0013-target-net10-and-net11.md) | Target .NET 10 and .NET 11 | Accepted |
| [0014](0014-resx-source-generator.md) | Resource classes from the ResX source generator | Accepted |
| [0015](0015-split-licence.md) | Split licence: free Community Licence, paid Commercial Licence | Accepted; licence text revised by 0021 |
| [0016](0016-rename-to-latiprimere.md) | Rename CrossPrint to Latiprimere | Accepted |
| [0017](0017-retry-busy-ipp-printers.md) | Resend jobs that a busy IPP printer turns away | Accepted |
| [0018](0018-api-review-0-1.md) | Public API for 0.1.0-preview | Accepted |
| [0019](0019-windows-error-mapping.md) | Windows error mapping | Accepted |
| [0020](0020-cups-authentication.md) | CUPS authentication | Accepted |
| [0021](0021-licence-text-before-first-package.md) | Licence text revised before the first package | Accepted |
| [0022](0022-release-process.md) | Release process and package contents | Accepted; partly superseded by 0028 |
| [0023](0023-commercial-licence-price.md) | Commercial Licence price and terms | Accepted |
| [0024](0024-known-ipp-printer-uris.md) | Look up only known IPP printer URIs and Windows printer names | Accepted |
| [0025](0025-choose-paper-colour-and-tray.md) | Choose paper size, colour mode and tray | Accepted |
| [0026](0026-vendor-options-in-detail.md) | Vendor options in detail | Accepted; print tickets in 0031 |
| [0027](0027-direct-ipp-for-windows-queues.md) | Direct IPP for Windows queues | Accepted |
| [0028](0028-automated-releases.md) | Automated releases from the release branch | Accepted; build number superseded by 0030 |
| [0029](0029-end-to-end-test-harness.md) | End-to-end test harness | Accepted |
| [0030](0030-release-numbering.md) | Number releases after the latest one | Accepted |
| [0031](0031-windows-print-tickets.md) | Print Tickets on Windows | Accepted; Print Capabilities in 0032 |
| [0032](0032-vendor-options-in-capabilities.md) | Vendor options in the capabilities, and Windows Print Capabilities | Accepted |
| [0033](0033-validate-job.md) | Validate-Job before Print-Job | Accepted |
| [0034](0034-raster-encoders.md) | Raster encoders for printers without PDF | Accepted |
| [0035](0035-printing-pdfs-as-raster.md) | Printing PDFs as raster over IPP | Accepted |
| [0036](0036-wsd-queues-over-ipp.md) | Windows WSD queues over IPP, matched by UUID | Accepted |
| [0037](0037-ipps-and-authentication.md) | IPPS and authentication for direct IPP | Accepted |
| [0038](0038-remembered-addresses.md) | Remembered printer addresses that survive a restart | Accepted |
| [0039](0039-api-reference.md) | API reference generated into committed pages | Accepted |
| [0040](0040-api-review-1-0.md) | Public API for 1.0 | Accepted |
| [0041](0041-upload-stall-timeout.md) | A time limit on a printer that stops taking a job | Accepted |
| [0042](0042-ipps-for-windows-queues-and-uri-authentication.md) | IPPS for Windows queues, and what printers say they ask for | Accepted |
| [0043](0043-job-state-reasons.md) | Why a job is in its state | Accepted |
| [0044](0044-room-to-grow-after-1-0.md) | Room to grow after 1.0 | Accepted |
| [0045](0045-pdf-writer.md) | A small PDF writer | Accepted |
| [0046](0046-pclm-and-raster-format-order.md) | PCLm, and the order of raster formats | Accepted |
| [0047](0047-printing-images.md) | Printing JPEG and PNG images | Accepted |
| [0048](0048-printing-plain-text.md) | Printing plain text | Accepted |
| [0049](0049-following-printers-and-jobs.md) | Following printers and jobs | Accepted |
| [0050](0050-commercial-licence-agreement.md) | Commercial Licence agreement, from Common Paper's Software License Agreement | Proposed |
| [0051](0051-security-policy-sbom-and-cra.md) | Security policy, SBOMs and groundwork for the Cyber Resilience Act | Proposed |
| [0052](0052-support-branches.md) | Support branches for older .NET lines | Proposed |
| [0053](0053-document-generation-companion.md) | Document generation as a companion product | Accepted |

## Template

```markdown
# NNNN: Title

- Status: Proposed
- Date: YYYY-MM-DD

## Context

## Decision

## Consequences
```
