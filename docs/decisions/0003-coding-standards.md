# 0003: Adopt an adapted coding standard

- Status: Accepted
- Date: 2026-11-10

## Context

The project started from the Prodigy Software Solutions C# standard. Prodigy Software Solutions is the owner's trading name, so the standard is the owner's own. Most of it applies directly: formatting, naming, explicit typing, async rules, nullable reference types, SOLID, DI, and the unit-test conventions. Some sections were specific to internal business applications and didn't apply to a printing library: a five-layer application architecture (front end down to database), database access helpers, and internal escalation processes.

## Decision

[CONTRIBUTING.md](../../CONTRIBUTING.md) is a Latiprimere-specific version of that standard:

- The general C# rules are kept, and enforced through `.editorconfig` and analyzers where possible.
- ERP, database and company-process sections are removed.
- The architecture section is replaced by the Latiprimere package layering: `Latiprimere.Abstractions` → platform adapters → `Latiprimere` services → consumers, with strict one-way references and no UI types in core packages.
- New sections cover the contribution workflow, platform-specific code (`LibraryImport`, `SafeHandle`, `[SupportedOSPlatform]`, AOT safety), and integration tests against virtual printers only.
- Clarifications: no `_`/`m` field prefixes (the `this.` qualifier marks fields); `const` allowed for non-public values and interop; `ConfigureAwait(false)` not required in tests; exception messages come from `.resx` resources (English only); log messages use structured templates.

## Consequences

- Contributors get a clear, enforced style from the first commit.
- Some rules (`this.` qualification, no `var`, no expression-bodied members) differ from common open-source .NET style; the `.editorconfig` has to guide external contributors.
- Every project carries a `.resx` resource file for its exception messages from the start (confirmed 2026-11-10).
