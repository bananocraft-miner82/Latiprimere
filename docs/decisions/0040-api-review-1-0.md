# 0040: Public API for 1.0

- Status: Accepted
- Date: 2026-11-19

## Context

1.0 is the first stable release (Phase 3 plan, decision D8). After it, breaking changes wait for a major version, and members can no longer be added freely to the public interfaces. As before 0.1 ([ADR 0018](0018-api-review-0-1.md)), the public API was reviewed first, in [api-review-1.0.md](../api-review-1.0.md). The shape from 0.1 held; seven questions were put to the owner.

## Decision

Owner decision, 2026-11-19: all seven as recommended. 11.0.6-preview was released the same day, before they were applied, and shipped names the review counted as unshipped: `RasterRotation.Anticlockwise90`, `VendorOption.MinimumValue` and `MaximumValue`, and `IppNetworkPrintSystemFactory.Create(pdfRasterizer, options)`. The renames went ahead, as decision 1's did for names already shipped; the overload stays, as removing it would break callers for nothing.

1. **.NET spellings in public names:** `MediaSize.WidthMillimeters` and `HeightMillimeters`, `PrintJobState.Canceled`, `RasterRotation.Counterclockwise90`, with the enum values' numbers kept. Identifiers are in American English, as .NET's are; prose, comments and messages stay British.
2. **Logging without dependency injection:** each factory has a public overload taking an `ILoggerFactory`: `CupsPrintSystemFactory.Create(options, loggerFactory)`, `WindowsPrintSystemFactory.Create(pdfRasterizer, options, loggerFactory)` and `IppNetworkPrintSystemFactory.Create(pdfRasterizer, options, loggerFactory)`. Their parameters are all required, so `Create()` and `Create(null)` stay unambiguous. `IppNetworkPrintSystemFactory.Create(pdfRasterizer, options)` stays beside them.
3. **`PrintCertificateException : PrintException`,** with `CertificateFingerprint` and `PrinterUri`, thrown by direct IPP for a printer certificate that isn't trusted, where it threw a `PrintException` saying the fingerprint. Both properties are nullable, as an exception's standard constructors don't set them.
4. **`IPrinter.GetJobAsync(string jobId)`,** returning `null` for a job the print system no longer knows or that isn't on that printer. IPP and CUPS ask with Get-Job-Attributes. CUPS numbers jobs across its queues, so a CUPS queue checks the job's `job-printer-uri` names it. Windows asks the spooler, and a job sent straight to a queue's IPP printer has an ID starting `ipp:`, to tell it from the spooler's job IDs, which are numbers too.
5. **`VendorOption.MinValue` and `MaxValue`;** `PrintOptions.MinimumCopies` kept.
6. **After 1.0:** new interface members get default implementations; additions go on records and options classes; the adapter-facing interfaces change only in a major version; members are made obsolete before they are removed. CONTRIBUTING.md has it, under "Changing the Public API".
7. **Job state reasons, document formats in capabilities, the print system's name and an IPP response timeout** come later, as additive changes.

## Consequences

- Every renamed member had shipped, by 11.0.6-preview, so `Latiprimere.Abstractions` has a `CompatibilitySuppressions.xml` for package validation, with them and the new `IPrinter` member, and `*REMOVED*` lines in `PublicAPI.Unshipped.txt`. The changelog lists the renames.
- `main` compares its packages with 11.0.6-preview, as the 1.0 release will. The release workflow used to leave `main` comparing with the release before the one just made, so a change to something that release first shipped passed on `main` and failed at the next release; here the baseline was moved by hand. Now the release workflow's post-release pull request moves it, with a second commit made by `tools/releasing/post-release.sh`.
- Package validation fails on a suppression it doesn't need, which this one becomes once a release has shipped with it. The same post-release commit removes the suppression files, and `anything-to-release.sh` doesn't count its changes as something to release.
- Code and test fakes that implement `IPrinter` must add `GetJobAsync`. The testing guide's `FakePrinter` shows one.
- Jobs that Windows sends straight to a queue's IPP printer report IDs like `ipp:42` rather than `42`.
- CONTRIBUTING.md records the spelling rule and the policy for after 1.0. The logging, secure printers, jobs and testing guides, the examples and the API reference pages changed with the API.
