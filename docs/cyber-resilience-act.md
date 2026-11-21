# The Cyber Resilience Act

How Latiprimere meets the EU Cyber Resilience Act (CRA, [Regulation (EU) 2024/2847](https://eur-lex.europa.eu/eli/reg/2024/2847/oj)): what's done, what to do when a vulnerability is exploited, and what's left before December 2027. The decisions are in [ADR 0051](decisions/0051-security-policy-sbom-and-cra.md). This is a working plan, not legal advice. The questions for a lawyer are kept in the owner's private list.

## Latiprimere under the CRA

- **Latiprimere is a product with digital elements.** Software sold on its own is one, and a library is no exception.
- **Its manufacturer** is Jonathan Price, trading as Prodigy Software Solutions, in the Netherlands.
- **It's sold in the course of a commercial activity:** larger organisations pay for a Commercial Licence. So the CRA's exemption for free and open-source software doesn't apply, whether or not the previews already count.
- **It's in the default category:** it isn't one of the important or critical products listed in Annexes III and IV. So its conformity is self-assessed, under internal control (module A), with no notified body.
- **The dates:**
  - **from 11 September 2026:** reporting actively exploited vulnerabilities and severe incidents (article 14). This covers versions already on the market.
  - **from 11 December 2027:** everything else, for versions placed on the market from then on.

## What's done

| Requirement | Where |
|---|---|
| A contact for vulnerability reports, and a coordinated disclosure policy (Annex I, part II) | [SECURITY.md](../SECURITY.md): GitHub private vulnerability reporting and security@latiprimere.com |
| A software bill of materials, in a common machine-readable format, covering at least the top-level dependencies (Annex I, part II, 1) | A CycloneDX SBOM in every package, at `sbom/<package>.cdx.json` ([generate-sboms.sh](../tools/releasing/generate-sboms.sh)) |
| A support period, and security updates free of charge during it (article 13, Annex I, part II) | [SECURITY.md](../SECURITY.md#supported-versions): five years from each line's first stable release. A Commercial Licence that has ended may still ship security updates to copies it shipped ([COMMERCIAL-TERMS.md](../COMMERCIAL-TERMS.md), change 12) |
| The manufacturer's name, postal address and contact (article 13) | [SECURITY.md](../SECURITY.md#manufacturer), and the manufacturer in each SBOM |
| Keeping third-party components up to date (article 13) | Dependabot for NuGet and GitHub Actions ([dependabot.yml](../.github/dependabot.yml)) |
| Publishing fixed vulnerabilities (Annex I, part II, 4) | GitHub security advisories, with CVEs ([SECURITY.md](../SECURITY.md#what-happens-next)) |

## When a vulnerability is actively exploited

**This has applied since 11 September 2026.**

A vulnerability is actively exploited when there's reliable evidence that someone has used it without permission. A severe incident is one that affects Latiprimere's security, such as a compromised release.

Reports go through ENISA's [Single Reporting Platform](https://www.enisa.europa.eu/topics/product-security/single-reporting-platform-srp). They reach the CSIRT the Netherlands designated as coordinator, because the manufacturer is established there. The clock starts when the manufacturer becomes aware.

1. **Within 24 hours: an early warning.** It says which product and versions are affected, and whether the vulnerability or incident is suspected to be malicious. It also lists the EU countries where Latiprimere is known to be used, as far as known.
2. **Within 72 hours: a notification.** It adds:
   - what's known about the vulnerability or incident;
   - how severe it is;
   - what's being done to fix it;
   - what users can do in the meantime.
3. **The final report.** For a vulnerability, it's due within 14 days of a fix or other corrective measure being available. For an incident, within one month of the notification. It gives:
   - a description and its severity;
   - for a vulnerability, who exploited it, where known;
   - for an incident, its likely cause;
   - the fix or other measures taken.
4. **Telling users.** Those affected, and the public where that's needed, hear about the vulnerability or incident and what to do. This happens through a GitHub security advisory, the changelog and the website.

Write down when each step happened. A report that isn't complete yet can be updated later, but the deadlines still apply.

## What's left before 11 December 2027

These apply to versions released from 11 December 2027, which will be 1.0 or later.

- [ ] **Register on the Single Reporting Platform** before it's needed. Check which Dutch CSIRT coordinates, and how to reach it outside office hours.
- [ ] **A cybersecurity risk assessment** (article 13). It covers Latiprimere's intended use and the threats that come with it:
  - documents from anyone, such as PDFs, images and text;
  - printers anyone on the network can announce;
  - certificates and passwords;
  - responses from printers and print servers.

  It says how each essential requirement in Annex I, part I, is met, or why one doesn't apply. Much of it already exists in the ADRs and the documentation, such as [network printers](site/print-systems/network-printers.md) and [secure printers](site/print-systems/secure-printers.md).
- [ ] **Check the essential requirements against the code** (Annex I, part I). The main points:
  - no known exploitable vulnerabilities at release;
  - secure by default;
  - protection of confidentiality and integrity, such as certificate checks and passwords only over IPPS;
  - limiting attack surface and the impact of incidents;
  - resilience against malformed input, as the image size cap already gives.
- [ ] **Security updates apart from new features,** where technically feasible (Annex I). Today a fix comes in the next `11.0.<n>` release with whatever else is new. Decide whether security fixes need their own releases.
- [ ] **Information for users** (Annex II), in the documentation:
  - the manufacturer and the contact for vulnerabilities;
  - the product, its versions and its intended purpose;
  - known risks;
  - where the SBOM is;
  - the end date of the support period;
  - how to install security updates;
  - the EU declaration of conformity.
- [ ] **Technical documentation** (Annex VII). It covers:
  - the design and development;
  - the risk assessment;
  - the vulnerability handling process;
  - the SBOM;
  - the support period;
  - the standards or specifications applied;
  - test reports.

  Keep it for ten years after the product is placed on the market, or for the support period if that is longer (article 13).
- [ ] **The EU declaration of conformity** (Annex V), drawn up and signed by the manufacturer.
- [ ] **CE marking:** for software, on the declaration of conformity or on the website that goes with the product (articles 29 and 30).
- [ ] **A security contact that stays reachable for the whole support period,** including while the owner is away.
- [ ] **Due diligence on components** (article 13). PDFium parses the documents applications print, so its updates matter most. The SBOM lists the `bblanchon.PDFium.*` packages, but not the libraries bundled inside PDFium's binaries.

## Product Liability Directive

The revised Product Liability Directive ([Directive (EU) 2024/2853](https://eur-lex.europa.eu/eli/dir/2024/2853/oj)) treats software as a product, for products placed on the market after 9 December 2026. Missing security updates can make a product defective while it's still under the manufacturer's control. So the security updates promised in [SECURITY.md](../SECURITY.md) matter here as well as under the CRA. Liability to injured people can't be limited by contract.
