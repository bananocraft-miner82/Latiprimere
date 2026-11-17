# 0029: End-to-end test harness

- Status: Accepted
- Date: 2026-11-16

## Context

Each print system has integration tests against virtual printers, inside its own test project ([CONTRIBUTING.md](../../CONTRIBUTING.md), "Integration Tests and Real Printers"). Nothing checks the packages together through the public API, what a printer actually received, faults, the shipped `.nupkg` files, the samples, or real printers in a repeatable way. [test-harness-plan.md](../test-harness-plan.md) lists the gaps and proposes a harness.

Owner: "build a test harness for end-to-end testing in all areas of the packages … possible to run automatically or as part of manual testing … does not need to be packaged to nuget."

## Decision

Owner's answers to the plan's six questions, 2026-11-16:

1. **Scenarios, run by two programs.** Each end-to-end check is written once, as a scenario in `tests/Latiprimere.EndToEnd`, using the public API only. `tests/Latiprimere.EndToEnd.Tests` runs them under xUnit; `tests/Latiprimere.TestHarness` runs them from the command line and writes a Markdown and JSON report.
2. **ASP.NET Core is acceptable in the tests,** for the scripted IPP printer's Kestrel server (milestone H1).
3. **The existing `Integration/` tests stay as they are.** They are the single-adapter layer; the harness adds the layer above and doesn't duplicate them.
4. **Not in CI for now** (owner: "Leave it out of the CI/CD for now - just as a manual test harness. Just build it so that it's possible. A scheduled run could be a future option"). The harness projects are in the solution, so CI builds them and they can't rot, but their scenarios run only when `LATIPRIMERE_TEST_E2E` is set. CI doesn't set it; `tools/test-harness/run.ps1` and `run.sh` do. A scheduled workflow stays a later option.

   **One exception, 2026-11-17** (owner: "Replace check-aot.sh's run section with AOT mode", then "Same checks as today"): CI's Native AOT check (`tools/aot-check/check-aot.sh`) publishes the harness with Native AOT and runs four of its scenarios, the ones that make the checks the script made with the CLI sample before: listing, options through CUPS, options straight to `ippeveprinter` with a refused value, and Microsoft Print to PDF's paper sizes. It still publishes the CLI sample too. The rest of the harness stays out of CI.
5. **Manual mode may print on paper** (owner: "allow printing on paper in manual mode only as it will help prove that everything functions correctly with real printers"). Only in the console harness's manual mode, only to a printer named on the command line, and only after a person confirms each job at a prompt. Automated tests still never reach a physical printer. CONTRIBUTING.md says so.
6. **Try automated mDNS discovery on the Ubuntu runner,** with the test container on the host network so its announcements reach the tests (milestone H5). With the AOT check's four scenarios (decision 4), it is all of the harness that runs in CI; if it proves flaky, it is dropped.

Further rules:

- **Targets are the existing virtual printers,** found through the existing `LATIPRIMERE_TEST_*` variables, plus the scripted IPP printer once it exists. A scenario declares what it needs: it is skipped when that is missing and must pass when it is there, as the integration tests do.
- **Scenarios print only to the test printers.** The catalog lists every printer on the machine, real ones included; scenarios look up and print to the virtual printers by name only.
- **Printers that can't share** (Microsoft Print to PDF, file ports) are locked across processes with the same named semaphores the Windows integration tests use, so the harness, both target frameworks and `dotnet test` can run at once.

## Consequences

- One command per OS brings up the virtual printers and runs every scenario this machine can run, with a report to paste into [progress.md](../progress.md).
- CI time is unchanged until a scheduled run or the mDNS check is added.
- The scenario library can't use internals, which keeps the later packaged and Native AOT modes possible.
