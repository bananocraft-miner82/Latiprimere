# Security policy

## Reporting a vulnerability

**Please don't report a vulnerability in a public issue, discussion or pull request.** Report it privately, in either of these ways:

- **On GitHub:** open the repository's [Security tab](https://github.com/bananocraft-miner82/Latiprimere/security) and choose **Report a vulnerability**. Only you and the maintainer can see the report.
- **By email** to [security@latiprimere.com](mailto:security@latiprimere.com).

It helps to include:

- the package and version affected;
- the operating system and print system: CUPS, the Windows spooler, or a network printer over IPP or IPPS;
- what an attacker could do, and what they would need, such as a place on the same network, a printer they control, or a document they wrote;
- the steps to reproduce it, or a proof of concept;
- whether you know of anyone exploiting it.

## What happens next

Latiprimere is maintained by one developer. These are goals, not promises:

- **An acknowledgement** within 5 business days in the Netherlands.
- **An assessment** within 10 business days: whether it's a vulnerability, how severe, and what the fix is likely to be.
- **News as it's fixed,** and a chance to check the fix before it's released.

**Fixes are released as new versions** on nuget.org. Each fix gets a [GitHub security advisory](https://github.com/bananocraft-miner82/Latiprimere/security/advisories), with a CVE where one is warranted. The advisory credits you, unless you'd rather it didn't.

**Please keep the details private** until the fix is released, or for 90 days from your report, whichever is sooner. If a fix needs longer, we'll agree a date with you.

**Actively exploited vulnerabilities are also reported to the authorities,** as the EU Cyber Resilience Act requires: the Dutch CSIRT and ENISA, through ENISA's Single Reporting Platform. Only what the report needs is shared.

## Supported versions

| Versions | Security updates |
|---|---|
| Stable, from 1.0 | Each .NET line, such as `11.0.x`, gets security updates for at least five years from the date of its first stable release. The fix goes into a new version of that line. |
| Previews, such as `11.0.8-preview` | Only the latest preview is fixed. Upgrade to it, or to the first stable release once it's out. |

**Within a line, upgrading doesn't break your code:** the public API only gains additions until the next .NET major version ([CONTRIBUTING.md](CONTRIBUTING.md#changing-the-public-api)). So a fix comes in the latest version of the line, and that's the version to upgrade to.

**Security updates cost nothing,** whichever licence you use Latiprimere under:

- **Community Licence:** it covers updates as it covers every version.
- **Commercial Licence that has ended:** you may still ship security updates to the copies of your products you distributed while it was in force ([COMMERCIAL-TERMS.md](COMMERCIAL-TERMS.md)).

## What's in scope

**In scope:** the `Latiprimere` packages on nuget.org and the code in `src/`. Examples:

- a crafted printer response or document that crashes or takes over an application;
- a password or document sent where it shouldn't go;
- a certificate accepted when it shouldn't be.

**Out of scope, but still welcome:** the samples, tests and tools in this repository, which aren't shipped.

**Reported to their own projects, please,** with a note to us too, as we may need to update:

- Latiprimere's dependencies, such as PDFium, SharpIppNext and Tmds.MDns;
- printers' own firmware.

Some risks come with printing on a network, and the documentation says what Latiprimere can and can't do about them:

- [anyone on the network can announce a printer](docs/site/print-systems/network-printers.md#anyone-on-the-network-can-announce-a-printer);
- [secure printers](docs/site/print-systems/secure-printers.md) covers IPPS and certificates.

## Software bill of materials

Each package carries a [CycloneDX](https://cyclonedx.org) software bill of materials (SBOM), at `sbom/<package>.cdx.json` inside the `.nupkg`, for example `sbom/Latiprimere.Ipp.cdx.json`. It lists the package's dependencies, direct and transitive, with their versions, licences and hashes. Packages used only to build Latiprimere, such as analyzers, are listed with the scope `excluded`.

## Manufacturer

Latiprimere is made by Jonathan Price, trading as Prodigy Software Solutions, Jeroen Boschstraat 11, 4715 BL Rucphen, the Netherlands. Security contact: [security@latiprimere.com](mailto:security@latiprimere.com).
