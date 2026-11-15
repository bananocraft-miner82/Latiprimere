# 0021: Licence text revised before the first package

- Status: Accepted
- Date: 2026-11-13

## Context

[ADR 0015](0015-split-licence.md) chose the split licence, and its text was drafted then. Every package published to nuget.org carries `LICENSE`, and keeps that text for good: a version can be unlisted but not deleted. So the text was read again before 0.1.0-preview. These problems turned up:

- **"See COMMERCIAL.md" pointed nowhere.** Only `LICENSE` goes into the packages.
- **"Production" wasn't defined,** although condition 1d, evaluation and development, depends on it. It was unclear whether a larger company that ships Latiprimere inside its product needs a Commercial Licence.
- **The open-source condition (1c)** applied only when the project was the user's sole use of Latiprimere. So a company with one open-source project and any other use was covered for neither. It also didn't say what happens further down: whether a project under, say, MIT could pass Latiprimere on under MIT.
- **Nothing said that each version keeps its own licence.**

## Decision

The owner asked for the changes to be drafted ("draft them") and confirmed the wording on 2026-11-13 ("Yes, keep them"). The split-licence decision in ADR 0015 is unchanged.

- **"Production" is defined:** any use except evaluating Latiprimere, or developing and testing software with it. It includes:
  - running it, or software that includes it, in your own operations;
  - distributing it to others, paid or free;
  - providing a service that uses it.

  A larger organisation that ships Latiprimere inside its product therefore needs a Commercial Licence.
- **Open source (1c):**
  - It covers use in a published OSI-licensed project, whoever publishes it, but only that use.
  - Section 2b says Latiprimere stays under its own licence inside a project under another licence. That project's users need a condition of their own, usually indirect use (1e).
- **Versions (new section 5):** the licence applies to the versions it comes with. Later versions may have different terms without changing earlier ones.
- **The Commercial Licence is reached by URL** to COMMERCIAL.md on GitHub.
- **Unchanged:**
  - the revenue limit counts revenue only, not funding raised;
  - the licence stays version 1.0, since no package shipped under the earlier text.
- **COMMERCIAL.md** uses the same meaning of production. **CLA.md** sends organisations that want to contribute to the contact address in COMMERCIAL.md.

## Consequences

- **COMMERCIAL.md needs a contact address before the first package,** because every package links to it.
- **Still for a lawyer before any Commercial Licence is sold:**
  - governing law and jurisdiction;
  - the warranty wording for paid licences and consumer law;
  - the commercial agreement itself.
- **The licence must make sense on its own,** without other repository files. Any later change to it needs the same care, since published versions keep their text.
