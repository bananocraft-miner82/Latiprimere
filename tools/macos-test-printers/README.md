# macOS test printers

Test queues on a Mac's own CUPS, for running Latiprimere's integration tests on macOS. They copy the [test container](../virtual-printers/README.md) as far as macOS allows. CI sets them up on the macOS runner, which has no Docker. Nothing prints on paper.

**Meant for CI runners and spare Macs.** The install script adds three queues to the system CUPS, starts two small servers, and turns on CUPS's debug logging. `remove-test-printers.sh` undoes all of it.

## Install and remove

Run as yourself, not with `sudo`: the scripts ask `sudo` for the steps that need root, and the servers run as you. The capture server needs `python3`.

```bash
sh tools/macos-test-printers/install-test-printers.sh
sh tools/macos-test-printers/remove-test-printers.sh
```

The install script takes the output folder as an optional argument; it defaults to `/Users/Shared/LatiprimereTestOutput`. Re-running the script recreates everything. The remove script leaves the output folder, and Homebrew's `cups` if the install script added it.

## What's inside

| Queue | Device | Behaviour | Output |
|---|---|---|---|
| `Raw` | `socket://127.0.0.1:9101/?snmp=false` | Sends job data unchanged to the capture server | `<output-folder>/capture-<process-id>-<number>.prn` |
| `Paused` | `socket://127.0.0.1:9101/?snmp=false` | Disabled, so jobs stay pending until cancelled | — |
| `Virtual-IPP` | `ipp://localhost:8631/ipp/print` | Driverless IPP Everywhere queue in front of `ippeveprinter`, through macOS's own PDF filters | `/tmp/latiprimere-test-printers/spool/` |

| Port | Server | Log |
|---|---|---|
| 8631 | `ippeveprinter`, started with the same options as in the container | `/tmp/latiprimere-test-printers/ippeveprinter.log` |
| 9101 | [capture-server.py](capture-server.py), on `127.0.0.1` only: saves each connection to a file, as a printer on an AppSocket port would receive it | `/tmp/latiprimere-test-printers/capture-server.log` |

The container's `CUPS-PDF` and `Secure` queues are left out:
- **CUPS-PDF** is a Debian package with no macOS equivalent.
- **Secure:** macOS CUPS checks passwords against the Mac's own accounts, and asks for encryption only from other machines, which the tests on the same Mac aren't. So it couldn't reproduce what the container's tests check.

Their tests skip when `LATIPRIMERE_TEST_CUPS_SETUP=macos` (see "Integration Tests and Real Printers" in [CONTRIBUTING.md](../../CONTRIBUTING.md)).

## Running the tests

```bash
sh tools/macos-test-printers/install-test-printers.sh
export LATIPRIMERE_TEST_CUPS_SERVER=/private/var/run/cupsd LATIPRIMERE_TEST_CUPS_SETUP=macos
export LATIPRIMERE_TEST_CUPS_OUTPUT=/Users/Shared/LatiprimereTestOutput
export LATIPRIMERE_TEST_IPP_PRINTER=ipp://127.0.0.1:8631/ipp/print
dotnet test
```

## How it works

- **No extra backend:** the container saves raw jobs with its own `capture` backend, but Apple's CUPS can't take one.
  - macOS keeps CUPS's backends in `/usr/libexec/cups`, which System Integrity Protection makes read-only, even for root.
  - Apple's CUPS also ignores `ServerBin` in `cups-files.conf`. On the macOS runner (CUPS 2.3.4, macOS 26), `cupsd -t` accepted `ServerBin` pointing at a copy of the folder, in `/opt` and then in `/Library/Printers`. Yet `cupsd` kept running its programs from `/usr/libexec/cups`, with no sandbox denial.
- **The capture server instead:** `Raw` uses CUPS's own `socket` backend, which sends job data unchanged, the same way it would to a network printer on port 9100. `capture-server.py` takes the printer's place, and saves each connection under a temporary name before renaming it, so nothing reads half a job.
  - **Found by content:** the server can't know CUPS's job ID, so it numbers its files itself. With `LATIPRIMERE_TEST_CUPS_SETUP=macos`, the raw-bytes test finds its job's file by its content, which is unique to each run. In the container, the file name is checked against the job ID too.
  - **`snmp=false`** stops the socket backend asking the "printer" for supply levels over SNMP, which nothing answers.
- **Not `ippeveprinter` for raw data:** it reads the first bytes of `application/octet-stream` documents to guess their type, and refuses data it doesn't recognise, such as plain text.
- **`ippeveprinter`:** taken from the `PATH` if macOS has it. Otherwise the script installs Homebrew's `cups`, which is keg-only and doesn't replace the system CUPS.
- **Debug logging** (`cupsctl --debug-logging`) is on, so a failed CI run shows why in `/var/log/cups/error_log`.
