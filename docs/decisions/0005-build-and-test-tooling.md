# 0005: Build and test tooling

- Status: Accepted; the strongly typed resources line is superseded by [ADR 0014](0014-resx-source-generator.md).
- Date: 2026-11-10

## Context

Spike 0 set up the solution. Several tooling choices aren't covered by earlier records but affect every contributor.

## Decision

- **Package sources:** a repo-level `nuget.config` clears inherited sources and restores everything from nuget.org, so builds don't depend on a contributor's machine-level NuGet configuration.
- **Central package management:** all package versions live in `Directory.Packages.props`.
- **SDK pinning:** `global.json` requires the .NET 10 SDK (`10.0.100`, rolling forward to the latest feature band).
- **Tests:** xUnit v3 on Microsoft Testing Platform, opted in through `global.json`. Test projects are executables and multi-target like the libraries.
- **Style enforcement:** `.editorconfig` with `EnforceCodeStyleInBuild` and warnings as errors. StyleCop.Analyzers is used only for its documentation rules (SA1600–SA1602), so private members need XML docs too; every other StyleCop rule is off.
- **Code layout choices not specified by CONTRIBUTING.md:** file-scoped namespaces, and `using` directives outside the namespace with `System` first.
- **Analyzer rules switched off:** CA1805 and CA1863 everywhere; CA1861, CS1591, SA1600 (except builders, fakes and fixtures), CA2007 and the async-suffix naming rule in tests; CA2007 in samples. Each is commented in its `.editorconfig`.
- **Strongly typed resources:** generated at build time from `.resx` files via explicit `StronglyTyped*` metadata in the project file; no generated code is committed.

## Consequences

- A fresh clone builds the same way on any machine and in CI.
- Visual Studio needs a version with Microsoft Testing Platform support to run tests from Test Explorer.
- Target-typed `new()` can't be enforced by tooling and is checked in review.
