# Manual test: Spike 1 on macOS

Checks that the CUPS adapter works against macOS's own CUPS scheduler through its Unix socket, with no configuration. Takes about 15 minutes. Nothing is printed on paper.

Please paste the output of each step back into the conversation (or add it to [progress.md](../progress.md)).

## 1. Prepare

```bash
# .NET 11 SDK (skip if `dotnet --list-sdks` already shows 11.0.x)
brew install --cask dotnet-sdk

# The scheduler socket Latiprimere looks for, and the queues macOS already has
ls -l /private/var/run/cupsd
lpstat -p -d
```

Get the working copy onto the Mac (copy the folder, or push a branch — your choice), then from the repository root:

```bash
dotnet build samples/Latiprimere.Cli -f net11.0
```

## 2. List printers (no configuration)

```bash
unset CUPS_SERVER
dotnet run --project samples/Latiprimere.Cli -f net11.0 --no-build -- list
```

**Expect:** the same queues as `lpstat -p`, each with a `cups:` ID, and the system default marked `[default]`.

## 3. Create a test queue that never prints

A raw queue pointing at a port nothing listens on, then disabled, so jobs wait until cancelled. Needs your admin password.

```bash
sudo lpadmin -p Latiprimere-Paused -E -v socket://127.0.0.1:9 -D "Latiprimere test queue"
sudo cupsdisable Latiprimere-Paused
```

## 4. Submit and cancel a PDF

```bash
dotnet run --project samples/Latiprimere.Cli -f net11.0 --no-build -- \
  print cups:Latiprimere-Paused tools/test-documents/three-pages.pdf --cancel
```

**Expect:** `Submitted job N`, `Cancellation requested`, then a `Cancelled` status.

## 5. Submit raw data and cancel

```bash
printf 'Latiprimere raw test\n' > /tmp/raw.txt
dotnet run --project samples/Latiprimere.Cli -f net11.0 --no-build -- \
  print cups:Latiprimere-Paused /tmp/raw.txt --raw --cancel
```

**Expect:** as step 4.

## 6. Optional: a job that completes

Only if `which ippeveprinter` finds it on your Mac. In a second terminal:

```bash
ippeveprinter -p 8631 -f application/pdf,image/pwg-raster "Latiprimere Virtual"
```

Then:

```bash
sudo lpadmin -p Latiprimere-Virtual -E -v ipp://localhost:8631/ipp/print -m everywhere
dotnet run --project samples/Latiprimere.Cli -f net11.0 --no-build -- \
  print cups:Latiprimere-Virtual tools/test-documents/three-pages.pdf
```

**Expect:** `Processing`, then `Completed (3 pages)`.

## 7. Clean up

```bash
sudo lpadmin -x Latiprimere-Paused
sudo lpadmin -x Latiprimere-Virtual   # only if created in step 6
```

## What to report

- Output of steps 1, 2, 4 and 5 (and 6 if run).
- Any difference between `lpstat -p -d` and the `list` output.
- Any error, including the "Print system error code" line if one appears.
