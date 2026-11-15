# 0002: Multi-target .NET 8 and .NET 10 with source-generated interop

- Status: Accepted; the target frameworks are superseded by [ADR 0013](0013-target-net10-and-net11.md) (`net10.0;net11.0`). The interop decisions still stand.
- Date: 2026-11-10

## Context

The library needs P/Invoke on Windows (`winspool.drv`, GDI, `prntvpt.dll`) and possibly on Unix (`libcups`). Server and edge users benefit from trimming and Native AOT. The library should support versions below .NET 10, up to and including .NET 10.

As of 2026-11-10, .NET 8 (LTS) and .NET 9 (STS) both reach end of support on that date; .NET 10 (LTS) is supported until November 2028. Many applications will stay on .NET 8 for some time after its end of support. Going lower than .NET 8 (`netstandard2.0`, for .NET Framework 4.6.2+) was considered and rejected: it would need polyfill packages, `DllImport` fallbacks, a separate non-AOT code path and TCP-only CUPS access, and .NET Framework apps on Windows already have `System.Drawing.Printing`.

## Decision

- Every project multi-targets `net8.0;net10.0`. Apps on .NET 8, 9 and 10 are covered (.NET 9 apps consume the `net8.0` build).
- Use `LibraryImport` (source-generated marshalling, available from .NET 7) rather than `DllImport`.
- Mark library projects `IsAotCompatible` and keep them trim-safe (no reflection-based serialisation).
- Platform-specific code is guarded with `[SupportedOSPlatform]` and `OperatingSystem.IsWindows()` / `IsLinux()` / `IsMacOS()` checks so the platform analyzer catches mistakes.
- Use only APIs available in .NET 8. Where .NET 10 offers a clearly better API, use it behind `#if NET10_0_OR_GREATER` with a .NET 8 fallback, and test both builds.
- Pin the SDK with `global.json` to the .NET 10 SDK, which builds both targets.

## Consequences

- .NET Framework and .NET 6/7 apps can't use Latiprimere.
- CI runs the test suite against both target frameworks.
- When .NET 12 (next LTS) ships, review whether to drop `net8.0`.
