# 0004: Naming, and building the proof of concept as the real packages

- Status: Accepted; the name is superseded by [ADR 0016](0016-rename-to-latiprimere.md)
- Date: 2026-11-10

## Context

The coding standard's namespace template is `CompanyName.Project.Package`, and `Prodigy.CrossPrint.*` was first chosen. On NuGet, `Prodigy` and 17 `Prodigy.*` package IDs belong to an unrelated owner and are marked verified, which indicates a reserved ID prefix; NuGet refuses new packages from other accounts under a reserved prefix. .NET convention is for package IDs and root namespaces to match.

Alternatives checked on 2026-11-10: `CrossPrint`, `Spoolwise`, `OpenSpool` and `Platen` were all unclaimed on NuGet. "Cross Print" is also the name of a commercial cloud-printing service (crossprint.io); `PrintBridge` and `Printwise` clash with existing printing software.

The proof of concept could either be throwaway spikes or the first slices of the real library.

## Decision

- Namespaces, assemblies, project names and NuGet package IDs all use the root `CrossPrint` (for example `CrossPrint.Windows`). The main package is `CrossPrint`.
- Apply for a NuGet ID prefix reservation for `CrossPrint.` before the first public release.
- The proof of concept builds thin vertical slices of the real projects in `src/` and `tests/`, to the full coding standard and with tests. Code from a route that fails its go/no-go check is removed or parked behind an issue.

## Consequences

- Package IDs and namespaces match, so users find the right package from a `using` directive.
- The project name overlaps with an unrelated commercial product in the printing field; README and package descriptions must make clear what CrossPrint is.
- No rewrite step between proof of concept and Phase 1, but the proof of concept takes somewhat longer because it includes scaffolding, standards enforcement and CI.
