# 0055: The catalog's own printers, and the options they apply

- Status: Accepted (owner, 2026-11-23: "Accept all", for [phase-4-plan.md](../phase-4-plan.md)'s D14 and D15; built as P4-9, "Start P4-8 and P4-9")
- Date: 2026-11-23

## Context

`PrinterCatalog` handed back each print system's own `IPrinter`, with nothing of Latiprimere's between the caller and the adapter. So whatever should hold for every printer had to be written into every adapter, and two things couldn't be added after 1.0:

- **wrapping printers later would break callers.** The documented way to reach Windows features was `printer is IWindowsPrinter windowsPrinter`, which a wrapper makes false;
- **an option added later could be ignored without a word.** The adapter packages depend on `Latiprimere.Abstractions` at a version "or later", so an adapter built before an option existed, or one of someone else's, would print as if it weren't set: every page, for page ranges (P4-10).

Phase 4's plan, P4-9, with decisions D14 and D15 (owner: "Accept all"; then "Start P4-8 and P4-9").

## Decision

### Public API

- **`IPrinter.GetFeature<T>()`** and **`IPrintJob.GetFeature<T>()`,** where `T` is a class or interface, with default implementations of `this as T`. They give what a print system's printer or job offers beyond the interface, such as `IWindowsPrinter`, or `null`. So wrapped and unwrapped printers answer alike, and classes of an application's own keep compiling.
- **`PrintOptionKinds`,** a `[Flags]` enum with a value for each setting of `PrintOptions`: `JobName`, `Copies`, `Duplex`, `Orientation`, `MediaSize`, `ColorMode`, `Scaling`, `InputTray`, `VendorOptions` and `OutputFile`. It gains a value with each setting added, and says so.
- **`IPrinter.AppliedOptions`,** the settings a printer applies, whose default implementation gives every setting `PrintOptions` has in this version. A print system that applies a setting added later overrides it. This is a member of `IPrinter`, which may gain members with default implementations after 1.0, not of the frozen `IPrintSystem`, so the declaration can grow with the options and be per printer (D15).
- **`IWindowsPrinter`'s documentation** shows `printer.GetFeature<IWindowsPrinter>()` instead of the cast.

### The catalog

- **`GetPrinterAsync` and `GetDefaultPrinterAsync` wrap the printer** they find in an internal `CatalogPrinter` (D14). `GetPrintersAsync` returns `PrinterInfo`s, so it needn't.
- **`CatalogPrinter.SubmitAsync` refuses an option it shouldn't pass on:** a setting set to anything but its default that the print system's printer doesn't list in `AppliedOptions`. It throws `NotSupportedException` before anything is sent, naming the printer and the settings, as `SubmitAsync` already documents for what a printer can't do. Today every adapter applies every setting, so nothing is refused; it matters from the first option added after 1.0.
- **The jobs it returns,** from `SubmitAsync` and `GetJobAsync`, are wrapped in an internal `CatalogPrintJob`. It adds nothing yet; it is where what should hold for every job will go, such as metrics.
- **`GetFeature<T>()` on the wrapper** gives the wrapper itself for `IPrinter` and `IPrintJob`, so the checks can't be stepped round by asking for the interface, and the print system's printer or job for anything else.
- **Everything else forwards:** `Info`, `GetCapabilitiesAsync`, `GetStatusAsync` and `WatchStatusAsync`, so the Windows spooler's change notifications still drive the watches.
- **Print systems used directly** (`CupsPrintSystemFactory.Create` and the others, without a catalog) aren't wrapped. `IPrintSystem`'s documentation says what that loses; every example goes through the catalog.

### Not in this package

The tracing and metrics of plan.md's tier 1, the durable outbox and checks of options against capabilities would live in this layer; none is built here.

## Consequences

- **Breaking, before 1.0, for code that casts a catalog's printer:** `printer is IWindowsPrinter` is now false. The CLI sample, the examples, the vendor options guide and the harness's vendor option scenario use `GetFeature<IWindowsPrinter>()`. Tests that compared a catalog's printer with the print system's by reference compare `GetFeature<FakePrinter>()`.
- **A test keeps the flags and the settings together:** every writable property of `PrintOptions` must have a `PrintOptionKinds` value of its name, so an option added without one fails the build's tests rather than being impossible to refuse. Another sets every setting on a printer applying none, and checks each is named in the refusal.
- **Adding an option after 1.0, as P4-10 will before it:** add the property, its flag, its check in `CatalogPrinter`, and override `AppliedOptions` in each adapter that applies it.
- **Tests:** the catalog's printers and default printer are its own, with the print system's reached by `GetFeature`; a setting not applied refused before anything reaches the print system, with the settings and the printer named, and a setting not applied but left at its default let through; the jobs wrapped too; the default `AppliedOptions` of a class of one's own being every setting.
