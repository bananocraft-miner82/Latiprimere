# Phase 0 go/no-go review

> Status: **Decided: Go** (owner, 2026-11-12). Drafted 2026-11-11. Criteria from [poc-plan.md](poc-plan.md); details in [progress.md](progress.md).

## Recommendation

**Go: continue into Phase 1 on the same code base.** Every technical route works, and none failed. What is missing is verification in places this PC can't reach:

- a CI run;
- a Mac;
- the Windows spooler end to end;
- a real network printer.

Those checks should be done at the start of Phase 1, before building on the parts they cover (see [Conditions](#conditions)).

## Scorecard

| Spike | Done when | Result | Verdict |
|---|---|---|---|
| 0: Scaffold and standards | Build and tests pass with zero warnings on both frameworks on all three CI runners; a deliberate `var` fails the build | Passes locally on Windows in Debug and Release. A deliberate-violation file fails the build for `var` and six other rules. **The CI workflow has never run.** | Met locally; CI not run |
| 1: CUPS over IPP | A PDF from the CLI is produced by CUPS on Linux and macOS; status transitions seen; cancel works | **Linux** (CUPS 2.4 in Docker): PDF and raw jobs, status, cancel and the default printer all work, over TCP from Windows and over the Unix socket. Raw bytes came out identical. **macOS: not run yet.** | Met on Linux; macOS pending |
| 2: Windows spooler | Raw bytes land in the output file byte for byte; job status seen; the Native AOT build runs | **Read-only checks pass on this PC:** listing, the default printer, printer details. The Native AOT build (16 MB, no warnings) runs. **Not run through the real spooler:** printing raw bytes, job status and cancel. They are covered by unit tests only, because you chose not to make admin changes to this PC. | Partly met |
| 3: Windows PDF (highest risk) | Output is visually correct at 300 and 600 DPI; 100 pages under 500 MB peak | 100 pages at 600 DPI in 66 s with a 225 MiB peak, flat with page count. Output was checked visually at both resolutions, which caught and fixed one colour-format bug. | **Met** |
| 4: Direct IPP | Discovery finds both printers on both machines; attributes read; Validate-Job succeeds; a job completes on the virtual printer | A job completes on `ippeveprinter`. Attributes are read. Discovery works through a Windows DNS-SD announcement. The design is accepted in ADR 0010. **Not done:** the HP ENVY (switched off), discovery from the Mac, and Validate-Job, which isn't implemented. | Partly met |
| 5: Capability model | The same model fills from Windows, CUPS and direct IPP for the core properties, with tests over captured dumps | **Live:** two Windows drivers, three CUPS queues and one IPP printer. **Tests:** they replay four captured printer responses through the real decoder. | **Met** |

## Against the go/no-go criteria

| Criterion ([poc-plan.md](poc-plan.md)) | Finding |
|---|---|
| All spikes meet their "done when" → Go | Not yet in full. Every shortfall is a check not yet run, not a route that failed. |
| Spike 3 fails or is too slow → Windows PDF ships later | Doesn't apply: Spike 3 passed comfortably. |
| SharpIppNext is unsuitable → use `libcups` P/Invoke | Doesn't apply. It served CUPS and direct IPP. Its gaps had workarounds: CUPS-Get-Default sent raw, a `null` text pitfall caught by a test, and no `printer-dns-sd-name` (`printer-uuid` is used instead). |
| Two or more routes fail → stop and reassess | Doesn't apply: no route failed. |

## Conditions

These are mostly yours to do, in roughly this order:

1. **Push the branch and get the first CI run** on the Windows, Ubuntu and macOS runners. That completes Spike 0, and it is the first time the code builds anywhere but this PC.
2. **Run the Mac checks:** [spike-1-macos.md](manual-tests/spike-1-macos.md), then part A of [spike-4-network.md](manual-tests/spike-4-network.md). About 25 minutes.
3. **Windows end to end:** either
   - run `tools/windows-test-printers/Install-TestPrinters.ps1` (admin) so the raw, status and cancel test can be done here, or
   - accept unit-test coverage until Phase 1's CI integration tests cover it on the Windows runner.
4. ~~**HP ENVY, when it is on:** part B of the network test (listing only), plus reading its IPP attributes. Both are read-only.~~ Done 2026-11-12: found by discovery, attributes read, Validate-Job run; see [progress.md](progress.md).
5. ~~**Update the .NET SDK.** The ILCompiler packages that come with the installed SDK have a high-severity advisory (GHSA-p3jp-9v83-76w9), which blocks AOT publishing until patched.~~ Done 2026-11-12: SDK 10.0.404; the AOT publish passes the audit.
6. **Review [ADR 0011](decisions/0011-capability-model.md)** (capability model), the only decision still Proposed.

None of these needs code changes unless a check fails.

## Risks after Phase 0

| Risk | Likelihood now | Why |
|---|---|---|
| Windows PDF is too slow or poor | Low | 66 s and 225 MiB for 100 pages at 600 DPI, on a virtual driver. Real drivers are still to be measured. |
| SharpIppNext gaps or abandonment | Low | Every gap so far had a workaround, and it sits behind `IIppClient`. |
| macOS differs from Linux CUPS | Medium | Still unchecked. |
| Behaviour differs between printers | High | Seen already: CUPS accepts unsupported options and cancels later, while printers reject them up front; raw-data handling differs between printers. |
| **New:** Tmds.MDns is pre-1.0 | Low | Small and dependency-free, behind `IIppPrinterDiscovery`. Two pitfalls found and handled. |
| **New:** Windows doesn't record how a job ended | Certain | Finished jobs vanish from the queue, so Latiprimere reports Completed (with a note) or Cancelled. Applications that need the real outcome can't get it from the spooler. |

## What Phase 1 should start with

The Phase 1 list in [plan.md](plan.md), led by:

1. **CI integration tests against the virtual printers:**
   - the CUPS container on the Ubuntu runner;
   - Microsoft Print to PDF and a test queue on the Windows runner.

   This turns today's manual end-to-end runs into checks on every change.
2. **A connect timeout for IPP requests.** Today a dead address can take about 21 s to fail.
3. **The first preview package** (`0.1.0-preview`), so the `Latiprimere` ID is claimed.

## By the numbers

- **Packages:** 6 projects: Abstractions, core, CUPS, IPP, Windows and PDFium rendering. Plus a CLI sample.
- **Tests:** 331 tests, 662 runs over `net8.0` and `net10.0`, all passing.
- **Decisions:** 12 ADRs; 11 accepted and 1 proposed.
- **Verified against:**
  - CUPS 2.4 in Docker (4 queues);
  - `ippeveprinter`;
  - Microsoft Print to PDF;
  - the HP ENVY 5640's Windows driver.

## Decision

- [x] Go
- [ ] Go, with the conditions above
- [ ] No-go

Owner decision, 2026-11-12. The conditions above stay on the list as the first Phase 1 checks.
