# 0016: Rename CrossPrint to Latiprimere

- Status: Accepted
- Date: 2026-11-12
- Supersedes: the name chosen in [ADR 0004](0004-naming-and-poc-approach.md).

## Context

ADR 0004 chose `CrossPrint` for namespaces and package IDs. At the time, the only concern was that "Cross Print" is also a commercial cloud-printing service (crossprint.io).

[ADR 0015](0015-split-licence.md) then made CrossPrint a product that sells licences. That raises the risk of a trademark dispute under a name so close to an existing printing business. The `crossprint.com` and `crossprint.io` domains are taken too. No package has been published yet, so this is the cheapest time to rename.

About 90 names were checked on 2026-11-12 against:
- the domain registries;
- GitHub user and organisation names;
- NuGet IDs;
- a web search for existing products.

| Name | Result |
|---|---|
| Printvane, Printkeel, Platenwise | All free. The first shortlist. |
| Names with "spool" in them | Left out: "spool" also means a reel of 3D-printer filament, a field crowded with names like Spoolman and Spoolara |
| Imprime, Imprimere, Imprimo, Imprimis | Most domains and the GitHub names taken. Real words for printing in Latin or in French, Spanish and Portuguese, so weak as trademarks in the EU. |
| Imprimely | All free; a coined form of *imprime* |
| **Latiprimere**, Latiprime, Latiprint, Ubimprime | All free, and no existing product found |

## Decision

Owner decision 2026-11-12: **the project becomes Latiprimere.**

- **The name** joins Latin *lati-* ("broad", from *latus*) with *imprimere* ("to print"): "to print broadly". It also echoes Star Trek's "to boldly go".
- **When checked, all of these were free:**
  - the `.com`, `.net`, `.org`, `.dev` and `.io` domains;
  - the GitHub name;
  - the NuGet ID.
- **It's an invented word,** so it should make a stronger trademark than a word that describes printing.
- **Namespaces and package IDs become `Latiprimere.*`,** such as `Latiprimere.Cups` and `Latiprimere.Windows`.

## Consequences

- **Every reference is renamed** in one change: code, projects, packages, tools, test data and docs, including past progress-log entries and ADRs. The old name stays only where the docs discuss the name itself, such as how it was chosen in ADR 0004, and in mentions of the "Cross Print" service.
- **Before the rename is pushed** to the public repository, the owner registers at least `latiprimere.com` and `latiprimere.dev`. Pushing the rename shows the name publicly.
- **Before selling licences under it,** the owner searches the trademark registers (EUIPO, USPTO, national) in classes 9 and 42.
- **Reserve the `Latiprimere.` NuGet ID prefix** before the first publish, as planned for `CrossPrint.`.
- **The GitHub repository is renamed by the owner.** GitHub redirects the old URL.
