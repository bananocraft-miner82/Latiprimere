# Virtual printers

A Docker container running CUPS 2.4 (Debian Bookworm) with virtual printers, for developing and testing Latiprimere without paper. Everything is published on `127.0.0.1` only.

## Start and stop

```bash
docker compose -f tools/virtual-printers/compose.yaml up -d --build --wait
docker compose -f tools/virtual-printers/compose.yaml down
```

`--wait` returns once the health check passes: the queues exist, CUPS-PDF is the default, and `ippeveprinter` answers. `docker logs latiprimere-cups` also shows `Latiprimere virtual printers ready: …` at that point.

Job output goes to `/output`, which is a Docker volume of its own (`latiprimere-output`, removed by `down -v`). To read it from the host, set `LATIPRIMERE_TEST_CUPS_OUTPUT` to a host directory before `up`, and it is mounted there instead.

## Integration tests

The integration tests in `tests/` run against this container when environment variables say where it is; see "Integration Tests and Real Printers" in [CONTRIBUTING.md](../../CONTRIBUTING.md). CI starts it on the Ubuntu runner. The macOS runner has no Docker, so it gets the same queues, except CUPS-PDF and Secure, on its own CUPS instead: see [macos-test-printers](../macos-test-printers/README.md).

## What's inside

| Queue | Device | Behaviour | Output |
|---|---|---|---|
| `CUPS-PDF` (default) | `cups-pdf:/` | Renders jobs to PDF | `/output/pdf/` |
| `Raw` | `capture:/` | Saves job data unchanged | `/output/Raw-<job-id>.prn` |
| `Paused` | `capture:/` | Disabled, so jobs stay pending until cancelled | — |
| `Secure` | `capture:/` | Printing needs user `latiprimere`, password `test-password`, over TLS (policy `authenticated`) | `/output/Secure-<job-id>.prn` |
| `Virtual-IPP` | `ipp://localhost:8631/ipp/print` | Driverless IPP Everywhere queue in front of `ippeveprinter` (one-sided only) | `/var/spool/ippeve/` |

| Port | Service |
|---|---|
| 631 | CUPS scheduler |
| 8631 | `ippeveprinter`, for direct IPP testing: set `LATIPRIMERE_IPP_PRINTERS=ipp://localhost:8631/ipp/print` and print to `ipp:ipp://localhost:8631/ipp/print` |

## Using it with Latiprimere

From Windows (or any host), point the CUPS adapter at the container over TCP:

```bash
CUPS_SERVER=localhost:631 dotnet run --project samples/Latiprimere.Cli -f net10.0 -- list
CUPS_SERVER=localhost:631 dotnet run --project samples/Latiprimere.Cli -f net10.0 -- print cups:Raw some-file.txt --raw
```

To test the Unix-socket transport, publish the CLI for Linux and run it inside the container, where it finds `/run/cups/cups.sock` by itself:

```bash
dotnet publish samples/Latiprimere.Cli -f net10.0 -r linux-x64 --self-contained -c Release -o out/cli-linux
docker cp out/cli-linux latiprimere-cups:/opt/latiprimere
docker exec latiprimere-cups /opt/latiprimere/Latiprimere.Cli list
```

On Windows with Git Bash, set `MSYS_NO_PATHCONV=1` before `docker exec`/`docker cp` so container paths aren't rewritten.

Inspect results with, for example, `docker exec latiprimere-cups ls /output` or `docker exec latiprimere-cups ipptool -tv ipp://localhost/jobs/3 get-job-attributes.test`.

## Configuration notes

- `cupsd.conf` keeps the stock CUPS job policy so authentication behaves like a default installation.
- The `Secure` queue uses an extra `authenticated` policy: printing needs a valid user (`AuthType Basic`, checked by PAM against the container's accounts) and `Encryption Required`, which stock CUPS applies to authenticated requests from other machines. Over plain HTTP, CUPS answers 426 before it asks for the password. TLS works on port 631 itself, with a self-signed certificate that CUPS creates when it first needs one, so it changes with every container. `latiprimere` / `test-password` is a throwaway account in a container published on `127.0.0.1` only. See [ADR 0020](../../docs/decisions/0020-cups-authentication.md).
- `ServerAlias *` is needed because requests arrive through Docker's network from a non-local address while naming `localhost`; CUPS otherwise rejects them with HTTP 400 (DNS-rebinding protection).
- The `capture` backend exists because CUPS writes nothing for raw jobs on a `file:` device: with no filters to run, the output file is never opened. Its device URI names the output folder: `capture:/` means `/output`.
- The backend sets umask 022, so its files are readable by everyone. CUPS runs backends as `lp` with umask 077, and on a Linux host, where permissions on the bind mount are real, the tests (running as the host user) couldn't read the files. Docker Desktop on Windows ignores those permissions, which hid this until the first Ubuntu CI run.
- D-Bus and Avahi are included only because `ippeveprinter` in CUPS 2.4 refuses to start without DNS-SD.
- `ippeveprinter` runs with `-c /bin/true`, so each job completes as soon as it arrives. Without a print command it pretends to print for a random 5–15 s per job, and it takes one job at a time, answering others with `server-error-busy`. With the test processes for both target frameworks printing to it, plus the Virtual-IPP queue, jobs queued for most of a minute.
- Ports are published on IPv4 only. `localhost` resolves to IPv6 first, so each new connection to it waits 250 ms before Latiprimere tries IPv4 (Happy Eyeballs; it was about 2 s on Windows before that), and tests use `127.0.0.1`.
- `tools/test-documents/` holds the test PDFs (see its README); `three-pages.pdf` was generated in this container with `cupsfilter`.
