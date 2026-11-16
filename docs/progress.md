# Progress Log

Newest entries first. Each entry records what was done, what was learned, and what's next. Milestone checkboxes live in [plan.md](plan.md).

## Current status

| Item | Value |
|---|---|
| Phase | 2: PDF and capabilities, begun; Phase 1's open items remain (Phase 0 decided: **Go**, 2026-11-12) |
| Released | `0.1.0-preview` on nuget.org, 2026-11-15 |
| State | Phase 1 under way. The public API for 0.1 is settled and applied ([ADR 0018](decisions/0018-api-review-0-1.md)), guarded by the public API analyzer. Windows errors now map to explained exceptions ([ADR 0019](decisions/0019-windows-error-mapping.md), proposed), and raw data to Microsoft Print to PDF is refused. CUPS servers that need a password can be used, over TLS ([ADR 0020](decisions/0020-cups-authentication.md), proposed). Integration tests against the virtual printers are wired into CI on all three runners: the CUPS container on Ubuntu, the runner's own CUPS on macOS, and the spooler test printers on Windows. The first Ubuntu run found a permissions bug in the test container, now fixed; none has passed in CI yet. Direct IPP resends jobs a busy printer turns away; IPP connections give up after 10 s and try IPv6 and IPv4 together. Still open from Phase 0: the Mac checks (Spikes 1 and 4). |
| Next step | Owner: review `feature/print-media-options`: [ADR 0025](decisions/0025-choose-paper-colour-and-tray.md) (choosing paper, colour and tray), `WindowsOptions.MaxRenderDpi`, [ADR 0026](decisions/0026-vendor-options-in-detail.md) (vendor options), the Native AOT check in CI, and direct IPP for Windows queues ([ADR 0027](decisions/0027-direct-ipp-for-windows-queues.md), accepted); then push for CI. Phase 2's checklist is done. If anyone has a Windows 11 PC with a driverless IPP queue, its `Get-Printer` and `Get-PrinterPort` output would settle ADR 0027's last open point. Still proposed and waiting for review: ADRs [0011](decisions/0011-capability-model.md), [0019](decisions/0019-windows-error-mapping.md), [0020](decisions/0020-cups-authentication.md), [0022](decisions/0022-release-process.md) and [0024](decisions/0024-known-ipp-printer-uris.md). |
| Blockers | None |

---

## 2026-11-16: Direct IPP for Windows queues, built

**Done** (owner: "Yes, build it", accepting [ADR 0027](decisions/0027-direct-ipp-for-windows-queues.md) as written; on `feature/print-media-options`)
- **ADR 0027 accepted,** with one refinement written into it before acceptance: the printer is asked whether it takes PDF at its first PDF job, not at lookup, so a lookup never waits on a printer that doesn't answer. [ADR 0026](decisions/0026-vendor-options-in-detail.md) now says `ipp:` keys reach routed jobs.
- **`WindowsOptions`:**
  - `PrintPdfOverIpp` (off by default);
  - `IppPrinterUris`, queue names (not case-sensitive) to URIs; anything but an absolute `ipp`, `ipps`, `http` or `https` URI is refused when the print system is created.

  Three lines in Windows' `PublicAPI.Unshipped.txt`.
- **`WindowsIppRoute`** (new `DirectIpp/` folder):
  - finds a queue's IPP URI: configured first, otherwise the port name when it is an absolute URL (`http` becomes `ipp`, `https` becomes `ipps`, keeping ports 80 and 443 explicit);
  - never for a print server's queue; nothing is guessed from WSD or TCP/IP ports;
  - owns the IPP client, which `WindowsPrintSystem.Dispose` now releases.
- **`WindowsPrinter`:**
  - a PDF job goes through `IppPrinter`, keeping the `windows:` printer ID, when the route is on, the URI is known, the printer lists `application/pdf`, and the job has no `OutputFile`, tray or `windows:` key;
  - the answer is kept: yes for good, "no PDF" for good, "couldn't ask" not at all;
  - a connect failure (`IppConnectionErrors.IsConnectFailure`: connection refused, name not resolved, connect timeout) rewinds the document and uses the spooler; any other failure is thrown, so nothing prints twice; a stream that can't seek isn't retried;
  - raw jobs and capabilities stay with the spooler.
- **Packages:** `Latiprimere.Windows` references `Latiprimere.Ipp`, which lets it see Ipp's internals. Packed, the nuspec lists `Latiprimere.Ipp` for both frameworks.
- **Tests:**
  - new: `IppConnectionErrors` (which failures count as never connected); `WindowsIppRoute` (URLs from ports, ports that don't name a printer, configured URIs winning and ignoring case, print servers, bad URIs, disposal); routing in `WindowsPrinter` (routed with the Windows ID and `ipp:` keys; a printer without PDF; one that can't be asked, then can; each spooler-only option; a WSD queue and a print server's; the option off; a connect failure falling back from the start of the document; a refusal thrown without the spooler; a stream that can't seek; raw jobs); the factory (off by default, a configured URI, a bad one);
  - integration: Microsoft Print to PDF's queue, mapped to the container's `ippeveprinter`, sent a PDF that `ippeveprinter` completed. Its log shows the Print-Jobs with `document-format="application/pdf"`. Without the route, the job would have gone to the driver, which needs a file name;
  - 1,480 runs locally against the container: 1,472 passed, 8 skipped (the Windows test printers), 0 failed. Release build clean; the Native AOT check passes.

**Not done**
- **Not tried with a queue added by URL,** which needs an admin to add; the port-name path is unit-tested only. The Windows CI runner is elevated but has no `ippeveprinter`, so the end-to-end test skips there.
- **Driverless Windows 11 queues** are still unexamined (ADR 0027, decision 4).

**Next**
- Owner: as in the status table.

---

## 2026-11-16: Direct IPP for Windows queues, design

**Done** (owner: "Start the direct IPP on Windows design next"; design only, no code)
- **[ADR 0027](decisions/0027-direct-ipp-for-windows-queues.md), proposed.** Four options:
  - (a) leave it to applications, which can already print to a printer's `ipp:` ID;
  - (b) route PDF jobs for known queues over IPP, inside the Windows print system;
  - (c) merge printers in the catalog;
  - (d) send PDFs through the spooler unchanged.
- **Recommended: (a) as documentation, and (b), off by default** (`WindowsOptions.PrintPdfOverIpp`). A PDF job goes straight to the printer only if all of these hold, and otherwise through the spooler as today:
  - the queue's IPP URI is known exactly, either configured in `WindowsOptions.IppPrinterUris` or because the port name is the URL;
  - the printer lists `application/pdf`;
  - the job uses nothing only the spooler can do (`OutputFile`, a tray, `windows:` keys).

  Routed jobs keep the `windows:` printer ID, and report the printer's real outcome. A connect failure falls back to the spooler. Nothing is guessed from TCP/IP or WSD ports.
- **Four questions for the owner** are in the ADR: the default, `ipp:` vendor options on routed jobs, the new dependency of `Latiprimere.Windows` on `Latiprimere.Ipp`, and output from a Windows 11 PC with a driverless IPP queue.

**Learned**
- **This PC's queues:**
  - the HP ENVY sits on a WSD port, whose name is only a local GUID; its address shows only in the queue's location, as a WSD endpoint;
  - a Standard TCP/IP port gives a host and raw port 9100, not an IPP URI;
  - no queue here uses an IPP port.
- **Microsoft doesn't document** how Windows 11 names the ports of the queues it creates for driverless IPP printers. That decides how many queues can be routed without configuration.

**Next**
- Owner: answer ADR 0027's questions. Then build it, with a CI test that adds a queue by URL in front of `ippeveprinter` on the elevated Windows runner.

---

## 2026-11-16: Trimming and Native AOT check

**Done** (owner: "Start the trimming and AOT check next", on `feature/print-media-options`)
- **Published the CLI sample with Native AOT** for `net10.0` and `net11.0` on Windows (win-x64), from clean:
  - no IL2xxx (trimming) or IL3xxx (AOT) warnings, with `TrimmerSingleWarn=false` so each would be reported on its own;
  - 16.1 MB and 15.8 MB, plus `pdfium.dll`.

  That covers every package and their dependencies (SharpIppNext, Tmds.MDns and Microsoft.Extensions.DependencyInjection), which the `IsAotCompatible` analyzers on `src/` can't see into.
- **Ran the native program for real,** both frameworks:
  - listing CUPS, Windows and IPP printers, with mDNS discovery;
  - capabilities of Microsoft Print to PDF and `ippeveprinter`;
  - A5 and monochrome to Microsoft Print to PDF, whose file has A5 MediaBoxes;
  - direct IPP with paper, tray, colour and vendor options through SharpIppNext's raw request; `print-quality` 99 refused with 1035;
  - CUPS Virtual-IPP with `ipp:` and `cups:` options, landscape;
  - raw data to `cups:Raw` byte for byte; a job on `cups:Paused` cancelled;
  - a vendor key without a prefix refused.
- **CLI sample:** `print` gained `--paper NAME`, `--color color|mono`, `--tray ID` and `--option KEY=VALUE` (repeatable). Paper and tray are looked up in the printer's capabilities, as the library expects, with a message naming the printer when it doesn't list them.
- **[tools/aot-check/check-aot.sh](../tools/aot-check/check-aot.sh):**
  - publishes the CLI with Native AOT for one framework and fails on any trimming or AOT warning;
  - then runs the native program against the machine's test printers: always `list`; CUPS Virtual-IPP when `LATIPRIMERE_TEST_CUPS_SERVER` is set; direct IPP, including a value that must be refused, when `LATIPRIMERE_TEST_IPP_PRINTER` is; and A5 to Microsoft Print to PDF on Windows, checking the MediaBoxes;
  - written for bash 3.2, as on macOS.
- **CI:** [build.yml](../.github/workflows/build.yml) runs it for both frameworks on all three runners after the tests. Linux installs clang and zlib first.
- **CONTRIBUTING.md** says when to run it.
- **Checked:**
  - the script passes locally for both frameworks against the container and this PC's spooler;
  - with a `Type.GetType(string)` call added to the CLI, the publish fails with IL2057 and the script exits 1. The repository's `TreatWarningsAsErrors` makes the AOT compiler's warnings errors, and the script's own check stays as a second guard.

**Learned**
- **The AOT compiler finds the Windows linker through `vswhere.exe`,** which Git Bash doesn't have on its path; PowerShell does. The script adds the Visual Studio Installer folder when it's there.
- **A raw job's copies on CUPS are the backend's job:** CUPS passes the count to the backend. The test container's capture backend writes the data once, so `--copies 2` to `cups:Raw` gives one copy, with or without AOT. CUPS's own `socket` backend repeats the data. Not a Latiprimere bug, but worth remembering before a test counts raw copies on CUPS.

**Not done**
- **Not run in CI yet:** the Linux and macOS publishes, and the Windows runner's toolchain, are untested until the push.
- **Not run on macOS or Linux locally.**

**Next**
- Owner: as in the status table.

---

## 2026-11-16: Vendor options

**Done** (owner: "Start the vendor options next", on `feature/print-media-options`; the shape is from [ADR 0012](decisions/0012-vendor-option-escape-hatch.md), accepted)
- **Details in [ADR 0026](decisions/0026-vendor-options-in-detail.md), proposed:** the choices are mine, for the owner to review.
- **`PrintOptions.VendorOptions`** (`IReadOnlyDictionary<string, string>`, empty by default):
  - copied when set;
  - every key must start with `ipp:`, `cups:` or `windows:` followed by a name, or it is refused with `ArgumentException`, so a mistyped prefix isn't silently ignored;
  - two lines in Abstractions' `PublicAPI.Unshipped.txt`.
- **IPP (`IppVendorAttributeMapper`):**
  - Direct IPP reads `ipp:`; CUPS reads `ipp:` and `cups:` (`IppPrintContext.VendorOptionPrefixes`).
  - Commas separate values.
  - Types are inferred: integer, or enum for the six enum attributes; `true`/`false`; keyword; otherwise name.
  - Eight Print-Job operation attributes, such as `ipp-attribute-fidelity`, go in the operation group; the rest in the job group.
  - Refused with `ArgumentException` before anything is sent:
    - a name that isn't an IPP keyword;
    - an empty value;
    - an attribute set by two keys (`ipp:x` and `cups:x`);
    - an attribute Latiprimere already sends, such as `ipp:copies` with `Copies` set, or `ipp:printer-uri`.
  - **Sending:** SharpIppNext's typed requests can't carry other attributes, so a job with vendor attributes goes through `CreateRawRequest` and `SendAsync`, and the job ID and state are read from the raw answer. Jobs without them use the typed request, as before. `IIppClient.PrintJobAsync` gained an `IppVendorAttributes` parameter, and the test fake is now internal.
- **Windows (`WindowsVendorOptions`):**
  - `windows:devmode`, in base64, replaces the driver's defaults as the DEVMODE a PDF job starts from; the typed options are applied on top, and the driver merges the result.
  - It is refused with `ArgumentException` if it isn't base64, has a public part shorter than up to `dmFormName`, or its sizes run past the data.
  - Any other `windows:` key is refused with `NotSupportedException`, for raw jobs too.
  - `WindowsCapabilityReader.ReadDefaultDevMode` now reads the driver's default DEVMODE for both the capabilities and the test.
- **Tests:**
  - new: `PrintOptions` (copying, refused keys and values); the mapper (types, enums, sets, groups, prefixes, every refusal); the raw request on the wire (groups, tags, the document after the attributes, the job read back) and a printer refusing it; `IppPrinter` passing the attributes on and refusing a clash before sending; `WindowsVendorOptions`; `WindowsPrinter` passing the DEVMODE to GDI and refusing an unsupported key for a raw job;
  - integration, container: `ippeveprinter` completes a job with `ipp:print-quality` 5 and refuses 99 up front, so the attribute reaches it; CUPS's Virtual-IPP queue completes a job with `ipp:` and `cups:` options;
  - integration, Windows: Microsoft Print to PDF's own DEVMODE, changed to A5 and passed as `windows:devmode`, gives A5 pages. With the saved DEVMODE ignored, the test fails with A4;
  - 1,396 runs locally against the container: 1,388 passed, 8 skipped (the Windows test printers), 0 failed. Release build clean.

**Learned**
- **SharpIppNext's raw path reports errors the same way as its typed one** (`IppResponseException` with the status), so the adapter's error mapping and busy retries work unchanged.
- **SharpIppNext writes repeated attributes with the same name as one attribute with several values,** giving the later ones an empty name, as IPP requires.

**Not done**
- **`windows:print-ticket`:** needs `prntvpt.dll` interop; listed in plan.md.
- **Collections, ranges and resolutions** can't be expressed as vendor values yet (ADR 0026, consequences).
- **The macOS runner** hasn't run the new IPP tests; CI will.

**Next**
- Owner: as in the status table.

---

## 2026-11-16: Windows PDF render resolution

**Done** (owner: "Yes, start it", on `feature/print-media-options`; the decision itself is from 2026-11-11, in [ADR 0009](decisions/0009-windows-pdf-printing.md))
- **`WindowsOptions.MaxRenderDpi`** (`int?`): the highest resolution PDF pages are rendered at on Windows.
  - `null`, the default, renders at the printer's resolution, as before.
  - Zero or less is refused when set (`ArgumentOutOfRangeException`).
  - Set through `AddLatiprimereWindows(options => options.MaxRenderDpi = 300)` or `WindowsPrintSystemFactory.Create(rasterizer, options)`. It reaches `GdiRasterPrinter`'s existing cap, and the factory's TODO is gone.
  - Its docs give ADR 0009's measurements, and say raw jobs aren't affected.
  - Two lines in Windows' `PublicAPI.Unshipped.txt`.
- **For tests:** an internal `GdiRasterPrinter.MaxRenderDpi`, and `WindowsPrintSystem.Gdi`.
- **Tests:**
  - new: the option's default, values and refusals; the factory and the DI setup passing it on;
  - integration: one page to Microsoft Print to PDF with a 100 DPI cap embeds only images at most 828 pixels wide, A4 at 100 DPI. With the option not passed on, the test fails with 4,957, the driver's 600 DPI;
  - 1,274 runs locally against the container: 1,266 passed, 8 skipped (the Windows test printers), 0 failed. Release build clean.

**Learned**
- **Microsoft Print to PDF embeds the bitmaps it is given unchanged,** so the render resolution shows directly in its output. One mixed-content A4 page: 1.7 MB in nine 4957-pixel strips at full resolution, 123 KB as one 826 × 1169 image at 100 DPI. ADR 0009 measured 300 DPI files 3.4× *larger* over 100 pages, so file size depends on the content; it isn't a reliable reason to lower the resolution.

**Next**
- Owner: as in the status table.

---

## 2026-11-16: Choosing paper size, colour mode and tray

**Done** (owner: "Paper, colour, tray", on `feature/print-media-options`)
- **Design in [ADR 0025](decisions/0025-choose-paper-colour-and-tray.md), proposed:** the choices are mine, for the owner to review.
- **`PrintOptions` gains three properties,** each defaulting to the printer's own setting:
  - `MediaSize`, matched by its PWG name;
  - `ColorMode`;
  - `InputTray`, matched by its ID.

  Callers pass values from `GetCapabilitiesAsync`. Six lines in Abstractions' `PublicAPI.Unshipped.txt`.
- **IPP (CUPS and direct):**
  - colour as `print-color-mode`;
  - a size alone as the `media` keyword;
  - a tray as `media-col` with `media-source`, carrying the size as `media-size` dimensions in hundredths of a millimetre when one is chosen too.

  `media` and `media-col` can't both be sent, and `media-source` exists only inside `media-col`.
- **Windows (PDF jobs):**
  - colour in `dmColor`;
  - the size's DMPAPER number, found in the driver's paper list by the same PWG name the capabilities give, in `dmPaperSize`, with `DM_PAPERLENGTH`, `DM_PAPERWIDTH` and `DM_FORMNAME` cleared, since each overrides it;
  - the tray's DMBIN number in `dmDefaultSource`.

  A size or tray the driver doesn't list is refused with `NotSupportedException` (new messages `MediaSizeNotListed` and `InputTrayNotListed`) before the PDF is opened. The driver's lists are read only when a size or tray is chosen. Raw jobs ignore all three, as they already ignore duplex.
- **Tests:**
  - new: the IPP mapping (colour, `media`, `media-col` with and without a size, three sizes in hundredths); the request as SharpIppNext encodes it on the wire; `IppPrinter` passing them on; the DEVMODE edits; finding papers and bins (rotated, duplicated and custom sizes; IDs that aren't bin numbers); `WindowsPrinter` passing the numbers to GDI and refusing unlisted ones; defaults in `PrintOptions`;
  - integration, Windows: A5 to Microsoft Print to PDF, checking every page of the PDF it writes is A5. With the paper setting switched off, the test fails with A4 pages;
  - integration, container: tray, size and colour from the capabilities complete on `ippeveprinter`, directly and through CUPS's Virtual-IPP queue. A size `ippeveprinter` doesn't support is refused up front both as `media` and, with a tray, as `media-size` inside `media-col`, so the size reaches the printer either way;
  - 1,252 runs locally against the container: 1,244 passed, 8 skipped (the Windows test printers), 0 failed. Release build clean.

**Learned**
- **SharpIppNext models `media-size` dimensions as ranges,** but sends a range with equal bounds as a plain integer (tag 0x21), which is what a request needs. A test now checks the encoding, in case a SharpIppNext update changes it.
- **`ToCapabilities` already named sizes the way a lookup needs,** so finding a driver's paper reuses its naming instead of a second table.

**Not done**
- **Not run on the macOS runner yet,** where `ippeveprinter` comes from Homebrew; CI will.
- **Not tried on a printer with real trays,** or with colour on a colour printer: Microsoft Print to PDF has neither.
- **The developer docs** at docs/site still say 0.1 can't choose these settings, which stays true until the next release.

**Next**
- Owner: review ADR 0025, then push for CI.
- Update docs/site's capabilities page when this is released.

---

## 2026-11-15: Developer documentation and examples

**Done** (owner: "Yes, write them", the ten documents proposed)
- **Ten pages in [site/](site/),** published at latiprimere.com/docs: Getting started (installation, first print job, finding printers), Guides (options and following jobs, capabilities, labels and receipts, errors and troubleshooting, a web app with a background service) and Print systems (network printers over IPP, CUPS servers and the Windows spooler), plus an overview page.
- **[samples/Latiprimere.Examples](../samples/Latiprimere.Examples/)** holds every example on those pages, one file per page, and is in the solution, so CI builds it. Its namespace is `LatiprimereExamples`, so each file needs `using Latiprimere;` as the docs do. `dotnet run -- <name>` runs an example; with no name it lists them.
- **Checked on Windows:** listing, capabilities, the error example, Print to PDF with `OutputFile` (job completed, file written), and the web example's endpoints and worker.
- **README:** a quick start, three short examples and links to the docs and examples.
- **[CHANGELOG.md](../CHANGELOG.md)** added earlier today for users; this log stays the development record.

**Learned**
- **0.1 can read paper sizes, trays and colour modes but not choose them;** the capabilities page says so. A likely request for the next API round.
- **The Windows spooler prints PDFs as page images,** so a PDF saved with Microsoft Print to PDF has no text. The docs mention it.

**Next**
- Owner: review the pages and examples, then commit `docs/site/`, `samples/Latiprimere.Examples/`, the solution file and the README.

---

## 2026-11-15: Before going public

**Done** (owner: "Make the two doc fixes for ADR 0003 and 0015")
- **Checked what would stop the repository going public and the packages being published.** The code, the package metadata and the dependencies are ready; the six `Latiprimere.*` IDs are free on nuget.org; no secrets are in the history. Two licensing questions remain for the owner:
  - **Ownership: settled.** ADR 0003 says the coding standard came from Prodigy Software Solutions, and the first names were `Prodigy.CrossPrint.*`, which looked like a possible employer claim. The owner confirmed that Prodigy Software Solutions is their own trading name, not yet registered, and the standard is their own. If a company is registered later and should own Latiprimere, the copyright has to be assigned to it in writing.
  - **The MIT commits:** the private repository's first commits hold the proof of concept under MIT. Publishing that history would make the MIT version permanent. Plan: rename the private repository to `Latiprimere-archive`, and publish a new `Latiprimere` repository from a single commit, made from `git archive main` in a fresh folder and committed with the GitHub noreply address.
- **[ADR 0003](decisions/0003-coding-standards.md):** removed "The repository is public on GitHub", which wasn't true. Added that Prodigy Software Solutions is the owner's trading name, so the standard is the owner's own.
- **[ADR 0015](decisions/0015-split-licence.md):** the consequence "the commit pushed before this change is still MIT" now says no MIT version is published, because the public repository starts from a single commit.

**Next**
- Owner: merge `feature/bug-hunt-review`; then follow the steps for the new repository.
- For the lawyer review of the CLA: let the Owner pass contributions on to a successor, such as a future Prodigy Software Solutions company. ADR 0015's new wording assumes it is published that way.

---

## 2026-11-15: Test coverage gaps

**Done** (owner: "Yes, all ten", on `feature/bug-hunt-review`)
- **Measured first:** unit-test coverage on .NET 10, with the container off, was 87.7% of lines and 82.9% of branches. Ten gaps were picked from it; afterwards it is 90.5% and 86.6%.
- **Tests added (140 more runs over both frameworks):**
  1. An invalid `CUPS_SERVER`: the print system is still created and supported, listing, lookup and the default each fail with a `PrintException` naming the variable and value, and every `InvalidServerIppClient` operation does. A configured server wins without recording the environment failure.
  2. Malformed IPP answers (cut short in the header or in an attribute) become `PrintException`s naming the printer.
  3. Reading CUPS's default queue from a successful answer: the name with and without a language, and no name at all.
  4. Adapter errors: an HTTP 401 carrying an IPP body, IPP `client-error-not-authorized` (the "not allowed" message), and SharpIppNext's `IppRequestException`.
  5. Known-URI near misses: another port, path, path case, scheme, query or host is refused without contact; the host in other case, the default port written out, a user name and an IPv6 address in other case reach the configured printer.
  6. `AnnouncementCollector` (was 0%): replacing, removing, case-insensitive keys, snapshots and the wanted-service signal; and which announcement ends a search by name.
  7. Mapping: Windows default duplex and colour values, a job only `Retained`, an undefined IPP job state, and both "no longer listed" statuses with and without a cancellation.
  8. PDF reading: limits on streams that can't seek (exactly at, one over, and within the first buffer), a seekable stream over the limit refused unread, and a stream failing partway.
  9. Windows driver answers: name slots with and without a terminating null, and paper and tray lists of different lengths.
  10. Watching a job whose printer stops answering, cancelling a catalog listing (not logged as a failure), and keeping a valid `ConnectTimeout`.
- **Small changes to make them testable:**
  - an internal `CupsPrintSystemFactory.Create(options, environmentServer)`;
  - an internal `PdfiumRasterizer.Open(content, maxDocumentBytes)`;
  - `AnnouncementCollector.Add` and `Remove`, behind the browser's event handlers;
  - `MdnsIppPrinterDiscovery.EndsSearch`;
  - `WindowsCapabilityReader.SplitNames`, `CombinePapers` and `CombineBins`;
  - `FakePrintJob.FailureAfterScript`; `FakePrintSystem`'s listing gate now honours cancellation.
- **Bug found:** a PDF from a stream that can't seek was refused when its buffer filled exactly at the size limit. The check was `>=` where `>` was meant. Unreachable with the real 2 GB limit, but fixed, and the new test fails on the old check.
- **Doc fix:** `CupsPrintSystemFactory.Create` still said an invalid `CUPS_SERVER` throws.
- **Answers that aren't readable IPP get their own messages** (owner: "Yes, fix it"). Both used to report an IPP status the printer never sent:
  - a web page (HTTP 200 with HTML), from a URI pointing at a printer's or CUPS's web interface, said "rejected with IPP status ServerErrorInternalError". It now says the address answered, but not with IPP, and may be a web interface (new resource `IppResponseNotIpp`);
  - an IPP answer cut short said "rejected with IPP status SuccessfulOk", the status of the part SharpIppNext had read. It now says Latiprimere can't read the answer (`IppResponseUnreadable`).
  - Neither sets a native error code. Tests check both messages.
- 1,174 runs locally, container off: 1,120 passed, 54 skipped, 0 failed.

**Learned**
- **SharpIppNext decodes defensively.** Truncated bodies become its `IppResponseException`, wrong value types decode to empty values, and unknown enum values pass through. No answer was found that makes our mappers throw, so the five `IppResponseErrors` catches around mapping stay untested. They are a guard, not a path.
- **SharpIppNext throws a plain `Exception` for a request without `printer-uri`.** That's a programming error, which ADR 0008 lets propagate; our requests always carry one.
- **How SharpIppNext reports answers it can't use:** a Content-Type other than `application/ipp` gives an `IppResponseException` with no response and no inner exception. A body that fails to parse gives one with the part read, whose status is the default `SuccessfulOk`, and the parsing failure as the inner exception.
- **Tmds.MDns announcements can't be built in tests** (internal setters), so the browser's event handlers and the copying of announcements remain untested.
- **Restoring a file with `mv` keeps its older timestamp,** so MSBuild may not rebuild it. Touch the file after restoring it.

**Next**
- Owner: as below.

---

## 2026-11-15: Third code review sweep, fixes

**Done** (owner: "Fix all six", on `feature/bug-hunt-review`)
1. **Announcements that don't make a valid printer URI are skipped.** Anyone on the local network can announce a printer.
   - A hostname with a space made `UriBuilder` throw, during listing and on the mDNS browser's own thread.
   - Worse, found while fixing it: on .NET 10, `UriBuilder` quietly turns `evil.example/x?`, `a@b.local` or `bad/host.local` into a URI for a different host, without the port, where .NET 11 throws.
   - `DnsSdServiceMapper` now keeps a URI only if its host and port are exactly the ones announced.
2. **libcups' `CUPS_SERVER` forms:**
   - `host[:port]/version=1.1` is accepted, and the suffix ignored.
   - `user@host` is refused: it isn't part of the form, and the user name used to be dropped silently.
   - An invalid `CUPS_SERVER` no longer stops the application starting. The CUPS print system then fails each request with a `PrintException` that names the variable (`InvalidServerIppClient`), so the catalog skips CUPS and lists the rest, as ADR 0008 intends.
   - An invalid `CupsOptions.Server`, the application's own setting, still throws.
3. **`IppNetworkOptions.DiscoveryDuration` is validated:** zero up to about 24 days, as `ConnectTimeout` allows. A negative value used to fail every listing from `Task.Delay`.
4. **Media sizes with absurd numbers are skipped:** the dimensions pattern allows six digits each side of the point, so a printer can't make `decimal.Parse` overflow.
5. **The Windows name check reads names only:** EnumPrinters level 4, from the registry, instead of level 2, which asks every connected print server. An offline server no longer holds up each lookup of a `\\server\printer` name. Adds `PrinterInfo4` and `IWinSpool.EnumeratePrinterNames`.
6. **Unreadable answers become `PrintException`s at the adapters, not in the catalog,** which ADR 0008 keeps to `PrintException`. `IppResponseErrors` turns a `FormatException`, `OverflowException` or `InvalidCastException` raised while mapping what a printer or server sent into a `PrintException` naming the source. It is used where IPP capabilities, job statuses, network printers and CUPS queues are mapped. `ArgumentException` and other programming errors still propagate.

- **Tests:**
  - new: four bad announcement hosts; libcups `/version=` and `user@host`; an invalid environment server recorded, and failing as a `PrintException`; `DiscoveryDuration` limits; two overflowing media names; the level-4 check; the unreadable-data helper (eight cases);
  - one existing test that called `printserver/version=1.1` invalid was changed, since that is now accepted;
  - 1,032 runs locally against the container: 1,024 passed, 8 skipped.
- **Checked for real:**
  - the CLI with `CUPS_SERVER=127.0.0.1:631/version=1.1` listed the container's queues; with `CUPS_SERVER=printserver:notaport` it still listed the Windows printers, where it used to fail at startup;
  - `windows:Fax,Job 1` was refused in 13 ms through the real level-4 call, and `windows:Fax` was still found.

**Learned**
- **.NET 10 and .NET 11 differ on `UriBuilder` with a host containing `/`, `@` or `?`:** 10 builds a URI for another host, 11 throws `ArgumentException`. Neither can be trusted with a hostname from the network without checking the result.

**Next**
- Owner: review [ADR 0024](decisions/0024-known-ipp-printer-uris.md) and the three review rounds on `feature/bug-hunt-review`, then push for CI.

## 2026-11-15: Second code review sweep, fixes

**Done** (owner: "Fix all six", on `feature/bug-hunt-review`)
1. **Windows PDF copies can't multiply any more.** A driver that can't make all the copies may cap the count at its maximum rather than reset it to 1. Latiprimere then rendered every copy while the driver still made its capped number of each: 150 × 99 for 150 asked of a 99-copy driver. The new `DevModeEditor.TakeOverCopies` sets the driver to one copy, before the device context is created, whenever Latiprimere makes the copies.
2. **`windows:` names reach only listed printers** (added to [ADR 0024](decisions/0024-known-ipp-printer-uris.md), proposed). A name with a backslash (another computer) or a comma (a special spooler handle) is opened only if Windows lists it, so `windows:\\attacker\x` no longer makes `OpenPrinter` contact the host and sign in to it. Plain local names are opened as before.
3. **Forgotten jobs are told apart from deleted queues.** When a job answers not-found, Latiprimere asks for the printer or queue:
   - if it still answers, the job was forgotten: completed;
   - if not, the queue is gone, as when a CUPS queue is deleted with its jobs: aborted, with a message saying how it ended isn't known.

   Last round's fix reported both as completed.
4. **PDFs are read straight into native memory:** they are held once while opening, not twice. A seekable stream sizes the buffer at once; otherwise it starts at 64 KB and doubles. Anything over 2 GB is refused with a `PrintException` (`PdfTooLarge`) instead of an `IOException`.
5. **CUPS listing sends CUPS-Get-Default and CUPS-Get-Printers together,** and waits for both.
6. **`PdfiumDocument` checks for disposal again under the PDFium lock,** so a Dispose on another thread can't close the document between the check and the render.

- **Tests:**
  - new: three for copy takeover; four for Windows names (an unlisted UNC name, a job handle and an Xcv handle are never opened; a listed connection is found); one for a deleted queue; two for reading PDFs (a stream that can't seek, past the 64 KB start, and a stream claiming over 2 GB);
  - the fake IPP client gained a failure that only job requests throw, and last round's forgotten-job tests now keep the printer answering;
  - 980 runs locally against the container: 972 passed, 8 skipped.
- **Checked for real:**
  - **Real CUPS 2.4:** a job on a disabled temporary queue read Pending, then Aborted with the new message once the queue was deleted.
  - **This PC's spooler:** `windows:\\192.0.2.1\x` gave "not found" in 16 ms, without contacting anything; Microsoft Print to PDF was still found.

**Not done**
- **Not tested with a real driver that caps copies,** since this PC has none.
- **The dispose race,** which can't be reproduced reliably in a test.

- **Remote Windows printers as an option** (owner: "can we make this an option?"):
  - **New `WindowsOptions` class** with `AllowAnyPrinterName` (default `false`). When on, names with a backslash or comma are opened without checking that Windows lists them, so applications with trusted identifiers can reach printers on servers not yet connected.
  - **How it's set:**
    - `AddLatiprimereWindows(options => options.AllowAnyPrinterName = true)`, as with the CUPS and IPP options;
    - `WindowsPrintSystemFactory.Create(rasterizer, options)`: the new parameter comes after the rasteriser, so existing calls still compile.
  - **Tests:** four new, for the option on and off, through DI and directly; 288 Windows runs pass.
  - ADR 0024 updated.

**Next**
- Owner: review ADR 0024, which now covers Windows names and the new option, then push for CI.

## 2026-11-15: Code review fixes

**Done** (owner: "Fix all ten", on `feature/bug-hunt-review`)
1. **IPP jobs a printer forgets:** printers with little job history answer not-found soon after a job finishes.
   - `IppPrintJob.GetStatusAsync` now reports such a job as completed, or as cancelled if Latiprimere cancelled it, as the Windows adapter does. Before, it threw `PrintException`.
   - `CancelAsync` returns `false` for it.
   - The message comes from a new `DisplayStrings.resx` in Latiprimere.Ipp.
2. **Windows raw jobs honour `Copies`:** the data is sent once per copy, as one page each, in the one job, rewinding the stream each time. RAW data bypasses the driver, which would otherwise make the copies. More than one copy from a stream that can't seek is refused before anything is queued (`RawCopiesNeedSeekableStream`).
3. **A failed raw job is always removed:** `SubmitRawJob` deletes the job and ends the document for any exception, not only `PrintException`. An `IOException` from the caller's stream used to leave it in the queue.
4. **Raw jobs can be cancelled while sending:** the caller's token reaches `WriteAll`, which checks it before each chunk. The job is then removed from the queue.
5. **No 100-second limit on uploads:** the IPP HTTP clients have no overall timeout.
   - Print-Job is limited only by the connect timeout and the caller's token.
   - Every other request keeps a 100-second response timeout (`SharpIppClientAdapter.DefaultResponseTimeout`), on a linked token of its own. When it runs out, the error is the same "did not answer in time" `PrintException`.
6. **`ipp:` identifiers reach only known printers** ([ADR 0024](decisions/0024-known-ipp-printer-uris.md), proposed): configured URIs, or ones announced on the network. Any other URI finds no printer without connecting. The new `IppNetworkOptions.AllowAnyPrinterUri` (off by default) brings back the old behaviour. URIs compare with port 631 filled in.
7. **DeviceCapabilities buffers have twice the headroom,** and no more than the buffer holds is used. The API takes no buffer size, so this makes an overrun from a driver whose list grows between the two calls much less likely, but can't rule it out.
8. **`WatchStatusAsync` refuses a zero poll interval,** which polled without pause. Its docs now say that a job whose state is never reported stays `Unknown` and isn't final, so callers should pass a token with a timeout.
9. **The catalog lists every print system at once,** then combines the results in registration order, with the same failure isolation. Listing takes as long as the slowest system, such as mDNS discovery, instead of the sum.
10. **CUPS asks for the default queue once** when finding the default printer: `FindDefaultPrinterAsync` passes it on to a shared `FindQueueAsync`.

- **Tests:**
  - new: three for forgotten IPP jobs; three for Windows raw copies and the token; three for the response timeout and the client timeout; four for known URIs; one for concurrent listing; the CUPS default test now counts requests; the poll-interval test covers zero;
  - `ForwardOnlyStream` moved to `Latiprimere.Testing` for both test projects;
  - 960 runs locally against the container: 952 passed, 8 skipped.
- **Checked on this PC's real spooler:**
  - two raw copies to the Microsoft XPS Document Writer's output file arrived byte for byte, and the job completed;
  - the CLI read Microsoft Print to PDF's capabilities, all 55 paper sizes, with the new buffer sizing.

**Not done**
- **Not tested on real hardware:**
  - cancelling a raw job mid-write, and a failing input stream: the unit tests can't reach the native spooler calls;
  - a slow printer taking a long upload.
- **Finding 8, only in part:** a job stuck in `Unknown` still polls until cancelled, now documented rather than changed.

**Next**
- Owner: review ADR 0024, then push for CI.

## 2026-11-15: macOS CUPS tests pass; a timing-dependent unit test fixed

**Done**
- **The fourth macOS run:** all CUPS integration tests passed on the Mac, 59 per framework, with the 10 container-only ones skipped. That includes the raw bytes reaching the capture server.
- **One unit test failed, on `net10.0` only:** `HappyEyeballsConnectorTests.GivenCallerCancelsWhenNoAddressAnswersThenThrowsAndAbandonsEveryAttempt` expected 4 connection attempts and saw 3.
  - The test cancelled after a fixed 200 ms, while the four attempts start 10 ms apart. On the busy runner the fourth hadn't started in time.
  - It's a race in the test, not a fault in the connector.
- **Fix:** the test starts connecting, waits until all four attempts have started, and only then cancels. It then checks that every attempt was abandoned.
- **Checked:**
  - the test passed 30 times in a row (15 runs, both frameworks);
  - the whole IPP test project passed 5 runs out of 5 (334 each).

**Next**
- Owner: push and re-run. Then all three runners should be green; the Windows runner's result hasn't been reported yet.

## 2026-11-15: CUPS with no default printer

**Done**
- **The third macOS run got past setup,** and the direct IPP tests passed against `ippeveprinter` on the Mac.
- **It found a library bug:** every CUPS printer lookup failed with "rejected the request with IPP status 'ClientErrorNotFound'".
  - Looking a printer up asks CUPS for its default queue (CUPS-Get-Default).
  - With no default set, as on a fresh Mac, CUPS answers `client-error-not-found`.
  - `ReadDefaultPrinterName` already treated that as "no default", but SharpIppNext throws `IppResponseException` for every error status before the response can be read, so the check never ran.
  - The container never showed this, because CUPS-PDF is its default.
- **Fix ([SharpIppClientAdapter.cs](../src/Latiprimere.Ipp/SharpIppClientAdapter.cs)):** the CUPS-Get-Default request is sent from a new `SendCupsGetDefaultAsync`, which returns "no default" for that exception when its status is not-found. Any other error status still throws.
- **Tests:**
  - two new unit tests: not-found gives no default; bad-request still throws `PrintException` with `NativeErrorCode` 0x0400;
  - 930 runs locally against the container: 922 passed, 8 skipped.
- **Checked against real CUPS 2.4:** with the container's default taken away (a temporary queue made default, then deleted), the CLI's `list` showed every queue with none marked default, and no error. The default was then set back to CUPS-PDF.

**Learned**
- **SharpIppNext throws for every IPP error status,** so a status that isn't an error for one operation has to be caught around that call.

**Next**
- Owner: push and re-run the macOS job.

## 2026-11-15: macOS raw jobs through a capture server

**Done**
- **The second macOS run settled it:** Apple's CUPS ignores `ServerBin`.
  - `cupsd -t` accepted `cups-files.conf` with `ServerBin /Library/Printers/Latiprimere/cups`, the copy and its permissions were fine, and no sandbox denial was logged.
  - Yet `cupsd` still gave its helpers `CUPS_SERVERBIN=/usr/libexec/cups`.

  So no backend can be added on a current Mac, and the `ServerBin` copy is gone.
- **[capture-server.py](../tools/macos-test-printers/capture-server.py)** takes the printer's place instead. It's a TCP server on `127.0.0.1:9101` that saves each connection to `<folder>/capture-<process id>-<number>.prn`, under a temporary name first. `Raw` and `Paused` use CUPS's own `socket` backend to reach it (`socket://127.0.0.1:9101/?snmp=false`).
- **Found by content:** the server can't know CUPS's job ID, so with `LATIPRIMERE_TEST_CUPS_SETUP=macos` the raw-bytes test finds its job's file by its content (`OutputFileWaiter.WaitForFileWithContentAsync`). The data is unique to each run. The container still checks the file name against the job ID.
- **Scripts:** the install script lost the `ServerBin` steps and their diagnostics. Its state lives in `/tmp/latiprimere-test-printers`. The remove script also cleans up after the earlier versions.
- **Docs:** the macOS README, the workflow's failure logs, CONTRIBUTING and `.gitattributes` (`*.py` LF) are updated.
- **Checked:**
  - **In the Linux container:** a raw queue on CUPS's `socket` backend, with the capture server (Python installed for the test), delivered CRLF data byte for byte, and the job completed in under a second.
  - **Rejected before that:** a second `ippeveprinter` as the capture printer. It refuses `application/octet-stream` data it can't recognise from the first bytes (CUPS 2.4.7 source and the test), yet CUPS reported the job as completed.
  - **Full suite against the container:** 926 runs, 918 passed, 8 skipped.
  - **The content lookup:** with `LATIPRIMERE_TEST_CUPS_SETUP=macos` against the container, the raw-bytes test passes, finding its file among 157.
  - shellcheck and actionlint are clean.

**Learned**
- **Apple's CUPS ignores `ServerBin`,** although its man page documents it and `cupsd -t` accepts it.
- **`ippeveprinter` can't capture arbitrary raw data:** it auto-types `application/octet-stream` and refuses what it doesn't recognise.
- **`lpadmin` refuses `socket://host:port?option`;** the option needs a path first: `socket://host:port/?option`.

**Not done**
- Not run on a Mac yet. Still untested there:
  - Apple's `socket` backend, and its sandbox, reaching `127.0.0.1:9101`;
  - `python3` on the runner;
  - Apple's PDF filters for Virtual-IPP.

**Next**
- Owner: push and re-run. If it fails, send the setup or test step's log, and the "Show macOS test printer logs" step.

## 2026-11-14: First macOS CI run

**Done**
- **The first run failed in setup:** "CUPS did not pick up the capture backend in /opt/latiprimere/cups". The CUPS log showed `cupsd` (CUPS 2.3.4, macOS 26.7.2, arm64) still starting its helpers from `/usr/libexec/cups`, so the `ServerBin` line in `cups-files.conf` hadn't taken effect.
- **Likely cause:** Apple documents `ServerBin` for macOS, and upstream CUPS has no macOS override of it. But it ignores a `ServerBin` it can't find ("does not exist"), and macOS runs `cupsd` itself in a sandbox, which most likely hides `/opt`. Not confirmed: the log excerpt didn't include the start-up messages.
- **Fix to try:** the copy of the CUPS program folder moves to `/Library/Printers/Latiprimere/cups`, where printer drivers live, so `cupsd` should be able to see it. The remove script follows.
- **Diagnostics:** the script now always prints `cupsd -t`, CUPS's own check of its configuration. If the backend still isn't picked up, it also prints:
  - `cups-files.conf`;
  - the copy's permissions;
  - the configuration messages in the CUPS log;
  - sandbox denials that mention `cupsd`;
  - the launchd job's arguments.
- **`.gitattributes`:** `*.sh` files are now always checked out with LF. On this Windows checkout (`core.autocrlf=true`), the three new scripts had come back as CRLF, which breaks them, and would break `capture-backend.sh` inside a locally built test container. The repository itself stores them as LF, so the runners weren't affected.
- **Checked:** shellcheck and actionlint are clean. Nothing ran on a Mac.

**Next**
- Owner: push and re-run. If setup fails again, send the whole "Set up macOS test printers" step log: the diagnostics will show which of the possible causes it is.

## 2026-11-14: Release commit automated

**Done**
- **[tools/releasing/prepare-release.sh](../tools/releasing/prepare-release.sh)** makes the release commit's changes (owner: "Can we automate this stage in the CI/CD?"):
  - it sets the version in Directory.Build.props;
  - it moves each `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt`, sorted, and `*REMOVED*` entries take their lines out;
  - from the second release, it sets `PackageValidationBaselineVersion` to the previous release, found as the newest `v*` tag in the history;
  - it updates the README status line.

  It refuses a version that is already tagged, or isn't a version. Each file keeps its line endings.
- **[prepare-release.yml](../.github/workflows/prepare-release.yml)** ("Prepare release", started by hand on `main` with a version):
  - it runs the script, then builds and packs;
  - it pushes `release/v<version>` and opens a pull request, with a checklist and the tag commands;
  - it starts `build.yml` on the branch. A push made with the workflow's own token starts no workflows, so `build.yml` gained a `workflow_dispatch` trigger.
- **[releasing.md](releasing.md) and [ADR 0022](decisions/0022-release-process.md)** describe the new steps. The one-time setup gains the repository setting that lets workflows open pull requests.
- **Checked on a scratch copy of the repository with its own Git history:**
  - The first release moved 305 API lines, kept CRLF, and left the baseline empty.
  - A second release after tagging `v0.1.0-preview` set the baseline to `0.1.0-preview`, added one API line and removed one through `*REMOVED*`.
  - An LF checkout, like the CI runner's, stays LF.
  - An already-tagged version and a malformed one are both refused.
  - The public API analyzer accepts the resulting files (Release build).
  - In the real repository, packing with the empty baseline still gives all 12 packages.
  - actionlint and shellcheck find nothing.

**Learned**
- **Git Bash's `grep` doesn't see carriage returns,** so a script that must keep CRLF can't detect it with grep. Comparing the file with itself after `tr -d '\r'` works on Git Bash, Linux and macOS.
- **The Visual Studio `.gitignore` ignores every folder named `release`** (`[Rr]elease/`), so the script lives in `tools/releasing/`. A first draft in `tools/release/` would never have been committed.
- **A pull request opened with `GITHUB_TOKEN` gets no `pull_request` build,** but `workflow_dispatch` runs are allowed, and their checks attach to the commit.

**Not done**
- Neither workflow has run on GitHub yet.
- The progress notes, and tagging after the merge, stay manual by design.

- **Licensing contact (owner):** "How to get one" in [COMMERCIAL.md](../COMMERCIAL.md) now gives `sales@latiprimere.com`, and says Commercial Licences aren't on sale yet. CLA.md's link to it now has an address behind it.

- **Commercial Licence price (owner decision, [ADR 0023](decisions/0023-commercial-licence-price.md)):**
  - US$250, €225 or £190 per organisation, per year, excluding VAT and sales tax;
  - it includes production use at any size, every developer and product, all updates while active, and an invoice with VAT handled.

  COMMERCIAL.md shows it in place of its last TODO.
- **Commercial Licence terms (owner decisions, also in ADR 0023), written into COMMERCIAL.md:**
  - renew within 30 days of the end date, or stop production use;
  - one licence per company in a group;
  - the buyer chooses the currency, with euros, pounds and dollars expected by region;
  - email support as well as GitHub issues;
  - refunds within 30 days;
  - sold through Paddle at latiprimere.com, where the documentation will also be.

  The price now reads "per company", to match one licence per company.
- **Checked against the preview website (main.latiprimere.dev):** price, inclusions and the VAT line match. Two differences, reported to the owner:
  - **The address:** the site's contact page gives `sales@latiprimere.com` for sales and licensing, while the repository used a separate licensing address. Owner: "Use sales@", so COMMERCIAL.md, CLA.md's link and releasing.md now use `sales@latiprimere.com`.
  - **The code sample:** the site uses `job.ID`, but the property is `Id`, so it wouldn't compile.

**Next**
- Owner: fix `job.ID` in the website's code sample, and consider "per company" there to match COMMERCIAL.md.
- Owner: also tick "Allow GitHub Actions to create and approve pull requests" during the one-time setup.
- Owner: make sure `sales@latiprimere.com` receives mail before the first package links to it.

## 2026-11-13: First preview package prepared

**Done**
- **Design in [ADR 0022](decisions/0022-release-process.md), proposed:** the choices are mine, for the owner to review. The steps are in [releasing.md](releasing.md).
- **Version:** `0.1.0-preview` for every package, set once in [Directory.Build.props](../Directory.Build.props).
- **Package contents:**
  - a README for nuget.org ([src/PACKAGE-README.md](../src/PACKAGE-README.md)): the six packages, a quick start, and the licence summary with absolute links;
  - symbols in `.snupkg` files, with Source Link to the GitHub commit;
  - deterministic builds in CI (`ContinuousIntegrationBuild`);
  - extra tags per package;
  - package validation on every pack.
- **CI:** [build.yml](../.github/workflows/build.yml) packs on the Ubuntu runner and keeps the packages for 7 days.
- **Release:** [release.yml](../.github/workflows/release.yml) runs on a `v*` tag:
  - it checks that the tag matches the version;
  - it builds, tests, packs, and pushes to nuget.org through trusted publishing (`NuGet/login`), so there's no API key to store;
  - it runs in a `release` environment that can wait for the owner's approval.
- **Accepted ADRs aren't edited:** the licence-text changes moved out of ADR 0015 into [ADR 0021](decisions/0021-licence-text-before-first-package.md).
- **Checked:**
  - Release build with `GITHUB_ACTIONS=true`, then `dotnet pack`: six packages and six symbol packages.
  - Each package has both target frameworks, XML docs, `LICENSE` and the README, and depends on the others at `0.1.0-preview`.
  - The PDBs carry Source Link to `raw.githubusercontent.com/bananocraft-miner82/Latiprimere/<commit>`.
  - The README's quick start compiles against the projects, and runs: it listed this PC's Windows printers. Its submit went to the default printer, Microsoft Print to PDF, which refused without `OutputFile` as expected; nothing printed.
  - actionlint finds nothing in either workflow.

**Learned**
- **Trusted publishing:** a nuget.org policy names the GitHub owner, repository, workflow file and (optionally) environment. The workflow needs `id-token: write`, and the key lasts one hour. A policy for a private repository is only active for 7 days until the first publish.
- **The SDK leaves `Microsoft.Extensions.DependencyInjection.Abstractions` out of the `net11.0` dependency group,** but keeps it for `net10.0` (package pruning). Worth watching once .NET 11 users install the packages.

**Not done**
- **Not published:**
  - publishing needs the owner's one-time nuget.org and GitHub setup (releasing.md);
  - it also needs a contact address in COMMERCIAL.md;
  - `release.yml` hasn't run yet.
- **The public API isn't marked shipped yet.** That's part of the release commit (releasing.md, step 3).
- **No package icon.**

**Next**
- Owner: review ADR 0022, do the one-time setup, add the contact address. Then follow releasing.md.

## 2026-11-13: Licence text revised for the first package

**Done**
- **[LICENSE](../LICENSE),** drafted at the owner's request and recorded in [ADR 0021](decisions/0021-licence-text-before-first-package.md) (accepted; first written as an update inside ADR 0015, then moved to its own ADR, since accepted ADRs aren't edited):
  - "production" is defined;
  - the open-source condition covers use in the project, and its downstream users are explained;
  - a new section says each version keeps its own licence;
  - the Commercial Licence is reached by URL.
- **[COMMERCIAL.md](../COMMERCIAL.md):** who needs a Commercial Licence now uses the same meaning of production, with examples and the open-source exception. "How to get one" is a TODO the owner must fill before publishing.
- **[CLA.md](../CLA.md):** organisations that want to contribute are sent to that address.

**Learned**
- **Only `LICENSE` goes into the packages,** so the licence must not depend on other repository files to make sense.

**Not done**
- The contact address in COMMERCIAL.md.
- Lawyer review: governing law and jurisdiction, the warranty wording, and the commercial agreement.

**Next**
- Owner: add the contact address. Then the preview package can be prepared.
- The owner confirmed the wording choices the same day ("Yes, keep them").

## 2026-11-13: Ubuntu CI fix, and integration tests on the macOS runner

**Done**
- **Ubuntu CI fix:** the first CI run with the integration tests failed the two raw-bytes checks (`Raw` and `Secure`): the files never became readable.
  - CUPS runs backends as `lp` with umask 077, so the `capture` backend's files were `-rw-------`. The tests run as the runner's user and couldn't read them.
  - The backend now sets umask 022 and replaces a leftover file rather than overwriting it, which would keep the old mode.
- **The `capture` backend's device URI names its output folder:** `capture:/` still means `/output`, and `capture:/some/folder` writes there.
- **macOS test printers** ([tools/macos-test-printers](../tools/macos-test-printers/README.md)): `install-test-printers.sh` and `remove-test-printers.sh` set up the container's Raw, Paused and Virtual-IPP queues on the Mac's own CUPS, and `ippeveprinter` on port 8631.
- **CI:** the macOS runner runs the setup script, then the CUPS and direct IPP integration tests. A failed run prints the CUPS error log and `ippeveprinter`'s log.
- **`LATIPRIMERE_TEST_CUPS_SETUP`** (`container` by default, or `macos`): on `macos`, the 10 tests of the CUPS-PDF and Secure queues, which only the container has, skip. The queue list test was split, so the Raw, Paused and Virtual-IPP part runs on macOS while the CUPS-PDF default check doesn't.
- **Tests:** 926 runs locally against the rebuilt container: 918 passed and 8 skipped (the Windows test printers, which need admin). With `LATIPRIMERE_TEST_CUPS_SETUP=macos` against the container, the 10 container-only tests skip on each framework and the rest pass.

**Learned**
- **Docker Desktop on Windows ignores file permissions on bind mounts,** so permission bugs in the container show up only on a Linux host.
- **macOS CUPS backends:**
  - macOS keeps CUPS's backends in `/usr/libexec/cups`, which System Integrity Protection makes read-only, even for root. A test backend needs a copy of that folder, set as `ServerBin` in `cups-files.conf`.
  - CUPS on macOS runs backends in a sandbox (`cupsdCreateProfile` in CUPS's `scheduler/process.c`). It allows writes under `/private/var/spool/`, `/Users/Shared/` and a few `/Library` folders, so test output goes to `/Users/Shared`.
- **`ippeveprinter`** may not be on macOS. Homebrew's `cups` formula has it, keg-only beside the system CUPS.
- **Secure can't simply move to macOS:** CUPS asks for encryption only from other machines, and the tests run on the same Mac.

**Not done**
- **Not run on a Mac at all,** since this PC can't. These are educated guesses until the first macOS CI run:
  - that macOS CUPS accepts a relocated `ServerBin`;
  - that the sandbox allows `/Users/Shared`;
  - where `ippeveprinter` comes from;
  - that Apple's PDF filters accept the test PDF for Virtual-IPP.

  Expect a round or two of fixes from the CI log.
- **Not on macOS:** CUPS-PDF and the authentication tests.

**Next**
- Owner: push and send the CI log, especially macOS and Windows.

## 2026-11-13: CUPS authentication

**Done**
- **Design in [ADR 0020](decisions/0020-cups-authentication.md), proposed:** the choices below are mine, for the owner to review.
- **`CupsOptions.Credentials`:**
  - a `NetworkCredential`, sent with every request as HTTP Basic;
  - not in answer to a challenge, so no document is uploaded twice;
  - a blank user name, or one containing `:`, is refused when the print system is created.
- **`CupsOptions.Encryption`:** `Automatic` (the default) uses TLS over TCP whenever credentials are set; also `Always` and `Never`. The local socket is never encrypted. TLS goes to `https://` on port 631, which CUPS accepts.
- **`CupsOptions.ServerCertificateValidationCallback`,** to trust a server's self-signed certificate.
- **`requesting-user-name`** defaults to the credentials' user when `UserName` isn't set.
- **New `PrintAuthenticationException : PrintException`** in Abstractions, for:
  - HTTP 401 ("needs a user name and password" / "did not accept the user name and password");
  - HTTP 426 ("…sent over an encrypted connection" / "…but encryption is turned off");
  - IPP `client-error-not-authenticated`.
- **Explained `PrintException`s:** for 403 and IPP `client-error-not-authorized` ("does not allow this user"), and for a failed TLS handshake (self-signed certificates and the callback). Before, all of these were "Could not communicate".
- **Plumbing in `Latiprimere.Ipp`,** ready for direct IPPS later: `IppConnectionSecurity`, the `IppConnectionHandler` delegating handler, and the mapping in `SharpIppClientAdapter`.
- **Fixed in passing:** `SharpIppClientAdapter.Dispose` now disposes its `HttpClient`, which SharpIppNext leaves to the caller. Each print system's connections were left to the finalizer before.
- **Test container:**
  - a `latiprimere` test account;
  - an `authenticated` policy: printing needs a valid user over TLS, as stock CUPS's `DefaultEncryption Required` asks;
  - a `Secure` raw queue using it.
- **Tests:** 35 new (70 runs).
  - Unit tests: the handler, the adapter's mapping (through real SharpIppNext with stubbed HTTP), the options, the factory's TLS choice, and the exception.
  - Six integration tests against the `Secure` queue.
  - All 916 runs pass locally against the container; 8 are skipped, the Windows test printers as before.

**Learned**
- **CUPS checks permissions only after the whole document has arrived.** It answers `Expect: 100-continue` straight away and sends 401 afterwards. So answering a challenge would upload the document twice, and a forward-only stream would be sent empty the second time. Hence credentials up front.
- **CUPS asks for TLS (426) before it asks for a password** when the request comes from another machine. Docker's network counts, even for `127.0.0.1` on the host. .NET can't do the in-place upgrade CUPS expects, but CUPS accepts TLS directly on port 631.
- **CUPS sends its refusals as HTML pages, not IPP.** SharpIppNext 5.0.2 reports them as `HttpRequestException` with the status.
- **A wrong password costs about 2 seconds per request** (PAM's delay after a failure). CUPS then treats the request as anonymous rather than refusing it outright.
- **With `Encryption = Never`, the password still reaches CUPS in clear text before CUPS answers 426.** The access log shows the user name. That's why `Never` is documented as for trusted networks only.

**Not done / not verified**
- **Credentials over the local Unix socket:** the CLI has no credential options to try it inside the container.
- **Not supported:** Kerberos (`Negotiate`) and libcups's password-free local `PeerCred`. Both are in the ADR's consequences.
- **No real print server** with a certificate from a trusted authority was tried.

**Next**
- Owner: review ADR 0020.

---

## 2026-11-13: Raw data to Microsoft Print to PDF refused

**Done**
- **Owner decision:** refuse raw data to drivers that drop it silently. Recorded in [ADR 0019](decisions/0019-windows-error-mapping.md) and [plan.md](plan.md).
- **`DriverQuirks`** lists such drivers by driver name, compared case-insensitively. It starts with "Microsoft Print To PDF". Driver names can't be renamed by users, unlike printer names.
- **`WindowsPrinter`** is now given the printer's driver name. For a raw job to a listed driver it throws `NotSupportedException` before anything is queued: "The printer '…' can't print raw data: its driver, Microsoft Print To PDF, accepts the job but writes nothing. Send a PDF instead." PDF jobs to Print to PDF are unchanged.
- **Tests (9 new):**
  - the driver table, including case and an unknown driver;
  - the refusal, with nothing reaching the fake spooler;
  - the XPS Document Writer's driver still accepting raw data;
  - an integration test against the real Microsoft Print to PDF, which checks the real driver name matches and no file is written.
- **Checked:**
  - The CLI refuses raw data to Print to PDF with that message, and writes no file. The XPS Document Writer still takes raw data.
  - Release build clean; against the container, 854 runs, 846 pass and 8 skip (the Windows test printers), two full runs in a row.

**Not done**
- **Other drivers that may drop raw data aren't known yet,** for example the OneNote drivers on this PC. Trying them would open OneNote, so they weren't tried.

---

## 2026-11-13: Windows error mapping

**Done**
- **`WindowsErrorMapper`** turns the Win32 error of a failed spooler or GDI call into the exception the contracts promise, with an explanation where Windows' text doesn't say what happened or what to do. Proposed in [ADR 0019](decisions/0019-windows-error-mapping.md).
  - **Spooler service not running** (RPC errors 1722, 1726, 1727): `PrintSystemUnavailableException`, saying how to start it. For a `\\server\printer` connection, a `PrintException` blaming the print server instead.
  - **Printer removed** (1801, 1905, 3012): `PrinterNotFoundException`, with its `PrinterId`.
  - **Driver refusing raw data** (1804): `NotSupportedException`.
  - **Explained `PrintException`s:**
    - access denied;
    - driver not installed;
    - job deleted while being sent;
    - cancelled by a driver dialog, pointing to `OutputFile`;
    - missing file or folder, pointing to `OutputFile`'s folder;
    - disk full.
  - **No error code set:** "Windows gave no reason", not Windows' "The operation completed successfully".
  - Every message keeps the failed function, the printer and Windows' own text. Every `PrintException` keeps the Win32 code in `NativeErrorCode`.
- **`SpoolerErrors`** delegates to it. The Win32 error codes moved to `Interop/Win32ErrorCodes`, with the four from `WinSpoolConstants`.
- **`PrintSystemUnavailableException`'s documentation** now covers a print system whose service isn't running.
- **Tests (20 new):**
  - 19 mapper cases, pure, so they run on every OS;
  - an integration test that prints raw data to the XPS Document Writer with `OutputFile` in a missing folder, and checks the explained message and error 3. It runs wherever that printer is installed, here included.
- **Checked:**
  - Release build clean.
  - Against the container: 836 runs, 828 pass and 8 skip (the Windows test printers), two full runs in a row.
  - The CLI now reports the missing folder as "Windows can't find a file or folder. If PrintOptions.OutputFile is set, check that its folder exists.", followed by the call, Windows' text and code 3.

**Learned**
- **v4 drivers don't refuse raw data,** contrary to what Spike 2 expected. With `OutputFile` set:
  - the XPS Document Writer wrote the raw bytes to the file unchanged;
  - Microsoft Print to PDF accepted the job and reported it Completed, but wrote an empty file. No error at all, so error mapping can't catch it. Added to [plan.md](plan.md) as a decision.
- **An `OutputFile` in a missing folder fails at once** at `StartDocPrinter`, with error 3, rather than later in the spooler.

**Not done**
- **Not observed live:** a stopped spooler, a removed printer, access denied and a driver refusing raw data. They need admin rights or another account. Unit tests only.

**Next**
- Owner: review ADR 0019; decide on raw data to Print to PDF.
- CUPS authentication.

---

## 2026-11-13: Public API for 0.1 applied

**Done**
- **Owner decisions** on the [API review](api-review-0.1.md): all nine as recommended, except `Id` instead of keeping `ID`. Recorded in [ADR 0018](decisions/0018-api-review-0-1.md); plan.md ticked, with the decisions in its resolved table and its API example rewritten against the real API.
- **Applied:**
  1. **Contracts in the `Latiprimere` namespace:** the Abstractions project's `RootNamespace` is `Latiprimere`; `using Latiprimere.Abstractions;` is gone from 83 committed files and from this session's new test files.
  2. **`Add…()` methods in `Microsoft.Extensions.DependencyInjection`,** as `Latiprimere[Cups|Ipp|Windows|Pdfium]ServiceCollectionExtensions`. Their test classes are renamed to match.
  3. **`IPdfRasterizer`, `IRasterDocument`, `PageSize` and `RasterBand`** moved to `Rendering/`, namespace `Latiprimere.Rendering`.
  4. **`IPrintJob.CancelAsync` returns `Task<bool>`,** `false` for a job that had already finished; `ICancellablePrintJob` is gone. The CLI says "had already finished, so there was nothing to cancel" instead of failing, and no longer catches `InvalidOperationException`.
  5. **`= default` on the cancellation tokens** of the public interfaces and their implementations. `WatchStatusAsync` has three overloads instead, as the analyzer requires.
  6. **`IPrintSystem : IDisposable`:** the Windows print system's `Dispose` does nothing, and `PrinterCatalog` documents that it doesn't dispose print systems.
  7. **`ID` became `Id` in every identifier:** 357 identifiers in 70 files, by a Roslyn script that renamed identifier tokens only, so "ID" in comments, messages and resource text is unchanged. Plus one resource key, and CONTRIBUTING.md's naming rule and examples.
  8. **No change for formats a printer refuses:** `NotSupportedException` stays.
  9. **`Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0** on every `src/` package: 291 public API lines across six `PublicAPI.Unshipped.txt` files, generated with `dotnet format analyzers --diagnostics RS0016`.
- **Documentation fixes from the review:**
  - thread safety on the catalog, printer and job interfaces;
  - `PrinterInfo.Id` is opaque and stable, and changes if a discovered printer is renamed;
  - `PrintContent` and seekable streams;
  - record equality on `PrinterCapabilities` and `MediaSize`;
  - disposal in the factories and `PrinterCatalog`.
- **Tests (12 new):**
  - `PageSize`;
  - `RasterBand`, including a byte count over 2 GB;
  - `PrinterCapabilities` defaults and list equality;
  - `PrinterInfo` defaults and equality;
  - `WatchStatusAsync()` with no arguments.
  - Cancel tests now expect `true` or `false`.
- **CONTRIBUTING.md:**
  - the `Id` rule;
  - the two namespace exceptions;
  - token defaults.
- **Checked:**
  - Release build clean.
  - Against the container: 796 runs, 788 pass and 8 skip (the Windows test printers), three full runs in a row.
  - The CLI lists the CUPS queues, and `print cups:Paused … --cancel` reports the cancellation and ends Cancelled.

**Learned**
- **Inside `Latiprimere.*` namespaces, a type in an enclosing namespace beats one brought in by a `using`.** Once the contracts moved into `Latiprimere`, `PrinterState` and `DocumentFormat` in the IPP test builder meant Latiprimere's types, not SharpIppNext's.
  - The compiler caught both, as the types don't convert.
  - They now use `IppPrinterState` and `IppDocumentFormat` aliases. An alias in the file's `using`s doesn't help if it has the same name, as the enclosing namespace is searched first.
- **The public API analyzer allows optional parameters only on the longest overload** (RS0027), so `WatchStatusAsync` got three overloads rather than an optional token.
- **xUnit's analyzer (xUnit1051) insists on the test's token** whenever an overload takes one. The one test of the no-token overload suppresses it on that line, with a comment.

**Next**
- When `0.1.0-preview` is published: move the `PublicAPI.Unshipped.txt` lines to `PublicAPI.Shipped.txt`, and turn on NuGet package validation against it.

---

## 2026-11-13: API review for 0.1.0-preview

**Done**
- **Reviewed the public API** before the first preview package: the 25 types in `Latiprimere.Abstractions`, and the catalog, options, factories and `Add…()` methods in the other packages. Written up in [api-review-0.1.md](api-review-0.1.md). No code changed.
- **Verdict:** the shape is sound and needs no redesign. Nine decisions for the owner, mostly about how the API feels and what is hard to change after publishing:
  1. contracts in the `Latiprimere` namespace;
  2. `Add…()` methods in `Microsoft.Extensions.DependencyInjection`;
  3. rendering contracts in `Latiprimere.Rendering`;
  4. `CancelAsync` on `IPrintJob`, returning whether it was in time;
  5. `= default` on cancellation tokens;
  6. `IPrintSystem : IDisposable`;
  7. keep `ID`;
  8. keep `NotSupportedException` for formats a printer refuses;
  9. the public API analyzer.
- **Also listed:**
  - additive changes that can wait, such as paper and colour in `PrintOptions`, why a job ended, and re-attaching to a job;
  - documentation fixes;
  - missing Abstractions tests;
  - a policy for evolving interfaces after 1.0.

**Learned**
- **An application needs seven `using`s today** to register Latiprimere and print: one per package's dependency injection namespace, plus `Latiprimere.Abstractions` and `Latiprimere`. Decisions 1 and 2 bring that down to two.
- **Cancelling a job that has just finished throws,** which callers can't avoid by checking first, as the job can finish in between. The CLI catches `InvalidOperationException` for this.
- **Applications using the factories without dependency injection have to cast to `IDisposable`** to release the HTTP client of the CUPS and direct IPP print systems.
- **The catalog, printers and jobs are already safe to use from several threads,** but nothing documents it.

**Next**
- Owner: decide the nine questions. Then one change applies them, with the documentation fixes, the missing tests and an ADR.

---

## 2026-11-13: Happy Eyeballs for IPP connections

**Done**
- **`HappyEyeballsConnector`** (new `Connections/` folder in `Latiprimere.Ipp`) follows RFC 8305:
  - it looks up the host and puts the addresses in order, alternating IPv6 and IPv4 with IPv6 first;
  - it starts the next address when the one before fails, or after 250 ms while it is still trying, with the earlier attempts still running;
  - the first connection to open wins; the others are cancelled, and any that open anyway are closed.
  - An IP address in the URI is used as it is, without a lookup.
  - If every address fails, the first address's error is thrown.
- **Wired in** as the `ConnectCallback` of the TCP HTTP client, for CUPS servers and network printers. The connect timeout still covers the whole race, as `SocketsHttpHandler` cancels the callback when it runs out.
- **Behind small interfaces,** so it can be tested without a network: `IHostResolver` (`DnsHostResolver`) and `ITcpConnector` (`TcpSocketConnector`, which sets `NoDelay` as `SocketsHttpHandler` does).
- **Tests (11 new):** address order; the first address answering; IPv6 never answering, so IPv4 starts after the delay and wins; IPv6 answering within the delay, so IPv4 never starts; a refusal starting the next address at once; every address refusing; the caller cancelling; a loser that opens after the winner being closed; an address literal; a name with no addresses. Fakes: `FakeHostResolver`, `FakeTcpConnector` and `FakeConnectionStream`.
- **Checked:**
  - The integration tests over `localhost` instead of `127.0.0.1`: each new connection now takes about 260 ms, the 250 ms delay, instead of about 2 s.
  - Through the CLI:

    | Target | Result |
    |---|---|
    | A refused port | "Could not communicate", about 2.9 s whether over `127.0.0.1` or `localhost` |
    | A dead address | "did not answer in time" at the 10 s connect timeout |
    | An unknown name | "Could not communicate", 0.6 s |

  - Release build clean; against the container, 772 runs, 764 pass and 8 skip (the Windows test printers), three full runs in a row. The Happy Eyeballs tests alone: twelve runs over both frameworks, all passing.
- **Docs:** the `localhost` notes in CONTRIBUTING.md and the container README; plan.md ticked.

**Learned**
- **Windows takes about 2 s to report a refused connection,** even to `127.0.0.1`, as it retries. Linux reports one at once.
  - That was the whole `localhost` delay: `::1` was refused slowly, and only then was IPv4 tried.
  - Racing hides it once one address answers. A port with nothing on any address still costs those 2 s, which is the operating system's behaviour, not something to work round.
- **One of my tests hung,** for a missing fake set-up rather than a bug in the code: two of the host's four addresses were left never answering, behind a 30 s attempt delay. It's a reminder that the connector waits on every address it has started until one answers, fails or the connect timeout runs out.

**Not done**
- **A real dual-stack network** hasn't been tried: the HP ENVY wasn't on the network today. Network printers found by discovery are mostly reached by IPv4 address, so they take the address-literal path anyway.

**Next**
- The `Latiprimere.Abstractions` v0.1 API review, before the first preview package.

---

## 2026-11-13: Connect timeout for IPP requests

**Done**
- **10 s connect timeout** on the TCP HTTP client that carries IPP, for CUPS servers and network printers alike. Set it with `IppNetworkOptions.ConnectTimeout` or `CupsOptions.ConnectTimeout`. Values that aren't positive, or are over `int.MaxValue` ms (the most `SocketsHttpHandler` takes), are refused when set. The Unix-socket transport to the local CUPS scheduler has none, as it connects or fails at once.
- **Timeouts become `PrintException`** with the new message "The print system at '…' did not answer in time.", keeping the original exception as the inner one (see Learned). A cancellation by the caller still comes through as `OperationCanceledException`.
- **Tests (12 new):**
  - the adapter: a connect timeout, a caller's cancellation, a refused connection;
  - the factory: valid, zero, negative and too-long timeouts;
  - both options classes: defaults and validation.
  - New fake: `FailingHttpMessageHandler`.
- **Checked:**
  - The CLI against `10.255.255.1`, which doesn't answer, fails after the 10 s timeout with the new message, for direct IPP and for CUPS (11 s, startup included). Before, it took about 21 s.
  - Release build clean; against the container, 750 runs, 742 pass and 8 skip (the Windows test printers), three full runs in a row.

**Learned**
- **When `SocketsHttpHandler.ConnectTimeout` runs out, .NET throws `TaskCanceledException` with an inner `TimeoutException`,** not the `HttpRequestException` it throws when the operating system gives up. Checked with a probe against `10.255.255.1`.
  - The adapter only translated `HttpRequestException`, so callers would have seen what looks like a cancellation.
  - It now treats an `OperationCanceledException` carrying a `TimeoutException` as a failure to reach the print system. `HttpClient.Timeout`, 100 s by default, ends the same way and is translated the same.
- **The remembered-address lookup is unaffected:** its own 3 s limit is shorter, and it already treats any `PrintException` as "search by name instead".

**Next**
- Happy Eyeballs (IPv4 and IPv6 together), still open in [plan.md](plan.md).

---

## 2026-11-13: Direct IPP resends jobs a busy printer turns away

**Done**
- **Owner decision:** direct IPP retries `server-error-busy`. Recorded in [ADR 0017](decisions/0017-retry-busy-ipp-printers.md) and [plan.md](plan.md).
- **`IppPrinter.SubmitAsync`:**
  - resends Print-Job while the printer answers busy;
  - waits 1 s before the first resend, doubling up to 15 s, as each attempt uploads the whole document;
  - sends the document from where its stream started each time;
  - throws the busy error unchanged when the time runs out.
  - Only busy answers are retried, and only for seekable streams. The cancellation token also stops the waits.
- **Setting:** `IppNetworkOptions.BusyRetryDuration`, 2 minutes by default, `TimeSpan.Zero` to turn it off, negative values refused. It reaches the printers through `IppPrintContext`. CUPS doesn't set it, so CUPS queues send once, as before.
- **Tests:**
  - **Unit (8 new):**
    - busy twice and then accepted, with the whole document sent each time from its start position;
    - retries off;
    - busy until the time runs out;
    - a stream that can't seek;
    - another rejection, not retried;
    - the option's default, zero and a negative value.
  - **Fakes:** `FakeIppClient` can answer busy a set number of times, and records each upload; a `ForwardOnlyStream` fake.
  - **Integration (1 new):** three PDFs sent to `ippeveprinter` at once all complete. Run once with retries switched off, it failed with `ServerErrorBusy` in 5 of 6 runs, so it does exercise the retry. The direct IPP test's own retry loop is gone.
- **Checked:** Release build clean; against the container, 726 runs, 718 pass and 8 skip (the Windows test printers), five full runs in a row.

**Next**
- Owner: review; the default of 2 minutes is a judgement call, so change it if you have a better figure.
- The IPP connect timeout, and possibly Happy Eyeballs.

---

## 2026-11-13: Integration tests against the virtual printers, in CI

**Done**
- **End-to-end tests** of each print system, through the catalog built with dependency injection, as applications use it. They sit in an `Integration/` folder of each test project. 20 tests (40 runs):
  - **CUPS** (`CupsPrintSystemIntegrationTests`, 9 tests), against the test container's four queues:
    - listing, with CUPS-PDF the default and Paused stopped;
    - the default printer;
    - an unknown queue;
    - CUPS-PDF's capabilities;
    - a raw job completing, and its bytes arriving unchanged in `/output/Raw-<job-id>.prn`;
    - a PDF, 2 copies, to CUPS-PDF;
    - a landscape PDF to the IPP Everywhere queue;
    - a job on the Paused queue cancelled.
  - **Direct IPP** (`IppNetworkPrintSystemIntegrationTests`, 6 tests), against `ippeveprinter`:
    - listing a configured URI;
    - make and model;
    - an unknown path;
    - capabilities;
    - a PDF completing;
    - duplex refused up front with status 0x040B.
  - **Windows** (`WindowsPrintSystemIntegrationTests`, 5 tests):
    - against the printers from `Install-TestPrinters.ps1`: listing, with Paused stopped; raw bytes arriving unchanged on the file port; a job kept by "Keep printed documents" ending Completed; a job on the paused printer cancelled;
    - a PDF through PDFium and GDI to Microsoft Print to PDF, end to end through the catalog.
- **Switched on by environment variables.** Each test is skipped unless its `LATIPRIMERE_TEST_*` variable says where its printers are. Once the variable is set, a printer that can't be reached fails the test, so CI can't pass by skipping. The variables are listed in CONTRIBUTING.md.
- **CI** ([build.yml](../.github/workflows/build.yml)):
  - Ubuntu starts the CUPS container (`docker compose up --wait`), mounts its output on the runner, and sets the CUPS and IPP variables. It prints the container's logs if a step fails.
  - Windows starts the spooler, runs `Install-TestPrinters.ps1` (the runner is elevated), and sets the Windows variable. This replaces the Windows end-to-end check deferred since Spike 2 (go/no-go condition 3).
  - macOS: unchanged. Its runners have no Docker.
- **`tests/Latiprimere.Testing`**, a new shared test library:
  - `IntegrationEnvironment` reads the variables;
  - `CatalogFixture` builds the catalog;
  - `PrintJobWaiter` and `OutputFileWaiter` wait for jobs and output files;
  - `TestDocuments` moved here from the PDFium tests, which now use this library.
- **Test container** ([tools/virtual-printers](../tools/virtual-printers/README.md)):
  - a health check, so `up --wait` waits for the queues;
  - `/output` as a volume, mounted on the host when `LATIPRIMERE_TEST_CUPS_OUTPUT` is set;
  - `ippeveprinter -c /bin/true` (see Learned).
- **`PrintToPdfLock` became `PrinterLock`,** for any printer, and is now a named semaphore (see Learned).
- **Docs:** CONTRIBUTING.md (where integration tests go, the variables, running them locally); the container README. Two rename slips fixed: ADR 0016 is "Rename CrossPrint to Latiprimere" in the ADR index and plan.md. `/out/` added to `.gitignore`.
- **Checked:**
  - Release build clean.
  - Against the container: 708 runs, 700 pass, and the 8 Windows test-printer runs skip because those printers aren't installed on this PC.
  - Six full runs in a row all passed, about 13 s each.
  - Without the variables, the 38 runs that need them skip and the rest pass.

**Learned**
- **`ippeveprinter` takes one job at a time and turns others away with `server-error-busy` (0x0507).**
  - The test processes for both frameworks printing at once hit it straight away.
  - CUPS retries a busy printer by itself; Latiprimere's direct IPP path passes the error to the caller.
  - The direct IPP test retries for now. Whether the library should retry became a decision in [plan.md](plan.md); decided the same day, see the entry above.
- **`ippeveprinter` fakes printing with a random 5–15 s per job.** Jobs queued behind each other for up to 55 s. `-c /bin/true` makes each job finish at once.
- **On Windows, `localhost` costs about 2 s per connection to a server listening on IPv4 only.** .NET tries `::1` first, and only then `127.0.0.1`.
  - The tests use `127.0.0.1`.
  - The same delay hits applications that name a host by a name with both kinds of address. Added to [plan.md](plan.md) Phase 1 alongside the connect timeout.
- **A `Mutex` can't guard async tests.** Only the thread that took it may release it, and an `await` can resume on another thread. The release failed, the lock stayed held, and other Print to PDF tests waited 2 minutes. A named semaphore has no thread affinity.

**Not done**
- **Not run in CI yet.** The Windows runner's part is untested anywhere: this PC doesn't have the test printers installed, which needs admin. Possible snags:
  - the runner image may lack the "Generic / Text Only" driver;
  - the runner image may lack Microsoft Print to PDF; that test then skips.
- **macOS:** no integration tests in CI. Its runners have no Docker, so it would need a queue on the runner's own CUPS. Worth working out together with the Mac manual checks.

**Next**
- Owner: review and push; check the first CI run, especially the Windows runner's test printers.
- Decide whether direct IPP retries `server-error-busy`.
- The IPP connect timeout, and possibly Happy Eyeballs, together.

---

## 2026-11-12: Split licence, and the new name

**Done**
- **Licence (owner decision):** a split licence replaces MIT ([ADR 0015](decisions/0015-split-licence.md)).
  - [LICENSE](../LICENSE): the Community Licence is free for:
    - organisations under US$1M annual revenue;
    - non-profits and education;
    - open source;
    - evaluation and development;
    - indirect use.

    Everyone else needs a Commercial Licence. Copyright is held by Jonathan Price.
  - [COMMERCIAL.md](../COMMERCIAL.md): who needs a Commercial Licence. What it includes, the prices and how to buy are left as TODOs for the owner.
  - [CLA.md](../CLA.md): a contributor licence agreement, so contributions can be licensed under both licences.
  - All three are drafts for a lawyer to review.
- **Packages:**
  - They carry the licence file (`PackageLicenseFile`), and `Copyright` is set.
  - Checked with `dotnet pack` of `Latiprimere.Abstractions`: the package contains `LICENSE`, and its nuspec has `<license type="file">`.
- **README:** the status line now says Phase 1, and a Licence section was added. CONTRIBUTING.md asks contributors to sign the CLA.
- **Name:** whether to rename before the first release is now an open question in [plan.md](plan.md).
  - About 90 names were checked for free domains, GitHub names and NuGet IDs.
  - **Owner decision: rename to Latiprimere** ("to print broadly", after Star Trek's "to boldly go"). Recorded in [ADR 0016](decisions/0016-rename-to-latiprimere.md). Then renamed throughout (owner: "rename all CrossPrint references"):
    - **Renamed:** the solution, every project folder and `.csproj`, the namespaces, package IDs and `Add…()` methods, the CLI, the `LATIPRIMERE_IPP_PRINTERS` variable, and the test container's queue and image names.
    - **The ippeveprinter capture:** its make, model and device ID are re-encoded with the new name and correct IPP value lengths.
    - **Docs:** all of them, including past progress-log entries and ADRs. The old name stays only where the docs discuss the name itself (ADR 0004, ADR 0016, the 2026-11-10 naming entries) and in mentions of the separate "Cross Print" service.
    - **Package metadata:** the URLs point to `github.com/bananocraft-miner82/Latiprimere`.
    - **Checked:** Release build clean; 668 test runs pass; `caps` against Microsoft Print to PDF works.

**Learned**
- **The repository is public, and the first pushed commit carries the MIT licence.** That commit stays MIT for anyone who copied it.
- **Short `.com` names with "print" or "spool" in them are almost all registered.** `.dev` and `.io` are mostly free.

**Next**
- Owner:
  - fill in the TODOs in COMMERCIAL.md;
  - have the licence texts reviewed;
  - set up the CLA Assistant bot before accepting outside contributions;
  - register `latiprimere.com` and `latiprimere.dev` before the rename is pushed;
  - rename the GitHub repository to `Latiprimere` (the package URLs already point there);
  - open `Latiprimere.slnx` in Visual Studio in place of the old solution;
  - rebuild the test container (`docker compose build`), since its queue names changed;
  - search the trademark registers.

---

## 2026-11-12: Layout and duplication rules

**Done**
- **New rules in CONTRIBUTING.md (owner request):**
  - A blank line before a `return`, a block statement, or any statement of three or more lines, unless it comes first in its block.
  - Lines up to 144 characters, up from 120; `.editorconfig` guides moved to match.
  - Multi-line argument and parameter lists put `(` and `)` on their own lines, like braces.
  - One type per file, named after the type, nested types included. StyleCop SA1402 and SA1649 now enforce this for top-level types.
  - No lambdas stored in fields, properties or locals in place of a method.
  - Shared code goes in one extension method, helper or builder.
- **Code swept to match:**
  - A Roslyn script applied the blank-line and parenthesis rules to all projects: 142 lists moved and 83 blank lines added, in tests and the few main-project places still left.
  - Lines over 144 characters in the main projects were wrapped.
  - The extra types in `WindowsCapabilitiesRecord.cs` and the nested `LocatedPrinter`, `BandBuffer` and `DefaultSettings` now have their own files.
  - The socket-check lambdas in `CupsEndpointTests` became methods.
- **Duplication removed:**
  - The resource generator's format methods replace all 20 `string.Format(CultureInfo.CurrentCulture, ...)` calls on resource messages ([ADR 0014](decisions/0014-resx-source-generator.md) updated).
  - A shared `IppPrintContextBuilder` in `Latiprimere.Ipp.Testing` replaces five copies of the same set-up in the IPP and CUPS tests.
- **Fixed:** an in-progress edit had turned `?? throw` into `? throw` in `WindowsPrinter.SubmitPdfAsync`, which broke the build.
- **Checked:** build clean; 668 test runs pass on .NET 10 and 11.

**Learned**
- **The project's naming rules treat local functions as locals,** so they are camelCase.

---

## 2026-11-12: Resource classes from a source generator

**Done**
- **The owner's build error:** `CapabilitiesCommand.cs` had lost its `using Latiprimere.Cli.Resources;`. Visual Studio's Remove and Sort Usings had treated it as unnecessary, because the editor couldn't see `CliMessages`, which the build generated into `obj/`. The `using` is restored.
- **Root cause fixed (owner decision):**
  - All seven projects with `.resx` files now use `Microsoft.CodeAnalysis.ResxSourceGenerator` (3.11.0-beta1.26226.5, prerelease, build-time only) instead of `StronglyTyped*` metadata.
  - Class names and namespaces are unchanged, and no code changed.
  - Recorded in [ADR 0014](decisions/0014-resx-source-generator.md), which supersedes one line of ADR 0005.
- **CONTRIBUTING.md:** says how a new project gets its resource class.
- **Checked:**
  - Build clean, no designer files written.
  - 668 test runs pass; some assert resource text, so the messages load at runtime.
  - The CLI runs on both frameworks.
  - `dotnet pack` of `Latiprimere.Ipp`: the generator isn't a package dependency.

**Learned**
- **Visual Studio doesn't reliably see C# files a build target writes into `obj/`.** Code clean-up can then remove the `using` that refers to them.
- **On `net11.0`, `Microsoft.Extensions.DependencyInjection.Abstractions` is part of the shared framework.** The SDK drops the package dependency from that framework's group in the package, which is expected.

**Next**
- Owner: reload the solution in Visual Studio if the red underlines persist, then push for the first CI run.

---

## 2026-11-12: Targets moved to .NET 10 and .NET 11

**Done**
- **Owner decision:** target `net10.0;net11.0`. Recorded in [ADR 0013](decisions/0013-target-net10-and-net11.md), which supersedes ADR 0002's target frameworks.
- **Build setup:**
  - `Directory.Build.props` targets `net10.0;net11.0`.
  - `global.json` pins the .NET 11 SDK (11.0.100).
  - The CI workflow installs the .NET 10 runtime for the `net10.0` tests.
- **No new diagnostics:** the .NET 11 SDK's analyzers found nothing; Debug and Release build clean.
- **`System.Threading.Lock`** now guards PDFium and the mDNS announcement collector, replacing the plain objects .NET 8 needed.
- **Paper sizes are converted in `decimal`** (see Learned).
- **Manual tests** use `-f net11.0`, so the Mac needs only the .NET 11 SDK.
- **Tests:** 334 tests (668 runs over `net10.0` and `net11.0`), all passing.
- **Native AOT:** the CLI publishes and runs on both frameworks (16.8 MB on .NET 10, 16.5 MB on .NET 11). The .NET 11 build read the HP's capabilities over IPP.

**Learned**
- **.NET 11 rounds differently from .NET 10.** `Math.Round(104.77499999999999, 2)`, which is 4.125 in × 25.4 in `double`, gives 104.78 on .NET 10 and 104.77 on .NET 11. That made a Number 10 envelope 0.01 mm narrower on .NET 11. Converting in `decimal` gives exactly 104.775 and rounds the same on both. This alone justifies testing both runtimes.
- **The HP stopped answering mDNS while still answering IPP.** Later in the day it didn't answer discovery at all, even over 10 s, while its IPP port still answered. It had reported `inPowerSave` earlier.
  - Within one process, the remembered address still finds it.
  - After a restart, a name lookup would fail until it wakes.
  - Added to [plan.md](plan.md) Phase 3: optionally persist remembered addresses.

**Next**
- Owner: push for the first CI run; review ADR 0011.

---

## 2026-11-12: Go; .NET update and the HP ENVY over Wi-Fi

**Done**
- **Go/no-go: Go.** Owner decision 2026-11-12, recorded in [go-no-go.md](go-no-go.md) and [plan.md](plan.md).
- **.NET:**
  - SDK 10.0.404, with runtimes 8.0.34 and 10.0.15; the .NET 11 SDK and runtime are also installed.
  - A clean Release build has no warnings, and all tests pass.
  - **The Native AOT publish of the CLI now passes NuGet's audit,** with no warnings, so condition 5 is met. The exe is 16.8 MB. It lists printers, and discovers network printers over mDNS under AOT.
- **HP ENVY 5640 over Wi-Fi**, read-only throughout; nothing was printed:
  - **Discovery:** found as `ipp:dnssd:HP ENVY 5640 series [<id>]`.
  - **Capabilities by name:** `caps` takes 0.66 s end to end, including resolving the name. Results:
    - 26 sizes, Letter default;
    - all three duplex modes;
    - monochrome and colour;
    - up to 99 copies;
    - portrait only;
    - trays: main, photo, auto.
  - **Name lookups, in one process:**
    - the first, which searches the network, took 1.0–1.2 s;
    - later ones, using the remembered address, took 75–90 ms.

    This is the IP-first behaviour of ADR 0010 on a real network.
  - **Validate-Job** checks a job without creating it:
    - `application/octet-stream`: accepted.
    - PDF, alone or with long-edge duplex: `client-error-document-format-not-supported` (0x040A).
    - CUPS-raw: the same.
  - **Attributes** captured, and the identifiers replaced. They are stored as `tests/Latiprimere.Ipp.Tests/Data/hp-envy-5640-printer-attributes.ipp`, with a [README](../tests/Latiprimere.Ipp.Tests/Data/README.md) on where each capture came from and how to clean one.
- **Display names:** a size name that starts with a digit keeps its case ("Index 4x6", not "Index 4X6").
- **Tests:** 334 tests (668 runs), all passing. New ones map the HP capture, and check it doesn't list PDF.

**Learned**
- **This HP doesn't accept PDF over IPP.** It takes PCL, JPEG, PCLm, Apple raster (`image/urf`) and octet-stream.
  - Latiprimere's format check refuses a PDF before sending, with the list of accepted formats.
  - Printing PDFs straight to inkjets like this needs PDF rendered to Apple raster or PCLm. That Phase 3 item matters more than expected; added to [plan.md](plan.md).
- **The printer and its Windows driver describe it differently:**

  | | IPP | Windows driver |
  |---|---|---|
  | Default paper | Letter | A4 |
  | Copies | up to 99 | up to 9999 |
  | Orientations | portrait only | portrait and landscape (the driver rotates) |
  | Colour default | not reported | colour |

  Both are right for their path, so capabilities belong to the print path, not to the device.
- **The printer advertises that its Wi-Fi name and password can be set over IPP** (`printer-settable-attributes-supported`). Latiprimere never sets printer attributes. Applications should know that IPP access to a printer can be powerful.
- **Real captures carry identifiers:** the Wi-Fi name, LAN address, MAC address (inside `printer-uuid`, and its suffix in the printer name) and serial number (in `printer-device-id`). They must be replaced before a capture goes into the repo.
- **Stale test announcements:**
  - **What:** five "Latiprimere Loopback Test (n)" announcements from the Spike 4 helper are still being answered.
  - **Why:** `DnsServiceDeRegister` is asynchronous, and the helper exited before it finished.
  - **Who can see them:** only on the WSL virtual adapter, so not on the Wi-Fi network.
  - **Fix:** a reboot clears them.
- **.NET 8 is out of support since 2026-11-10,** and .NET 11 is out. That questions the `net8.0` target in ADR 0002, so it's added as an open question in [plan.md](plan.md).

**Not done**
- Printing on the HP: not attempted; it needs your approval, and PDF needs raster rendering first.
- IPPS to the HP: not tried; the printer offers `ipps://…:443`.

**Next**
- Owner: push for the first CI run; decide the target-framework question; review ADR 0011.
- Phase 1, starting with CI integration tests against the virtual printers.

---

## 2026-11-11: Go/no-go review drafted

**Done**
- Drafted the Phase 0 review in [go-no-go.md](go-no-go.md): each spike's result against its "done when", the poc-plan criteria, conditions, re-assessed risks, and where Phase 1 should start.
- **Recommendation: Go, with conditions.** Spikes 3 and 5 are met in full. Spikes 0, 1, 2 and 4 are met in part, each waiting on a check this PC can't run: CI, the Mac, the Windows spooler end to end, a real network printer. No route failed.

**Next**
- Owner: decide go/no-go; work through the conditions, starting with the first CI run.

---

## 2026-11-11: ADR 0012 accepted

**Done**
- Owner accepted option (c) for vendor-specific settings: a dictionary of `ipp:`, `cups:` and `windows:` settings on `PrintOptions`, with typed options added over time. Recorded in [ADR 0012](decisions/0012-vendor-option-escape-hatch.md), [plan.md](plan.md) (resolved table and the Phase 2 item).

**Next**
- Owner: review [ADR 0011](decisions/0011-capability-model.md).
- Go/no-go review of Phase 0.

---

## 2026-11-11: Spike 5 — capability model

**Done**
- **Abstractions:**
  - `IPrinter.GetCapabilitiesAsync()` returns a `PrinterCapabilities` record: paper sizes, duplex modes and colour modes (each with its default), most copies, orientations and input trays.
  - New `MediaSize`, `ColorMode` and `InputTray` types.
  - Empty means "not reported".
- **Paper names:** sizes are named by PWG self-describing name everywhere, for example `iso_a4_210x297mm`.
- **IPP** (CUPS queues and direct printers):
  - `PwgMediaNames` parses names into sizes and display names.
  - `IppCapabilitiesMapper` maps sides, colour (with fallbacks), copies, orientation and media sources.
  - `IppPrinter.GetCapabilitiesAsync` asks only for those attributes.
- **Windows:**
  - Reads the driver's capabilities with `DeviceCapabilitiesW`, through the new `IWinSpool.GetCapabilities` and `WindowsCapabilityReader`, and its defaults from the default DEVMODE.
  - `WindowsCapabilitiesMapper` names sizes through a table of 27 common sizes (`StandardMediaSizes`) and turns them to portrait.
- **CLI:** `caps <printer-id>` shows a printer's capabilities, marking the defaults.
- **Test data:** raw Get-Printer-Attributes responses captured from the four test-container printers, in `tests/Latiprimere.Ipp.Tests/Data/*.ipp`. `ReplayHttpMessageHandler` runs them through the real IPP decoder. The captures were checked: they contain only `localhost` URIs and the test user name.
- **Tests:** 331 tests (662 runs), all passing. New ones cover:
  - PWG name parsing;
  - the IPP mapper over all four captures;
  - edge cases: missing attributes, colour fallbacks, values with no equivalent;
  - the Windows mapper: standard, rotated and custom sizes, and defaults;
  - both printers' `GetCapabilitiesAsync`;
  - a read-only check of the real Microsoft Print to PDF driver.
- Design in [ADR 0011](decisions/0011-capability-model.md) (Proposed); escape hatch proposed in [ADR 0012](decisions/0012-vendor-option-escape-hatch.md).

**Verified** with `caps`, all read-only:
- **Microsoft Print to PDF:** 54 sizes, A4 default; one-sided; colour default; copies "up to 1"; portrait and landscape; no trays.
- **HP ENVY 5640** (Windows driver, printer switched off):
  - Letter, A4 (default), envelopes and photo sizes;
  - all three duplex modes, one-sided default;
  - colour default; up to 9999 copies;
  - trays: Automatically Select (15), Main Tray (258), Photo Tray (257).
- **CUPS:**
  - `Virtual-IPP`: same as the printer behind it.
  - `CUPS-PDF`: 61 sizes, A4 default, colour.
  - `Raw`: almost nothing reported, as expected without a driver.
- **Direct IPP** (`ippeveprinter`): 5 sizes, Letter default, one-sided, monochrome, up to 999 copies, four trays.

**Learned**
- **The same paper can be compared across print systems by name:** A4 is `iso_a4_210x297mm` from the HP driver, CUPS and the IPP printer alike.
- **Windows drivers use their own bin numbers** (HP: 257, 258), so tray IDs only mean something to the printer that reported them.
- **Some common sizes weren't in the first table:**
  - Envelope #9, Photo L and Chou #3 showed up as `custom_` names on real drivers;
  - added to the table, they now get standard names;
  - expect the table to grow.
- **Copies on Windows report the driver's limit.** Microsoft Print to PDF says 1, though Latiprimere renders extra copies of PDF jobs itself.
- **The raw IPP captures decoded cleanly** through SharpIppNext with a replaying HTTP handler, so tests can cover real printer responses without a network.

**Not done**
- `PrintOptions` can't choose paper size, colour mode or tray yet (Phase 2).
- Media types, margins, custom size ranges, quality and finishing aren't modelled; see ADR 0011.
- No real network printer's attributes yet: the HP ENVY over IPP is still pending until it is switched on.

**Next**
- Owner: review ADRs 0011 and 0012.
- Go/no-go review of Phase 0.

---

## 2026-11-11: ADR 0010 accepted; known address first for name lookups

**Done**
- Owner accepted [ADR 0010](decisions/0010-direct-ipp-adapter-design.md), with one change: a lookup by name tries the known address first, and searches by name only if that address doesn't answer.
- **`IppNetworkPrintSystem`:**
  - Remembers, in memory, the address and announced UUID last seen for each name, from listings and lookups.
  - A lookup by name tries that address first, with a 3 s limit (`KnownAddressTimeout`). It uses the answer only if the printer's `printer-uuid` matches the announced UUID, or either is missing.
  - Otherwise it searches the network, then remembers the address found.
- **UUIDs:** `DiscoveredIppPrinter.Uuid` comes from the `UUID` TXT key, compared case-insensitively and without a `urn:uuid:` prefix. `printer-uuid` is now requested from network printers.
- **Fakes:** `FakeIppClient` can make an address unreachable or unresponsive; `FakeIppPrinterDiscovery` takes a UUID and can forget printers; `PrinterAttributesBuilder.WithUuid`.
- **Tests:** 303 tests (606 runs), all passing. New ones cover the remembered address being used, a moved printer, a different printer at the old address, an unreachable or unresponsive old address, a printer that has gone, and UUID handling.

**Learned**
- The default `HttpClient` has no connect timeout, so an address with nothing behind it can take about 21 s on Windows to fail. Hence the 3 s limit on the remembered address. Ordinary lookups by URI still have no such limit; worth adding a connect timeout to `IppHttpClientFactory` in Phase 1.

**Not done**
- The remembered-address path is covered by unit tests only. The test announcement's address can't reach the container, so it can't succeed live here.

**Next**
- Spike 5: capability model.

---

## 2026-11-11: Name-based IDs for discovered network printers

**Done**
- Owner decision: identify discovered printers by DNS-SD name, option (c). Recorded in [plan.md](plan.md) and [ADR 0010](decisions/0010-direct-ipp-adapter-design.md).
- **IDs:**
  - Discovered printers are listed as `ipp:dnssd:<name>`.
  - Configured printers keep `ipp:<uri>`. If one also announces itself, it is listed once, with the announced name and details.
- **Lookups by name** search the network each time, through the new `IIppPrinterDiscovery.ResolveAsync`, and stop as soon as the printer answers over IPP. A name that doesn't answer gives `null` (`PrinterNotFoundException` from the catalog).
- **Names keep their listed form:** a printer found by name keeps the name it was listed under, not its own `printer-name`.
- **Docs:** CLI usage text, `IppNetworkOptions` documentation and the [Spike 4 manual test](manual-tests/spike-4-network.md) updated.
- **Tests:** 291 tests (582 runs), all passing. New ones cover:
  - parsing both ID forms;
  - listing with discovered and configured printers;
  - lookups by name: found, case-insensitive, and not answering;
  - picking an announcement by name.

**Verified** with a test announcement through Windows DNS-SD:
- `list` shows `ipp:dnssd:Latiprimere Loopback Test`.
- `print "ipp:dnssd:latiprimere loopback test" …` resolved the name to the announced address. As before, the container can't be reached at that address, so the job itself wasn't sent.
- **Timing, with a 10 s timeout:** a name that answers resolved in 0.18 s; an unknown name took the full 10.07 s.

**Learned**
- Stopping at the first answer keeps lookups by name cheap. The cost of option (c) falls only on names that don't answer.
- **Renames happen in practice.** Announcing the test printer a second time, while Windows still remembered the first, made Windows rename it "Latiprimere Loopback Test(1)", which gave it a new ID. As ADR 0010 notes, applications that need an ID that never changes should configure the printer by URI.

**Next**
- As for Spike 4 below.

---

## 2026-11-11: Spike 4 — direct IPP to network printers

**Done**
- **`IppNetworkPrintSystem`** in `Latiprimere.Ipp`:
  - Printer IDs are `ipp:` plus the printer URI.
  - Lists discovered printers plus configured URIs, without contacting them.
  - A lookup reads the printer's attributes, including `document-format-supported`.
  - No default printer.
  - Registered with `AddLatiprimereIpp()` or created with `IppNetworkPrintSystemFactory.Create()`, configured through `IppNetworkOptions`.
- **Discovery** with Tmds.MDns 0.10.1:
  - Browses `_ipp._tcp` and `_ipps._tcp`.
  - Maps the TXT keys `rp`, `ty`, `note` and `pdl`.
  - Prefers an IPv4 address, and IPP over IPPS.
  - Drops renamed duplicates.
- **`IppPrinter` rejects formats the printer doesn't accept** before sending, when its formats are known.
- `IppPrinterQueries` now holds the Get-Printer-Attributes call shared with the CUPS adapter.
- **CLI:** registers the IPP print system. `LATIPRIMERE_IPP_PRINTERS` takes comma-separated printer URIs.
- **Tests:** 281 tests (562 runs), all passing. The 29 new tests cover:
  - the DNS-SD mapper and de-duplication;
  - the network print system over a fake discovery and the fake IPP client;
  - the format check, the factory and DI.
- **Flaky test fixed:** the Microsoft Print to PDF integration tests hold a cross-process lock (`PrintToPdfLock`), and its wait is back to 30 s. The cause was concurrent printing (see Learned).
- Design recorded in [ADR 0010](decisions/0010-direct-ipp-adapter-design.md) (Proposed).
- **TODO** in `WindowsPrintSystemFactory` points at the Phase 2 item for a configurable render resolution.

**Verified** with the CLI against `ippeveprinter` in the test container (`LATIPRIMERE_IPP_PRINTERS=ipp://localhost:8631/ipp/print`):
- `list` shows the configured printer.
- A PDF prints to `Completed`, with the printer's status messages along the way.
- `--duplex long` is rejected up front by the one-sided printer (status 1035).
- Raw data is rejected (status 1035).
- An unknown path gives `PrinterNotFoundException`; an unreachable host gives a connection `PrintException`.
- **Discovery end to end:** a test announcement registered through Windows DNS-SD on the WSL network adapter was found, with its TXT keys mapped and its duplicates dropped.

**Learned**
- **Direct printers check jobs up front; CUPS doesn't.** CUPS accepts unsupported options and cancels the job later. A printer rejects them immediately with 1035, which is easier for callers.
- **Tmds.MDns pitfalls:**
  - `StopBrowse()` reports every service as removed, so the first version always found nothing. Handlers are now detached before stopping.
  - `StopBrowse()` also changes the announcement objects, so they are copied when each event arrives.
- **Stale renamed announcements** ("Name (1)" to "(4)") all pointed at the same URI; de-duplicating by URI handles them.
- **`ippeveprinter` lists `application/octet-stream` only to mean "detect the format",** so it rejects raw data it doesn't recognise. Raw printing to network printers has to be checked printer by printer.
- **The loopback interface doesn't carry mDNS on Windows.** Windows also announced the adapter's own address rather than 127.0.0.1. So printing to the discovered URI couldn't reach the container, which listens only on 127.0.0.1. Discovery and printing were each verified, but not together against one printer.
- **The Print to PDF test failed because the test processes printed concurrently, not because the wait was too short.** `dotnet test` runs both target frameworks at once. When both printed to Microsoft Print to PDF together, one job stalled about a minute, then left an empty file. Alone it takes about 2 s. Three full runs with the lock: all pass, about 5 s each.

**Not done**
- **The real HP ENVY 5640** was switched off. Still to do when it is on:
  - read its attributes and save them as test data;
  - Validate-Job for a PDF (not implemented yet).

  Both are read-only; nothing is printed without approval.
- **Discovery from the Mac.** macOS's own Bonjour makes this a good second check. Steps in [manual-tests/spike-4-network.md](manual-tests/spike-4-network.md), which also covers listing the HP.
- **Discovery and printing together** against one printer on a real network.
- **IPPS:** discovered IPPS-only printers are listed with an `ipps:` URI, but TLS, including self-signed certificates, is untested (Phase 3).

**Next**
- Owner: review Spike 4 and ADR 0010. (The question on stable IDs was answered: see the entry above.)
- When the HP is on: the read-only checks above.
- Spike 5: capability model.

---

## 2026-11-11: Owner decisions and long-line cleanup

**Done**
- Owner decisions recorded in [plan.md](plan.md) and [ADR 0009](decisions/0009-windows-pdf-printing.md):
  - keep `PrintOptions.OutputFile` in the cross-platform options;
  - keep full device resolution as the default for Windows PDF, and make the cap configurable later (new Phase 2 item);
  - do a one-off cleanup of long lines.
- Rewrapped every C# line over 120 characters (tabs counted as 4).
- Added `max_line_length` and `guidelines` to `.editorconfig`, so editors that support them show a guide at column 120. Mentioned the guide in [CONTRIBUTING.md](../CONTRIBUTING.md).

**Learned**
- No analyzer enforces line length; the editor guide and review are the only checks.

**Next**
- Spike 4.

---

## 2026-11-11: Spike 3 — Windows PDF through PDFium and GDI

**Done**
- **Rasteriser contract** in `Latiprimere.Abstractions`: `IPdfRasterizer`, `IRasterDocument` (band rendering into caller buffers, BGRx), `PageSize`, `RasterBand`.
- **`Latiprimere.Rendering.Pdfium`**: PDFium through `LibraryImport`; one process-wide lock; native copy of the document; current page cached between bands; `AddLatiprimerePdfium()`. Native binaries from bblanchon/pdfium-binaries 158.0.8125 for Windows, Linux and macOS.
- **GDI print path** in `Latiprimere.Windows`:
  - DEVMODE through `DocumentProperties` (copies, collate, duplex, orientation), with orientation following the first page by default and a manual-copies fallback.
  - Page layout: fit to paper, centre, unprintable margin.
  - Rendering in bands of at most 16 MB, drawn with `StretchDIBits`.
  - Abort on cancel or failure.
  - An internal render-resolution cap.
- **`PrintOptions.OutputFile`**: print to a file. Honoured by Windows for raw and PDF jobs; IPP/CUPS refuse it.
- Shared helpers `PrinterHandles` and `SpoolerErrors` extracted from `WinSpoolApi`; Windows adapter now takes a `WindowsPrintContext`.
- CLI: `--output FILE` and `--stats`; registers PDFium.
- Test documents in `tools/test-documents/`: `mixed-1-page.pdf` and `mixed-100-pages.pdf`, generated by Ghostscript from `mixed-content.ps`; `three-pages.pdf` moved here.
- Tests: 252 tests (504 runs), all passing. New:
  - PDFium tests, including a byte-for-byte check that banded rendering matches whole-page rendering, and pixel checks.
  - Page-layout and DEVMODE tests.
  - DEVMODE/DOCINFO/BITMAPINFOHEADER layout tests.
  - PDF-path tests with fakes.
  - Two integration tests that print through real GDI to Microsoft Print to PDF.
- Decisions in [ADR 0009](decisions/0009-windows-pdf-printing.md).

**Measurements** (Microsoft Print to PDF at 600 DPI, A4 mixed content)

| Run | Time | Peak working set | Output |
|---|---|---|---|
| 1 page | 2.0 s | 225 MiB | 2.2 MB |
| 100 pages | 66 s | 225 MiB | 13 MB |
| 100 pages, render capped at 300 DPI | 36 s | 130 MiB | 44 MB |
| PDFium alone, 100 pages at 600 DPI | 4.6 s | 45 MiB | — |

Visual checks (rendered back to PNG): position, scale and colours match the original; band seams are invisible at print resolution; `--copies 2` gives 2 pages; `--landscape` gives a landscape page with the content fitted and centred.

**Learned**
- **The driver dominates.** PDFium is about 7% of the time at 600 DPI; Microsoft Print to PDF's in-process conversion is the rest, and most of the memory. Real drivers need measuring in Phase 2.
- **Memory stays flat** with page count (225 MiB for 1 and for 100 pages), well under the 500 MB target.
- **Bug caught visually:** `FPDFBitmap_BGRx` is 3, not 2 (2 is 3-byte BGR). The first pixel test passed by coincidence; rendering the output to PNG exposed it. Right-edge assertions now catch it, and were confirmed to fail with the wrong value.
- **Low-zoom previews show faint seams** where Print to PDF stores each band as a separate image; at print resolution the bands join seamlessly.
- **A 300 DPI cap** halves time and memory but made Print to PDF's output 3.4× larger; small text is slightly softer.
- **Microsoft Print to PDF needs an output file** or it shows a Save dialog, hence `PrintOptions.OutputFile`.
- **`System.Threading.Lock` is .NET 9+**; the PDFium lock is a plain object for .NET 8.
- **Constants on a `[SupportedOSPlatform("windows")]` class** trip the platform analyzer in cross-platform tests; platform-neutral values now live in `GdiRenderSettings`.

**Not done**
- Real (non-virtual) printer drivers: not measured.
- Mixed-orientation documents use the first page's orientation for all pages.
- PDFium has only been exercised on Windows.

**Next**
- Owner: review Spike 3; answer the open questions in [plan.md](plan.md).
- Spike 4: direct IPP to a network printer.

---

## 2026-11-11: Catalog skips failing print systems

**Done**
- Owner decision: a failing print system is skipped with a logged warning when listing printers or finding the default; the call fails only if every print system fails. Recorded in [ADR 0008](decisions/0008-print-system-failure-isolation.md).
- `PrinterCatalog` changed accordingly. With a single print system, its exception is rethrown unchanged; with several, a `PrintException` wraps them all in an `AggregateException`. Lookups by printer ID still report the owning print system's failure. Only `PrintException` is isolated; other exceptions propagate.
- `IPrinterCatalog` documentation updated; `Latiprimere` gained its first resource file.
- Tests: 8 new catalog tests and a capturing `FakeLogger`; 204 tests (408 runs), all passing.
- Verified with the CLI: with `CUPS_SERVER=localhost:631` and Docker stopped, `list` shows all Windows printers, and `print cups:Raw …` still reports the CUPS connection failure.

**Not done**
- The CLI doesn't configure logging, so it doesn't display the skipped-print-system warning.

---

## 2026-11-11: Spike 2 — Windows spooler

**Done**
- `Latiprimere.Windows`: `LibraryImport` interop for winspool.drv (EnumPrinters, OpenPrinter, GetPrinter, GetDefaultPrinter, StartDocPrinter/WritePrinter/EndDocPrinter, GetJob, SetJob), blittable structs and `SafePrinterHandle`; an internal `IWinSpool` boundary with the real `WinSpoolApi`; Windows print system, printer and job (`ICancellablePrintJob`); status mappers; `WindowsPrintSystemFactory` and `AddLatiprimereWindows()`. Design recorded in [ADR 0007](decisions/0007-windows-spooler-adapter-design.md).
- CLI registers the Windows adapter, and now reports "not supported" and "already finished" as messages instead of crashing.
- Tests: 196 tests (392 runs over `net8.0` and `net10.0`), all passing. New: mapper theories over JOB_STATUS / PRINTER_STATUS flag combinations, struct layout checks against the native sizes and offsets, the print system, printer and job against a fake spooler, and read-only integration tests against the real spooler (skipped off Windows).
- `tools/windows-test-printers/`: `Install-TestPrinters.ps1` and `Remove-TestPrinters.ps1` (admin) for the deferred end-to-end test.

**Verified on this PC, read-only**

| Check | Result |
|---|---|
| List printers | ✅ all 6 printers, matching `Get-Printer` |
| Default printer | ✅ Microsoft Print to PDF flagged |
| Printer state | ✅ offline HP ENVY shows `Stopped` |
| Unknown printer ID | ✅ `PrinterNotFoundException` |
| PDF to a Windows printer | ✅ rejected with `NotSupportedException` before anything is queued (queue stayed empty) |
| Native AOT publish (win-x64) | ✅ 16 MB exe, no trimming/AOT warnings, lists printers through the source-generated interop |

**Not done / not verified**
- **End-to-end raw printing, job status and cancel through the real spooler.** Deferred by the owner rather than make admin changes to this PC. The setup script is ready; until it runs, byte-for-byte output, status transitions and cancel are covered by unit tests only.
- Raw jobs to v4 (XPS-based) drivers, which the spooler is expected to refuse.

**Learned**
- **Windows doesn't record how a job finished.** Jobs leave the queue when done, and GetJob then fails with error 87. The adapter reports a vanished job as `Completed`, with a message saying the outcome isn't recorded, or `Cancelled` if Latiprimere cancelled it. "Keep printed documents" gives a definite final state. JOB_STATUS_COMPLETE means "sent to the printer", not "printed".
- **Raw data ignores PrintOptions.** Copies, duplex and orientation don't apply to RAW jobs on Windows; the data itself controls the output. Documented on `WindowsPrinter`.
- **One failing print system breaks the catalog.** With `CUPS_SERVER` set and Docker stopped, listing failed and the Windows printers weren't shown either. Added as an open question in [plan.md](plan.md).
- **The installed .NET SDK needs a patch.** A Native AOT publish fails NuGet's audit: `Microsoft.DotNet.ILCompiler` 10.0.12 and 8.0.31, which come with the installed SDK, have a high-severity advisory (GHSA-p3jp-9v83-76w9). The AOT check above was a throwaway build with that audit error downgraded on the command line only; the repo is unchanged.
- **AOT linking needs vswhere on PATH.** The native link step calls `vswhere.exe`, which wasn't on PATH in Git Bash.
- **Naming conflict resolved:** the enforced PascalCase rule rejects native names like `DOC_INFO_1`, so interop types are PascalCase with a comment naming the native type. CONTRIBUTING.md updated.

**Next**
- Owner: review Spike 2; decide the catalog failure question; optionally run `tools/windows-test-printers/Install-TestPrinters.ps1` (admin) so the end-to-end test can be done; update the .NET SDK.
- Spike 3: Windows PDF via PDFium + GDI.

---

## 2026-11-10: Spike 1 — CUPS over IPP

**Done**
- `tools/virtual-printers/`: Docker container with CUPS 2.4 and four queues (CUPS-PDF, Raw, Paused, Virtual-IPP) plus `ippeveprinter`, published on 127.0.0.1 only. See its [README](../tools/virtual-printers/README.md).
- `Latiprimere.Ipp`: IPP printer and job (`ICancellablePrintJob`), mappers for printer info, job status and job template attributes, TCP and Unix-socket HTTP transports, and an internal `IIppClient` wrapping SharpIppNext 5.0.2.
- `Latiprimere.Cups`: CUPS print system, `CupsOptions`, `CupsPrintSystemFactory` and `AddLatiprimereCups()`. Design recorded in [ADR 0006](decisions/0006-ipp-and-cups-adapter-design.md).
- CLI: `list` and `print <printer-id> <file> [--raw] [--copies N] [--duplex one|long|short] [--landscape] [--cancel]`.
- Tests: 134 tests (268 runs over `net8.0` and `net10.0`), all passing, including new `Latiprimere.Ipp.Tests` and `Latiprimere.Cups.Tests` and a shared `Latiprimere.Ipp.Testing` library of fakes and builders.
- Attribute dumps from the container saved in `tests/Latiprimere.Ipp.Tests/Data/` for Spike 5.
- [macOS manual test steps](manual-tests/spike-1-macos.md) written for the owner.

**End-to-end results** (CUPS 2.4.2 in Docker)

| Scenario | TCP from Windows | Unix socket on Linux |
|---|---|---|
| List queues, states and default | ✅ | ✅ (found the socket with no configuration) |
| Raw job, bytes compared | ✅ identical, CRLF kept | ✅ |
| PDF to CUPS-PDF, 2 copies | ✅ 6 impressions, PDF written | ✅ |
| PDF to IPP Everywhere queue, landscape | ✅ completed, 3 pages | — |
| Cancel a pending job | ✅ | ✅ |
| Unknown printer ID | ✅ `PrinterNotFoundException` | — |
| Trimmed self-contained build | — | ✅ no trim warnings, works |

**Learned**
- **CUPS accepts unsupported options and cancels later.** Duplex on the one-sided Virtual-IPP queue was accepted, then the IPP backend cancelled the job ("Printer cannot print with supplied options"). Latiprimere only sees `Cancelled`, with no message. Spike 5 should let callers check options against capabilities before submitting.
- **Authentication:** with the stock policy, Print-Job, Get-Job-Attributes and Cancel-Job of your *own* job need no authentication. CUPS trusts `requesting-user-name`. Cancelling another user's job returns HTTP 401. How SharpIppNext surfaces a 401 hasn't been exercised through Latiprimere yet.
- **Default printer:** filtering CUPS-Get-Printers on the `printer-type` default bit never matches; CUPS-Get-Default is needed. SharpIppNext has no typed request for it, so it is sent raw. Its validator rejects requests without `printer-uri`, so one is included.
- **SharpIppNext pitfall:** `StringWithLanguage` converts implicitly from `string`, so `cond ? null : new StringWithLanguage(x)` sends an empty attribute instead of none. A unit test caught this for `job-name`; fixed with explicit nullable casts.
- **Remote CUPS servers** reject a `Host` header they don't recognise (HTTP 400, DNS-rebinding protection). The container uses `ServerAlias *`. Real deployments need the client to use a name the server accepts.
- **Test container quirks:** CUPS writes nothing for unfiltered raw jobs on a `file:` device, so a small `capture` backend saves job data instead; `ippeveprinter` in CUPS 2.4 needs D-Bus and Avahi to start.
- **Paths in Git Bash:** `docker exec` paths need `MSYS_NO_PATHCONV=1`.

**Not done / not verified**
- macOS: waiting on the owner's manual test.
- The Docker-based end-to-end runs are manual; automating them in CI belongs to Phase 1.
- Job status reports no reason for a cancellation; `job-state-reasons` could be added later.

**Next**
- Owner: run the macOS test and review Spike 1.
- Spike 2: Windows spooler, raw jobs and job status.

---

## 2026-11-10: Spike 0 — scaffold, standards enforcement, core contracts

**Done**
- Solution `Latiprimere.slnx` with `global.json` (.NET 10 SDK), `Directory.Build.props` (`net8.0;net10.0`, nullable, warnings as errors, `latest-recommended` analyzers, code style enforced in build, AOT-compatible libraries), central package management, and a repo-level `nuget.config`.
- `.editorconfig` enforcing CONTRIBUTING.md. Verified with a deliberate-violation file: `var`, `_` field prefix, missing `this.`, missing braces, expression-bodied members, missing `ConfigureAwait(false)` and missing XML docs all fail the build.
- `Latiprimere.Abstractions`: `IPrinterCatalog`, `IPrinter`, `IPrintJob`, `ICancellablePrintJob`, `IPrintSystem` (the adapter contract), `PrinterInfo`, `PrintOptions`, `PrintContent`, `PrintJobStatus`, enums, and `PrintException` / `PrinterNotFoundException` / `PrintSystemUnavailableException`, with messages in `Resources/ExceptionMessages.resx`.
- `Latiprimere`: `PrinterCatalog` (combines all supported print systems), `PrintJobExtensions.WatchStatusAsync` (polls a job and reports changes until it finishes), and `AddLatiprimere()` for DI.
- Tests: 50 xUnit v3 tests, run on both `net8.0` and `net10.0` (100 runs, all passing), using hand-written builders and fakes.
- `samples/Latiprimere.Cli` lists printers; with no adapters yet it reports that printing is unavailable and exits with code 2.
- GitHub Actions workflow `.github/workflows/build.yml` (Windows, Ubuntu, macOS). The same commands pass locally in Release.
- Tooling decisions recorded in [ADR 0005](decisions/0005-build-and-test-tooling.md).

**Learned**
- The machine-level NuGet configuration maps packages to an unreachable company feed, which stalled restore for 5 minutes. The repo's own `nuget.config` now restores from nuget.org only.
- xUnit v3 4.x needs the Microsoft Testing Platform; .NET 10's `dotnet test` must opt in through `global.json`. `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` are no longer needed.
- The SDK's `GenerateSource` shortcut for `.resx` produced no class; explicit `StronglyTyped*` metadata works.
- Some analyzer rules conflict with CONTRIBUTING.md and were switched off with a comment: CA1805 (explicit default values), CA1863 (cached format strings would ignore culture changes), and in tests CA1861 and CS1591.
- No analyzer can flag target-typed `new()`; that rule relies on code review.
- Fixed during the spike: `PrintOptions.Default` was initialised before `MinimumCopies`, which would have given it zero copies. A test now guards it.

**Next**
- Owner review of the Spike 0 code.
- Push to GitHub to confirm the CI workflow on all three runners.
- Spike 1: CUPS over IPP on Linux (WSL/Docker) and macOS.

---

## 2026-11-10: Naming and target frameworks settled

**Done**
- Checked candidate names on NuGet and the web. Chose `CrossPrint.*` for namespaces and package IDs, replacing `Prodigy.CrossPrint.*` ([ADR 0004](decisions/0004-naming-and-poc-approach.md)).
- Target frameworks: multi-target `net8.0` and `net10.0` ([ADR 0002](decisions/0002-target-framework-and-interop.md)).
- Confirmed exception messages come from `.resx` resources ([ADR 0003](decisions/0003-coding-standards.md)).
- Updated CONTRIBUTING.md, plan.md and poc-plan.md to match; all plan open questions are now resolved.

**Learned**
- `CrossPrint`, `Spoolwise`, `OpenSpool` and `Platen` were all unclaimed on NuGet. "Cross Print" is also a commercial cloud-printing service (crossprint.io); `PrintBridge` and `Printwise` clash with existing printing software.
- .NET 8 and .NET 9 both reach end of support on 2026-11-10; .NET 10 is supported until November 2028.

**Next**
- Spike 0.

---

## 2026-11-10: Contributing guide and planning decisions

**Done**
- Rewrote [CONTRIBUTING.md](../CONTRIBUTING.md) as a Latiprimere-specific standard: general C# rules kept; ERP, database and company-process sections removed; new sections for contribution workflow, package layering, platform-specific code and virtual-printer integration tests. Recorded as [ADR 0003](decisions/0003-coding-standards.md).
- Recorded decisions: `Prodigy.CrossPrint.*` namespaces; a Mac is available; the proof of concept builds the real packages ([ADR 0004](decisions/0004-naming-and-poc-approach.md)).
- Reworked [poc-plan.md](poc-plan.md) around the real `src/`/`tests/`/`samples/` layout, added macOS steps to Spikes 1 and 4, and moved CI into Spike 0.
- Updated [plan.md](plan.md): package names, Phase 0/1 checklists, risks, open questions (5 resolved, 3 remaining).

**Learned**
- NuGet shows 18 verified `Prodigy` / `Prodigy.*` packages from an unrelated owner, which indicates a reserved `Prodigy.` ID prefix. Proposed: keep `Prodigy.CrossPrint.*` namespaces but publish under `Latiprimere.*` package IDs.

**Next**
- Answer the remaining open questions in [plan.md](plan.md) (package IDs, resource strings, target frameworks); none block Spike 0.
- Start Spike 0.

---

## 2026-11-10: Feasibility and planning

**Done**
- Researched existing .NET printing libraries and per-OS print APIs; recorded in [feasibility.md](feasibility.md). Verdict: feasible, gap is real but moderate.
- Confirmed NuGet package ID `Latiprimere` is unclaimed.
- Drafted [plan.md](plan.md) (roadmap, architecture, risks, open questions) and [poc-plan.md](poc-plan.md) (5 spikes + scaffold, go/no-go criteria).
- Recorded initial decisions as proposed ADRs in [decisions/](decisions/).
- Surveyed the dev environment: .NET SDK 10.0.401; WSL Ubuntu 20.04 (CUPS not installed); Docker Desktop 29.8; Windows queues include Microsoft Print to PDF and an HP ENVY 5640 on a WSD port.

**Learned**
- All three OSes are converging on IPP, which supports an IPP-first design.
- PrintingTools (MIT, alpha) is the closest open-source competitor; its Linux adapter shells out to `lp`.
- `CONTRIBUTING.md` is a company-internal standard with ERP-specific sections; how it applies to a public library needs a decision.

**Next**
- Review and answer the open questions in [plan.md](plan.md).
- Spike 0: scaffold `spikes/` with standards enforcement.

---

## Entry template

```markdown
## YYYY-MM-DD: Short title

**Done**
-

**Learned**
-

**Next**
-
```
