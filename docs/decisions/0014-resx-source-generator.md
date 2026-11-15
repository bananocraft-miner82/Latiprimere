# 0014: Resource classes from the ResX source generator

- Status: Accepted
- Date: 2026-11-12
- Supersedes: the "strongly typed resources" line of [ADR 0005](0005-build-and-test-tooling.md).

## Context

ADR 0005 generated the strongly typed classes for `.resx` files at build time, through `StronglyTyped*` metadata, into `obj/`. The command-line build was fine. Visual Studio, though, doesn't reliably include C# files that a build target writes. Before a build, or after a new target framework added a fresh `obj/` folder:

- the editor couldn't see classes such as `CliMessages`, and underlined their uses in red;
- its Remove and Sort Usings command deleted the `using` for the resources namespace as unnecessary, which then broke the command-line build.

## Decision

Owner decision 2026-11-12:

- **Every project with `.resx` files references `Microsoft.CodeAnalysis.ResxSourceGenerator`**, with `PrivateAssets="all"`. It is a Roslyn source generator from the .NET team, so the classes exist inside the compiler, in every editor, with or without a build.
- **The generator's defaults give the same classes as before:** namespace `<project>.Resources` and the file's name as the class name, such as `Latiprimere.Ipp.Resources.ExceptionMessages`. No code changed.
- **No generated code is committed**, as before.
- **Format methods are on (added 2026-11-12):** `EmitFormatMethods` is set for every `.resx` file in the root `Directory.Build.props`. Each message with placeholders gets a `Format…` method, such as `ExceptionMessages.FormatJobAlreadyFinished(jobID)`, so callers don't repeat `string.Format(CultureInfo.CurrentCulture, ...)`.
- **The package is published only as a prerelease.** 3.11.0-beta1.26226.5 (June 2026) is pinned in `Directory.Packages.props`. It is used at build time only and is not a dependency of any Latiprimere package; checked with `dotnet pack`.

## Consequences

- Visual Studio, Rider and VS Code see the resource classes immediately, and code clean-up no longer removes their `using`.
- A new project with `.resx` files needs the `PackageReference` ([CONTRIBUTING.md](../../CONTRIBUTING.md), Strings and Localisation).
- Move to a stable version when one is published.
