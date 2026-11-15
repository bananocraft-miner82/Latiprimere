# Manual test: Spike 4 on a real network

Checks that Latiprimere finds IPP printers through mDNS on a real network and prints straight to them. Part A runs on the Mac and prints to a virtual printer only. Part B runs on Windows and is read-only. Together they take about 10 minutes.

Please paste the output of each step back into the conversation (or add it to [progress.md](../progress.md)).

## Part A: Mac

Build as in [the Spike 1 test](spike-1-macos.md), step 1.

### A1. Start a virtual printer that announces itself

In a second terminal (it stays running):

```bash
ippeveprinter -p 8631 -f application/pdf,image/pwg-raster "Latiprimere Virtual"
```

Unlike the test container, this one announces itself through macOS's own Bonjour.

### A2. List printers

```bash
dotnet run --project samples/Latiprimere.Cli -f net11.0 --no-build -- list
```

**Expect:**

- `Latiprimere Virtual` with the ID `ipp:dnssd:Latiprimere Virtual` and state `Unknown`.
- Any AirPrint printers on your network, also with `ipp:dnssd:` IDs.
- The `cups:` queues as before.

### A3. Print to it directly

The quotes matter, as the name has a space:

```bash
dotnet run --project samples/Latiprimere.Cli -f net11.0 --no-build -- \
  print "ipp:dnssd:Latiprimere Virtual" tools/test-documents/three-pages.pdf
```

**Expect:** `Completed`. Stop `ippeveprinter` with Ctrl+C afterwards.

## Part B: Windows, with the HP ENVY switched on

> Done 2026-11-12: the HP was listed as `ipp:dnssd:HP ENVY 5640 series [<id>]`. Results are in [progress.md](../progress.md).

Only lists the printers; nothing is sent to the printer.

```bash
dotnet run --project samples/Latiprimere.Cli -f net11.0 -- list
```

**Expect:** an entry with an `ipp:dnssd:` ID for the HP, alongside its `windows:` queue.

## What to report

- The output of steps A2, A3 and B.
- Any printer you expected to see that isn't listed.
- Any error, including the "Print system error code" line if one appears.
