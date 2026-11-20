# Manual tests

Checks that need a person: what came out of a real printer, or a machine the automated tests can't reach.

## Real printers: the harness's manual mode

The end-to-end harness checks a printer you name, which may be real, and prints on paper only when you say so ([ADR 0029](../decisions/0029-end-to-end-test-harness.md), decision 5). It takes about 10 minutes and at most 10 sheets.

1. **Find the printer's ID** with the CLI sample:

   ```bash
   dotnet run --project samples/Latiprimere.Cli -f net11.0 -- list
   ```

   On Windows, use the queue's `windows:` ID: it prints PDFs through the driver, which every printer takes. A network printer's `ipp:` ID prints PDFs only if the printer accepts PDF itself; the HP ENVY 5640, for one, doesn't.

2. **Load plain paper,** A4 or Letter, and a sheet in each tray you want to try.

3. **Run the checks:**

   ```bash
   dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- manual --printer "windows:<the queue name from list>"
   ```

4. **Answer as you go.** The harness:
   - asks you to confirm the printer, then shows its details and capabilities and asks whether they are right;
   - saves its IPP attributes, if it has an IPP address (a `cups:` queue or an `ipp:` printer);
   - before each test job, shows the document, page count, copies and options, and prints only if you answer `y`;
   - after each job, asks what came out: `y` for yes, `n` for no (then what you saw), `s` if you can't tell.

   Anything but `y` at "Print it on paper now?" skips that job. Ctrl+C stops the run; the report is still written.

5. **Report the results.** The run writes `out/harness/<time>-<os>-<framework>-manual/report.md`, with every answer and the saved attributes. Paste the report into [progress.md](../progress.md).

| Scenario | Prints | Asks |
|---|---|---|
| `manual.printer-details` | Nothing | Is it the right printer? Are its capabilities right? |
| `manual.test-page` | One page, the printer's defaults | One page? All of it there, nothing cut off? In colour? |
| `manual.paper-and-colour` | One page on A4 (or Letter), monochrome | Right paper? Grey only? |
| `manual.two-sided-and-copies` | Two copies of three pages, two-sided if the printer can | Two copies in page order? Two-sided, turning like a book? |
| `manual.landscape` | One page, landscape | Printed across the paper? |
| `manual.tray` | One page from the tray you choose | From that tray? |

A scenario is skipped when the printer doesn't report what it would check, such as trays. Run some only by naming them: `manual --printer <id> manual.test-page manual.tray`.

**A printer over IPPS** (`ipp:ipps://…`) presents its own certificate, which isn't trusted until it's pinned. Run the checks once without it: they fail, giving the certificate's SHA-256 fingerprint. Check that against the printer's own web page, then pin it with `--fingerprint`, which you can repeat. It's trusted for direct IPP, for a Windows queue sent to its printer over IPPS, and for saving the printer's attributes:

```bash
dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- manual --printer "ipp:ipps://192.0.2.10:443/ipp/print" --fingerprint 3A:7F:…
```

Expected differences:

- **CUPS queues** keep a PDF's own orientation, so `manual.landscape` prints portrait there.
- **Microsoft Print to PDF** saves each job into the report's folder rather than asking where.

## Saving a printer's attributes only

To add a printer's attributes to the test data without printing anything:

```bash
dotnet run --project tests/Latiprimere.TestHarness -f net11.0 -- capture ipp://192.0.2.10/ipp/print --name my-printer-printer-attributes
```

It saves `.ipp` and `.txt` files in `out/captures` and lists the values that identify the printer or its network. Replace those with placeholders of the same length before adding the files to `tests/Latiprimere.Ipp.Tests/Data` (see its README).

## Earlier checklists

These were written for the proof of concept, before the harness existed, and are kept as the record of those checks:

- [Spike 1 on macOS](spike-1-macos.md): the CUPS adapter against a Mac's own CUPS.
- [Spike 4 on a real network](spike-4-network.md): mDNS discovery and direct IPP.
