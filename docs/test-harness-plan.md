# End-to-End Test Harness Plan

> Status: **Accepted** (2026-11-16); decisions recorded in [ADR 0029](decisions/0029-end-to-end-test-harness.md). H0 to H4 done (2026-11-17); H5 built the same day, with only its mDNS trial still to run in CI. Part of [plan.md](plan.md).

## Purpose

One harness that checks every package working together, through the public API, the way an application uses it. It runs unattended in CI and on a developer's machine, and it guides a person through the checks that need eyes on paper or a real printer. It is a test tool only: it lives under `tests/`, which is never packed.

## Where we are

The adapters already have good integration tests, run in CI against virtual printers:

| What exists | Covers | Runs on |
|---|---|---|
| `Integration/` in `Latiprimere.Cups.Tests` (18 tests) | Listing, default printer, capabilities, raw bytes, PDF, options, vendor options, cancel, authentication over TLS | Ubuntu (container), macOS (own CUPS) |
| `Integration/` in `Latiprimere.Ipp.Tests` (10 tests) | Direct IPP to `ippeveprinter`: lookup, capabilities, PDF, refused options, vendor options, concurrent jobs | Ubuntu, macOS |
| `Integration/` in `Latiprimere.Windows.Tests` (11 tests) | Spooler listing, raw bytes, retained jobs, cancel, spooler errors, Microsoft Print to PDF, render DPI cap, DEVMODE, paper size, IPP route | Windows |
| `tools/aot-check/check-aot.sh` | The CLI sample published with Native AOT, run against the runner's printers | All three |
| `docs/manual-tests/*.md` | Step-by-step manual checks (Mac CUPS, mDNS on a real network) | By hand |

Each adapter is tested on its own, from inside its own test project. What isn't covered:

1. **Packages together.** Nothing registers every print system in one catalog and checks that IDs route correctly, that the default printer is chosen sensibly, or that one print system failing leaves the others working ([ADR 0008](decisions/0008-print-system-failure-isolation.md)).
2. **What the printer actually received.** Direct IPP and the Virtual-IPP queue are checked for `Completed` only. Nothing checks that `media-col`, `sides`, `copies`, `print-color-mode` or `ipp:` vendor keys arrived as intended.
3. **Faults.** Busy printers ([ADR 0017](decisions/0017-retry-busy-ipp-printers.md)), connect timeouts, dropped connections, slow job progress, stopped printers and the Windows IPP route's fallback are unit-tested against fakes only. `ippeveprinter` can't be told to misbehave.
4. **Direct IPP on Windows.** The Windows runner has no `ippeveprinter`, so direct IPP and the IPP route skip there.
5. **The printed result.** CUPS-PDF and Microsoft Print to PDF produce PDFs, but only Windows checks their page size, and nothing checks page count, orientation or colour.
6. **The shipped packages.** Every test builds against project references. Nothing installs the `.nupkg` files as a consumer would, which is where missing PDFium native assets, wrong dependency groups or a broken `Latiprimere.Windows` → `Latiprimere.Ipp` reference would show.
7. **The samples and docs examples.** `Latiprimere.Examples` mirrors the documentation site but never runs in CI.
8. **mDNS discovery** is checked by hand only.
9. **Real printers.** Manual tests are Markdown checklists whose results are pasted back by hand. The release process waits on "manual testing with real printers" ([ADR 0028](decisions/0028-automated-releases.md)) with no repeatable way to do it.
10. **Local runs need setup by hand:** start the container, export four variables, run `dotnet test`.

## Recommendation in short

- **Keep the existing `Integration/` tests where they are.** They are the adapter layer: one print system, with access to internals. The new harness is the layer above: all packages, public API only.
- **Write each end-to-end check once, as a scenario,** in a library that uses only the public API. Two hosts run the same scenarios: an xUnit project for automated runs (CI, `dotnet test`), and a console harness for local and manual runs.
- **Add a scripted IPP printer** that runs inside the test process on any OS, records everything it receives, and misbehaves on request. It closes gaps 2, 3 and 4 without Docker.
- **Inspect the output** of the PDF-producing printers with the PDFium we already ship.
- **Add a packaged mode** that builds the scenarios against the `.nupkg` files, and an AOT mode that runs them as a native executable.
- **One script per OS** brings up the virtual printers, sets the variables, runs the harness and writes a report.

## Design

### Projects

All under `tests/`, so `IsPackable` is already false.

| Project | Kind | Contents | References |
|---|---|---|---|
| `Latiprimere.EndToEnd` | Library | Scenarios, the scenario model, targets, output verifiers | Public packages only (project or package references, see [Packaged mode](#packaged-mode)), `Latiprimere.FakeIppPrinter` |
| `Latiprimere.EndToEnd.Tests` | xUnit v3 executable | One test case per scenario and target; skips when a target is missing, fails once it is configured | `Latiprimere.EndToEnd` |
| `Latiprimere.TestHarness` | Console executable | `list`, `run`, `manual`, `capture`, `fake-printer` commands; Markdown and JSON reports; AOT-publishable | `Latiprimere.EndToEnd` |
| `Latiprimere.FakeIppPrinter` | Library | The scripted IPP printer | SharpIppNext, ASP.NET Core (Kestrel); **not** Latiprimere, so it can't share code paths with what it tests |

The scenario library may not use `InternalsVisibleTo`. That rule is what makes the packaged and AOT modes possible, and it keeps the scenarios honest about what an application can do.

### Scenarios

A scenario is a named, self-describing check with what it needs declared up front:

```csharp
Scenario scenario = new Scenario
{
	Id = "ipp.pdf.media-and-sides",
	Area = ScenarioArea.Submission,
	Needs = TargetNeeds.FakeIppPrinter,
	Description = "Paper, sides and copies chosen in PrintOptions reach the printer as IPP job attributes",
	RunAsync = async context =>
	{
		IPrinter printer = await context.Catalog.GetPrinterAsync(context.Targets.FakeIpp.PrinterId);
		// Submit, wait, then check what the fake printer received
		context.Check("media", received.Media == "iso_a5_148x210mm");
	}
};
```

- `context.Check(...)` records each assertion with evidence (output file, request log), so a failed run shows what was compared.
- `context.AskAsync(question)` exists only in manual mode. A scenario that asks a person is tagged `NeedsPerson` and never runs unattended.
- Scenarios are grouped by **area** and filtered by area, target or ID in both hosts.

### Targets

A target is a print system the harness can reach. Every scenario says which it needs; missing targets skip, configured ones must pass.

| Target | Set up by | Where it runs | Configured by |
|---|---|---|---|
| **Fake IPP printer** | The harness itself, in process | Everywhere, always | Nothing |
| **CUPS container** | `docker compose` (existing) | Ubuntu CI; any machine with Docker | `LATIPRIMERE_TEST_CUPS_*`, `LATIPRIMERE_TEST_IPP_PRINTER` (existing) |
| **Mac CUPS** | `tools/macos-test-printers` (existing) | macOS CI; a spare Mac | The same, with `LATIPRIMERE_TEST_CUPS_SETUP=macos` |
| **Windows spooler** | `Install-TestPrinters.ps1` (existing), Microsoft Print to PDF | Windows CI; an elevated dev machine | `LATIPRIMERE_TEST_WINDOWS_OUTPUT` (existing) |
| **Real printer** | A person | Manual mode only | `--printer <id>` on the command line, confirmed at a prompt |

The existing variables stay the source of truth, so `IntegrationEnvironment` and CI keep working unchanged.

### The scripted IPP printer

A small IPP server on Kestrel at `127.0.0.1` on a free port (and `[::1]` when a scenario asks), started per scenario or per test class. It is the main new piece.

- **Behaves like a printer:** answers Get-Printer-Attributes from a profile, either a built-in IPP Everywhere one or a captured `.ipp` attribute dump from `tests/Latiprimere.Ipp.Tests/Data/` (so the HP ENVY's real attributes can be served); accepts Print-Job, Validate-Job, Get-Job-Attributes, Get-Jobs and Cancel-Job; moves jobs through pending, processing and completed on a clock the scenario controls.
- **Records everything:** each request's operation, attributes and document bytes, for scenarios to assert on.
- **Misbehaves on request:** `server-error-busy` for the first N jobs; a delay before answering; a dropped connection mid-request; a refused attribute; `client-error-not-authorized` and a Basic challenge; TLS with a self-signed certificate for `ipps`; a stopped printer; a job that ends aborted.
- **Runs standalone too:** `Latiprimere.TestHarness fake-printer --port 8632 --busy 3` starts it on its own, so the CLI sample or a Windows queue added by URL can be pointed at it by hand.

Built in H1 as `FakePrinter` in `tests/Latiprimere.FakeIppPrinter`. It reads and writes IPP with SharpIppNext's raw message API (`IppProtocol`), not its typed server API, so it can answer with anything a printer might, including errors. It passes every test in CUPS's `ipptool` IPP/1.1 suite that applies to the operations it implements (Print-Job, Validate-Job, Cancel-Job, Get-Job-Attributes, Get-Jobs and Get-Printer-Attributes); `operations-supported` lists only those, so clients don't try the others.

Not covered by the fake: a connect timeout, which needs an address where nothing answers rather than a refused port; and authentication that succeeds, since direct IPP can't send a password until Phase 3.

### Output verifiers

Small helpers in `Latiprimere.EndToEnd`, each returning evidence for the report:

- **Raw bytes:** byte-for-byte comparison of what a file port, the container's `capture` backend or the Mac's capture server saved (existing waiters reused).
- **PDF output** (CUPS-PDF, Microsoft Print to PDF): page count, each page's size and orientation, read through `Latiprimere.Rendering.Pdfium`. Built in H3 as `PdfInspector`: each page is also rendered at 36 DPI and measured for ink (the share that isn't white) and colour (the share that isn't grey). A printed page's content counts as intact when its ink is within 30% of the page sent, on paper of the same shape.
  - **What each printer honours** (found 2026-11-17): CUPS-PDF honours paper size and monochrome, but keeps a PDF's own orientation and writes one copy. Microsoft Print to PDF honours paper size, landscape and copies, but ignores the DEVMODE's colour setting, the only thing Latiprimere sets for monochrome on Windows (ADR 0025). The scenarios check each printer only for what it honours, and note the rest.
- **IPP requests:** what the fake printer recorded.
- **Job status sequence:** the states `WatchStatusAsync` reported, in order, and that it ends.

Golden-image comparison is left out on purpose: driver and PDFium updates would make it fragile. A person checks appearance in manual mode.

### Packaged mode

`Latiprimere.EndToEnd` switches between project and package references on one MSBuild property:

```bash
dotnet pack -c Release -o out/packages
dotnet test tests/Latiprimere.EndToEnd.Tests -p:LatiprimereFromPackages=out/packages
```

`Latiprimere.Testing`, whose waiters and test documents the scenarios reuse, switches with it. With the property set, the projects reference the packages from that folder (a temporary NuGet source), at the version just packed. The same scenarios then prove that the packages restore, carry the right native PDFium assets for the runner's RID, and declare their dependencies correctly. The Release workflow can run it against the exact packages it is about to publish.

### AOT mode

`Latiprimere.TestHarness` is published with Native AOT and runs the unattended scenarios as a native program. This widens today's AOT check from the CLI sample's few commands to every scenario. The scenario library must therefore avoid reflection; xUnit isn't involved in this mode. Since 2026-11-17, `check-aot.sh`'s run section calls the native harness, with the four scenarios that make the checks it made with the CLI (`catalog.list-across-print-systems`, `cups.submission.vendor-options`, `ipp.submission.virtual-printer-options`, `windows.output.paper-and-orientation`), so CI runs those (ADR 0029, decision 4).

### Running it

| How | Command | Use |
|---|---|---|
| With everything else | `LATIPRIMERE_TEST_E2E=1 dotnet test` | The xUnit host is picked up like any test project; without the variable every scenario skips, and with it each skips only without its targets |
| End-to-end only | `dotnet test --project tests/Latiprimere.EndToEnd.Tests` with `LATIPRIMERE_TEST_E2E=1` | Quick local check |
| One command, set up included | `tools/test-harness/run.ps1` / `run.sh` | Starts the container if Docker is present, installs the Windows test printers if elevated, sets the variables, runs the harness, writes the report, `-Down` / `--down` to tear down |
| Console, chosen scenarios | `dotnet run --project tests/Latiprimere.TestHarness -- run --area capabilities --target container` | Debugging one area; prints evidence as it goes |
| Manual, real printer | `dotnet run --project tests/Latiprimere.TestHarness -- manual --printer "windows:HP ENVY 5640 series"` | Guided checklist on real hardware, see below |

Reports go to `out/harness/<date>-<os>/` as `report.md` (pasteable into [progress.md](progress.md)) and `report.json`, with the evidence files beside them. CI uploads that folder as an artifact.

### Manual mode

For the checks only a person can make, and for the "manual testing with real printers" that ADR 0028 waits on.

- **Opt-in and explicit.** Never runs from `dotnet test` or CI. The printer must be named with `--printer`, and the harness shows the printer, the document and the page count, and waits for "y" before each job that prints on paper. This is a deliberate exception to CONTRIBUTING.md's "never send output to a physical printer", which applies to automated tests; the section would gain a sentence saying so.
- **Read-only first.** It starts with listing, capabilities and an IPP attribute capture, then asks before printing anything.
- **Guided.** After each job, it asks the questions a person can answer: right paper, two-sided, mono, tray, copies, nothing clipped.
- **Captures data.** `capture <printer>` saves the printer's attributes as `.ipp` and `.txt`, ready for `tests/Latiprimere.Ipp.Tests/Data/` and for the fake printer's profiles. This follows CONTRIBUTING.md's "capture real-printer behaviour as data files".
- **Replaces the checklists** in `docs/manual-tests/` over time, so each manual run produces the same report format.

### CI

**Not for now** (ADR 0029, decision 4): the harness is a manual and local tool. CI builds its projects, so they keep compiling, but every scenario skips unless `LATIPRIMERE_TEST_E2E` is set, and CI doesn't set it. The run scripts set it.

The one planned exception is **mDNS discovery on the Ubuntu runner** (decision 6): the container on the host network so its Avahi announcements reach the tests, tried in H5 and dropped if flaky. Built as the `mdns` job in `build.yml`, which runs `ipp.discovery.mdns` and can't fail the build while on trial (`continue-on-error`).

Later options, not planned: a scheduled run of the slow scenarios (the 100-page PDF at 600 DPI with its time and memory budget from ADR 0009, many concurrent jobs, long busy retries), packaged and AOT modes, and packaged mode in `release.yml` before publishing.

## Coverage the harness adds

| Area | Scenarios (examples) | Targets |
|---|---|---|
| Catalog across print systems | Every system registered at once; IDs route to the right system; default printer; one system unavailable (CUPS address with nothing behind it) leaves the rest listed; DI with and without the host | All automated |
| Discovery | Configured IPP URIs; CUPS queues; Windows queues; mDNS (nightly, optional) | Fake, container, Mac, Windows |
| Capabilities | The same printer seen through CUPS and directly over IPP agrees; profiles from captured dumps (HP ENVY) map as the unit tests expect | Fake, container |
| Submission and options | Paper, tray, colour, sides, copies, orientation and `ipp:` / `cups:` / `windows:` vendor options arrive as intended | Fake (exact attributes), container, Windows (PDF inspection) |
| Job lifecycle | Status sequence ends; cancel while pending and while processing; aborted jobs report why | Fake, container, Windows |
| Faults and resilience | Busy retry then success; busy past the limit; connect timeout; dual-stack connect; dropped connection; stopped printer | Fake |
| Security | Basic authentication over TLS, wrong password, certificate check | Fake, container |
| Windows PDF path | Rendered output page count and size; DPI cap; 100-page time and memory (nightly) | Windows |
| Windows IPP route | Routed to the printer; printer without PDF falls back to the spooler; connect failure falls back; refusal is thrown | Fake + Windows |
| Samples and docs | Each `Latiprimere.Examples` example runs against the virtual printers and exits 0 | Container, Windows |
| Packaging | All of the above, built from the `.nupkg` files | Packaged mode |
| Native AOT | All unattended scenarios as a native program | AOT mode |
| Real printers | Guided checklist and attribute capture | Manual |

## Milestones

| | Milestone | Done when |
|---|---|---|
| H0 ✅ | **Skeleton and decision.** ADR for the harness; the scenario library, xUnit host and console harness (the fake printer's project comes with H1); scenario model; `run.ps1` / `run.sh`; Markdown and JSON reports; scenarios ported from today's checks (catalog listing across systems, raw bytes, PDF to CUPS-PDF, cancel on a paused queue, Microsoft Print to PDF) | `run.ps1` on the Windows dev machine passes with a report; CI builds the projects and skips the scenarios |
| H1 ✅ | **Scripted IPP printer.** Behaviour, recording, faults, profiles from dumps, standalone `fake-printer` command; submission, lifecycle, fault and security scenarios against it | Direct IPP scenarios pass on the Windows runner; every fault in the list has a scenario |
| H2 ✅ | **Across packages.** Catalog failure isolation, Windows IPP route and fallback, DI and hosted-service samples, `Latiprimere.Examples` smoke runs | Every example in the docs runs in the harness (not in CI, ADR 0029) |
| H3 ✅ | **Output verifiers.** PDF page count, size, orientation, blank and mono checks; Windows PDF path scenarios | CUPS-PDF and Microsoft Print to PDF outputs are inspected, not just found |
| H4 ✅ | **Manual mode.** Guided checklist, confirmations, attribute capture, report; `docs/manual-tests/` rewritten as harness runs; CONTRIBUTING.md amended | One real-printer run on the HP ENVY recorded in progress.md |
| H5 🔶 | **Packaged, AOT and mDNS.** Package-reference switch, AOT-published harness, mDNS discovery on the Ubuntu runner | Packaged and AOT modes pass locally; the mDNS check passes in CI or is dropped |

H0 and H1 give most of the value. H2 to H5 can be reordered.

## Decisions

Answered by the owner on 2026-11-16 and recorded in [ADR 0029](decisions/0029-end-to-end-test-harness.md):

1. **Two programs** run the scenarios: the xUnit host and the console harness.
2. **ASP.NET Core** is acceptable in the tests, for the fake printer.
3. **The existing `Integration/` tests stay as they are.**
4. **Not in CI for now;** built so a scheduled run can be added later. Scenarios run only when `LATIPRIMERE_TEST_E2E` is set.
5. **Printing on paper is allowed in manual mode only,** with the rules above; CONTRIBUTING.md says so.
6. **mDNS discovery is tried in CI on Ubuntu** (H5).

## Risks

| Risk | Mitigation |
|---|---|
| The fake printer agrees with Latiprimere's own mistakes | It shares no code with Latiprimere; it is checked against `ipptool` and against the same scenarios run on `ippeveprinter` |
| Slow or flaky CI | Fast scenarios per push, slow ones nightly; generous waits with clear evidence on timeout; the existing `PrinterLock` for printers that can't share |
| Scenarios and `Integration/` tests drift into duplicates | New end-to-end checks go in scenarios; `Integration/` stays for single-adapter checks needing internals; reviewed in each PR |
| AOT rules constrain the scenario library | No reflection in scenarios; the xUnit host keeps working if AOT mode is dropped |
