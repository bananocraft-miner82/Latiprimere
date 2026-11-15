# 0013: Target .NET 10 and .NET 11

- Status: Accepted
- Date: 2026-11-12
- Supersedes: the target-framework part of [ADR 0002](0002-target-framework-and-interop.md). Its interop decisions (`LibraryImport`, AOT and trim compatibility, platform guards) still stand.

## Context

ADR 0002 chose `net8.0;net10.0`, so applications on .NET 8 could use Latiprimere. On 2026-11-10, .NET 8 and .NET 9 reached end of support, and .NET 11 has been released. A library tested on a runtime that gets no security fixes helps few applications that matter. Options were to keep `net8.0;net10.0`, to target `net10.0` only, or to target `net10.0;net11.0`.

## Decision

Owner decision 2026-11-12:

- **Every project multi-targets `net10.0;net11.0`**, the two supported runtimes.
- **`global.json` pins the .NET 11 SDK** (`11.0.100`, rolling forward to the latest feature band), which builds both targets.
- **CI installs the .NET 10 runtime as well**, to run the `net10.0` tests.
- **The API floor is .NET 10.** Code may use anything .NET 10 has, such as `System.Threading.Lock`, which ADR 0002 had to avoid. Use a .NET 11-only API only where it is clearly better, behind `#if NET11_0_OR_GREATER` with a .NET 10 fallback, and test both builds.
- **`LangVersion` stays at 14.0**, the C# version both targets fully support.

## Consequences

- Applications on .NET 8 or 9 can't use Latiprimere. Both runtimes are out of support.
- **Testing on both runtimes catches real differences.** The first run on .NET 11 found one: `Math.Round(104.77499999999999, 2)` gives 104.78 on .NET 10 and 104.77 on .NET 11. Converting an envelope size from inches therefore gave different millimetres on each. Paper sizes are now converted in `decimal`, which gives exactly 104.775 and rounds the same everywhere.
- Contributors need the .NET 11 SDK, and the .NET 10 runtime to run the `net10.0` tests.
- **Review when .NET 12, the next LTS, ships:** probably moving to `net10.0;net12.0`.
