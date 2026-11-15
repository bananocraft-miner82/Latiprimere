# 0015: Split licence: free Community Licence, paid Commercial Licence

- Status: Accepted; licence text revised by [0021](0021-licence-text-before-first-package.md)
- Date: 2026-11-12

## Context

The repository started with the MIT licence. The owner wants to be able to earn from Latiprimere through licensing in future. MIT allows that only indirectly, through support, sponsorship or paid add-ons, because nothing in it makes a business pay.

The options considered:

| Model | For | Against |
|---|---|---|
| Permissive (MIT, Apache-2.0) | Widest adoption | No licensing income |
| Copyleft plus commercial (AGPL-3.0, with a commercial licence) | OSI-approved; proprietary users must buy | Many companies ban the AGPL, so they wouldn't evaluate Latiprimere at all |
| **Split licence: free under a revenue limit, paid above it** | Charges the businesses that get the most value; already familiar to .NET developers from QuestPDF and the Six Labors libraries | Not OSI open source; relies on users being honest |
| Delayed open source (BSL, FSL) | Each version becomes open source after a few years | Less familiar for .NET libraries, and better suited to hosted services |

The likely paying users of Latiprimere are businesses: point-of-sale systems, warehouse and label printing, kiosks, and line-of-business back ends. QuestPDF and the Six Labors libraries both changed licence after they had many users, and both drew a backlash. Changing before the first release avoids that.

## Decision

Owner decision 2026-11-12:

- **Latiprimere uses a split licence** ([LICENSE](../../LICENSE)):
  - the **Community Licence** is free for:
    - individuals and organisations with annual gross revenue under US$1,000,000, counting affiliates;
    - registered non-profits and accredited education;
    - OSI-licensed open-source projects;
    - evaluation and development, not production;
    - indirect use through someone else's product;
  - everyone else needs a **Commercial Licence** ([COMMERCIAL.md](../../COMMERCIAL.md)).
- **An organisation that passes the revenue limit has 90 days** to get a Commercial Licence.
- **Jonathan Price holds the copyright,** so contributors sign a **contributor licence agreement** ([CLA.md](../../CLA.md)). It lets the owner license contributions under both licences. A Developer Certificate of Origin isn't enough for this.
- **Packages carry the licence file** (`PackageLicenseFile`), because the split licence isn't an SPDX licence.

## Consequences

- **Latiprimere isn't "open source" by the OSI definition.** Some projects and companies won't use it because of that.
- **No MIT version is published.** The MIT licence covered only the private repository's first commits, which were never shared. The public repository starts from a single commit under this licence, so there is no MIT version anyone could have copied.
- **Before any Commercial Licence is sold:**
  - a lawyer drafts the commercial agreement and reviews the Community Licence and the CLA;
  - the owner decides what a licence includes, the prices, and how to buy one.
- **Before outside contributions are accepted,** the CLA Assistant bot must be set up on the repository.
- **The name should be checked as a trademark before it is sold under.** "Cross Print" is already a commercial cloud-printing service (crossprint.io). The project was renamed to Latiprimere for this reason ([ADR 0016](0016-rename-to-latiprimere.md)).
- **Dependencies are compatible:**
  - SharpIppNext, Tmds.MDns and the ResX source generator are MIT;
  - the PDFium binaries are Apache-2.0.
