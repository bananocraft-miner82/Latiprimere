# End-to-end test harness

Scripts that set up the virtual printers and run Latiprimere's end-to-end scenarios: every package together, through the public API, the way an application uses it. Nothing prints on paper. Background and roadmap: [test-harness-plan.md](../../docs/test-harness-plan.md) and [ADR 0029](../../docs/decisions/0029-end-to-end-test-harness.md).

The harness doesn't run in CI (ADR 0029). CI builds it, and its scenarios skip unless `LATIPRIMERE_TEST_E2E` is set.

## Run it

Windows (PowerShell):

```powershell
./tools/test-harness/run.ps1
./tools/test-harness/run.ps1 'cups.*' Lifecycle -Down
```

Linux or macOS:

```bash
tools/test-harness/run.sh
tools/test-harness/run.sh 'cups.*' Lifecycle --down
```

Each script:

1. starts the CUPS test container ([virtual-printers](../virtual-printers/README.md)) if Docker is running, or on a Mac uses its own test queues ([macos-test-printers](../macos-test-printers/README.md)) if they are installed;
   - makes the container afresh from the image when it is stopped, or running but unhealthy, as it is after the host restarts; a healthy one is left running;
2. on Windows, uses the spooler test printers ([windows-test-printers](../windows-test-printers/Install-TestPrinters.ps1)) if they are installed, and installs them first with `-InstallWindowsPrinters` from an elevated PowerShell;
3. sets the `LATIPRIMERE_TEST_*` variables for the run only;
4. runs the console harness and leaves a report in `out/harness/<time>-<os>-<framework>-console/`.

| Option (`run.ps1` / `run.sh`) | Effect |
|---|---|
| filters | Scenario IDs, ID prefixes ending in `*` (quote them) or areas such as `Lifecycle`; none runs everything |
| `-Xunit` / `--xunit` | Runs the xUnit host for both target frameworks instead, with a report per framework |
| `-Framework` / `--framework` | The .NET the console harness runs on; default `net11.0` |
| `-NoContainer` / `--no-container` | Doesn't start the container; the CUPS and direct IPP scenarios skip |
| `-InstallWindowsPrinters` | Installs the Windows test printers first (elevated only) |
| `-Down` / `--down` | Stops the container afterwards |
| `-Packages` / `--packages` | Packs Latiprimere into `out/harness-packages/<time>`, with a version of its own, and runs the scenarios against those packages instead of the projects |
| `-Aot` / `--aot` | Publishes the harness with Native AOT into `out/harness-aot` and runs the native program; fails on any trimming or AOT warning. Combines with `-Packages` |
| `--host-network` | Linux only: the container on the host's network, so `ipp.discovery.mdns` can find its `ippeveprinter`; CUPS then takes the host's port 631 |

Scenarios whose printers aren't there are skipped, and the report says what each needed.

**On WSL,** copy the repository into the Linux file system rather than building on `/mnt/c`, which would share `bin` and `obj` with Windows builds. If .NET stops at its first run with "The directory '/mnt/wslg/runtime-dir' does not have the expected owner or permissions", point `XDG_RUNTIME_DIR` at a private folder first: `mkdir -p -m 700 ~/.runtime-dir && export XDG_RUNTIME_DIR=~/.runtime-dir`.

## Without the scripts

With the variables set as in "Integration Tests and Real Printers" in [CONTRIBUTING.md](../../CONTRIBUTING.md):

```bash
dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- list      # every scenario, and whether it can run here
dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- targets   # the test printers found
dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- run       # run them, with a report
LATIPRIMERE_TEST_E2E=1 dotnet test --project tests/Latiprimere.EndToEnd.Tests
```

Exit codes: 0 when every scenario that ran passed, 1 when one failed, 2 for arguments it didn't understand.

## The fake printer

`tests/Latiprimere.FakeIppPrinter` is a scripted IPP printer that the `ipp.*` scenarios start inside the test process, on any OS, without Docker. It answers like an IPP Everywhere printer, records every request with its attributes and document, and can be busy, slow, stopped, out of paper, password-protected, encrypted with a self-signed certificate, or hang up mid-job.

It also runs on its own, to try Latiprimere, the CLI sample or `ipptool` against it by hand:

```bash
dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- fake-printer --busy 2 --processing 5
dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- fake-printer --profile hp-envy-5640 --stopped
```

It listens on `127.0.0.1:8632` and prints each request as it arrives; `dotnet run --project tests/Latiprimere.TestHarness -- help` lists the options. With `--any` it listens on every IPv4 address, so the CUPS test container can reach it as `host.docker.internal`, but then so can anyone on the network. To check it against CUPS's conformance tests (on Windows with Git Bash, set `MSYS_NO_PATHCONV=1` first):

```bash
docker cp tools/test-documents/three-pages.pdf latiprimere-cups:/tmp/three-pages.pdf
docker exec latiprimere-cups ipptool -t -I -f /tmp/three-pages.pdf ipp://host.docker.internal:8632/ipp/print ipp-1.1.test
```

Its printer attributes come from the captured responses in `tests/Latiprimere.Ipp.Tests/Data`: `ippeveprinter` (the default) and `hp-envy-5640`.

## Packaged and AOT modes

Packaged mode checks the packages users get: that they restore, carry PDFium's native files and declare their dependencies. The scenario library and `Latiprimere.Testing` then reference the packages, through a `nuget.config` beside them that takes `Latiprimere*` from that folder, and build into `out/packaged` rather than their own `bin` and `obj`. Each run's packages get a version of their own, such as `0.1.1-preview.e2e.20261117101417`, which the report's "Latiprimere under test" line shows. The samples still build from the projects.

AOT mode runs every unattended scenario in a Native AOT build of the harness, as an application published that way would run Latiprimere.

Both leave files in `out/` and, for packaged mode, a few megabytes in NuGet's global cache for each run's version.

## mDNS discovery

`ipp.discovery.mdns` finds a printer by the name it announces over mDNS, set in `LATIPRIMERE_TEST_MDNS_NAME`, then looks it up and reads its capabilities. It prints nothing, so it can point at a real printer on your network: `LATIPRIMERE_TEST_MDNS_NAME="<name>"` with the name `list` in the CLI sample shows after `ipp:dnssd:`. On Linux, `--host-network` points it at the test container's `ippeveprinter`, as the `mdns` job in CI does.

## Manual mode and captures

For real printers, `manual --printer <id>` runs the `manual.*` scenarios: it reads the printer, then prints each test job only after you answer `y`, and asks what came out. `capture <printer>` saves a printer's IPP attributes without printing. How to run both, and what each scenario asks: [docs/manual-tests](../../docs/manual-tests/README.md).

Scenarios that need a person declare `TargetNeeds.Person`. Only manual mode provides one, so `run`, the xUnit host and the scripts skip them. They print only to `context.Targets.ManualPrinterId`, through `ManualPrinting.PrintAsync`, which shows the job and waits for a yes.

## The report

`report.md` has a summary table, then each scenario's checks, notes and links to its evidence (the bytes a queue received, the PDF a driver wrote, the printers listed). It is written to paste into [progress.md](../../docs/progress.md). `report.json` holds the same for tools. Set `LATIPRIMERE_TEST_E2E_REPORTS` to write reports somewhere other than `out/harness`.

## Adding a scenario

Scenarios live in `tests/Latiprimere.EndToEnd/Scenarios`, grouped by area, and are listed in `AllScenarios`. Each declares what it needs (`TargetNeeds`) and records what it finds through `ScenarioContext`: `Check` and `CheckEqual` carry on after a failure, `Require` and `RequireEqual` stop the scenario, and `SaveEvidenceAsync` keeps a file for the report. Rules:

- **Public API only.** No `InternalsVisibleTo`, so the scenarios can later run against the packed `.nupkg` files and as a Native AOT program.
- **Test printers only.** Look printers up by the IDs in `TestPrinters`; the catalog also lists real printers, which must never receive a test job.
- **Unique data per job,** since both target frameworks and the integration tests may print to the same virtual printer at once; hold a `SharedPrinterLock` around jobs to Windows printers that can't share.
- **No reflection,** so the console harness stays Native AOT compatible.
- **Samples run as programs.** `Support/Samples` starts the sample built alongside the harness, with `CUPS_SERVER` set to the CUPS test server or removed, so a sample never reaches another server. Name the test printer on its command line; an example that uses the default printer must check it is a CUPS test queue first, and call `context.Skip` otherwise.
- **`context.Skip`** is for what only the scenario can find out at run time; anything the targets say belongs in `Needs`.
- **Printed PDFs:** `PdfPrinters` prints to CUPS-PDF or Microsoft Print to PDF and reads back what was written; `PdfInspector` gives each page's size, ink and colour. Check a printer only for what it honours (see `OutputScenarios`).
