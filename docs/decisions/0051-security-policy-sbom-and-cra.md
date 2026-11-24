# 0051: Security policy, SBOMs and groundwork for the Cyber Resilience Act

- Status: Proposed
- Date: 2026-11-21

## Context

Selling Latiprimere makes its seller the manufacturer of a product with digital elements, under the EU Cyber Resilience Act ([ADR 0050](0050-commercial-licence-agreement.md)). The seller is Jonathan Price, trading as Prodigy Software Solutions. The Act's reporting duties have applied since 11 September 2026, to products already on the market, and its other duties apply from 11 December 2027.

Latiprimere had:

- no security policy;
- no private way to report a vulnerability;
- no software bill of materials (SBOM);
- no stated support period.

The Commercial Licence draft ended all production use when a licence ended. So a lapsed buyer couldn't ship a security fix to its own customers, though the Act wants security updates free of charge for the whole support period.

## Decision

Owner, 2026-11-21:

- "Let's add the SECURITY.md now."
- "Can we already start generating an SBOM as part of the build and release process?"
- "I'm happy to stop the licence purchaser using the software, but their customers with valid deploys with copies from when the licence was valid should definitely get security updates."
- "Add the CRA groundwork now."

**[SECURITY.md](../../SECURITY.md):**
- **Reports** go through GitHub's private vulnerability reporting, or by email to `security@latiprimere.com`, never through public issues.
- **Response goals,** not promises, for one developer:
  - an acknowledgement within 5 Dutch business days;
  - an assessment within 10 business days.
- **Disclosure:** reporters are asked to wait until the fix, or 90 days. Each fix gets a GitHub security advisory, with a CVE where warranted.
- **Actively exploited vulnerabilities** are reported to the authorities, as the Act requires.
- **Scope:** the packages and `src/`. Dependencies and printers' firmware go to their own projects.
- **The manufacturer's name, address and security contact.**

**SBOMs:**
- **Each package carries a CycloneDX SBOM** at `sbom/<package>.cdx.json`.
- **[generate-sboms.sh](../../tools/releasing/generate-sboms.sh)** writes one per package with the CycloneDX tool, version 6.3.1, from [dotnet-tools.json](../../dotnet-tools.json).
- **What they hold:**
  - the dependencies, direct and transitive, with versions, licences and hashes;
  - Latiprimere's own packages that a package depends on;
  - build-only packages, such as analyzers, with scope `excluded`;
  - the manufacturer, from [sbom-metadata.cdx.xml](../../tools/releasing/sbom-metadata.cdx.xml).
- **CI writes them before packing** (build.yml) and passes the directory to `dotnet pack` as `SbomDirectory`. The pack step fails if any package lacks its SBOM. The SBOMs are also kept as a build artifact.
- **The release publishes the packages build.yml made,** so every version on nuget.org carries its own SBOM. release.yml needs no change.
- **Microsoft.Sbom.Targets was tried first and not used.** Its SPDX output:
  - listed build-only analyzers as dependencies;
  - listed Latiprimere as a dependency of itself;
  - picked up leftover pack files from `obj/`.

**Security updates after a Commercial Licence ends:** a new change 12 in [COMMERCIAL-TERMS.md](../../COMMERCIAL-TERMS.md), which survives the end of the Agreement.
- **What it allows:** the buyer may update the copies of its products it shipped while licensed with Latiprimere's security updates, and ship them to the same recipients.
- **How long:** until the end of the support period of that line.
- **What it doesn't allow:** production use in the buyer's own operations, or new copies for anyone else.
- **[COMMERCIAL.md](../../COMMERCIAL.md)** says so too.

**The plan, [cyber-resilience-act.md](../cyber-resilience-act.md):**
- what the Act asks of Latiprimere, and what's done;
- the steps for reporting an actively exploited vulnerability or severe incident: 24 hours, 72 hours, then a final report;
- what's left before 11 December 2027.

**READMEs:** [README.md](../../README.md) and the package README gain a Security section.

Drafted for the owner to confirm. None of these is a decision yet:

- **The support period:** each .NET line, such as `11.0.x`, gets security updates for at least five years from its first stable release. Five years is the Act's default.
  - Only the latest preview is fixed.
  - Within a line, a fix comes in the line's next version, and upgrading to it is safe, since the public API only grows within a line.
- **The two response goals,** and the 90-day disclosure window.

## Consequences

- **Five years per line is a real commitment.** When .NET 12 brings a `12.0.x` line, `11.0.x` still needs security fixes until five years after its first stable release. That means maintaining a branch and releasing from it, which the release workflow can't do yet: it releases from `release` only. [ADR 0052](0052-support-branches.md) proposes how.
- **The owner sets up, before relying on the policy:**
  - **GitHub private vulnerability reporting:** in the repository's Settings, then Advanced Security, then Private vulnerability reporting, choose Enable.
  - **the `security@latiprimere.com` mailbox;**
  - **registration on ENISA's Single Reporting Platform.**
- **The SBOM lists the PDFium packages, but not the libraries inside PDFium's binaries.** Those are tracked through PDFium's own updates.
- **The packages' contents change:** each gains `sbom/<package>.cdx.json`. Package validation is unaffected, as it compares APIs, not files.
- **A local `dotnet pack` without `SbomDirectory` makes packages with no SBOM.** Only CI's packs, which the release uses, must have them.
- **ADR 0050's Cover Page gains change 12,** and the changes after it are renumbered.
- **Questions for a lawyer are in the owner's private list:**
  - whether change 12 meets the Act's free security updates;
  - whether the previews already count as commercial;
  - the length of the support period;
  - which Dutch CSIRT coordinates.
