# Progress Log

Newest entries first. Each entry records what was done, what was learned, and what's next. Milestone checkboxes live in [plan.md](plan.md).

## Current status

| Item | Value |
|---|---|
| Phase | 4: PCLm, images and text, and events, before 1.0 ([phase-4-plan.md](phase-4-plan.md), accepted 2026-11-20). P4-1 to P4-5 and P4-8 to P4-10 done ([ADR 0045](decisions/0045-pdf-writer.md) to [ADR 0049](decisions/0049-following-printers-and-jobs.md), [ADR 0054](decisions/0054-rasteriser-options.md) to [ADR 0056](decisions/0056-typed-options-before-1-0.md)). P4-6 done 2026-11-23, its API review accepted ([ADR 0057](decisions/0057-api-review-phase-4.md)). P4-7, the sign-off's additions, to come. Phase 3 ([phase-3-plan.md](phase-3-plan.md)) is done but for its checks on the HP, the Mac (P3-8) and the stable switch, which wait for the sign-off |
| Released | `11.0.9-preview` on nuget.org, 2026-11-21, with P4-1 to P4-5. Its post-release pull request is merged into `main`. The next release is a preview with P4-6 and P4-8 to P4-10, or the first stable one, 1.0, after the sign-off ([releasing.md](releasing.md#the-first-stable-release-10)) |
| State | P4-8 to P4-10 and P4-6 are uncommitted on `feature/phasefour-prerelease-extras`. Not yet through CI. Every ADR from 0053 on is accepted (owner). |
| Next step | Owner: commit, and release a preview. Then the sign-off, as [releasing.md](releasing.md#1-the-sign-off) lists it, extended by P4-7: the HP over IPP, IPPS and its Windows queue (each sheet only when you say), and the Mac. Then the stable switch. |
| Blockers | The sign-off waits on the HP being on and on time at a Mac. Owner: delete the leftover branches `post-release/v11.0.4-preview` and `post-release/v11.0.6-preview`, and reserve the `Latiprimere.` ID prefix on nuget.org ([releasing.md](releasing.md#one-time-setup-owner)). |

---

## 2026-11-24: Hardening after an outside security review

**Done** (owner: "Can you analyse the results and identify potential fixes", then "Fix all findings, starting at the top and working down"), recorded in [ADR 0058](decisions/0058-hardening-after-security-review.md), accepted (owner: "Accept ADR 0058"):
- **1. No forced fall back to plain IPP:**
  - `IppNetworkOptions.RequireEncryption` keeps every request to IPPS;
  - a printer found on the network that has been reached over IPPS is never reached over plain IPP again, remembered in the address store as `reachedOverIpps`;
  - the fallback is logged as a warning;
  - a remembered address must report the UUID its printer announced.
- **2. Announced addresses:** loopback, unspecified, multicast, broadcast and cloud metadata addresses, and `localhost`, are skipped (`PrinterAddressRules`). A `.local` name's addresses are held to the same rules when connecting, and the address store drops such entries when it loads.
- **3. Response size:** no change, as SharpIppNext already bounds it (2026-11-21).
- **4. PDFium:** `PdfiumOptions.MaxDocumentBytes` and `RenderTimeout`, by default 2 minutes a band. Bands now render progressively, so a page that outlasts the limit is abandoned with a `PrintException`.
- **5. Certificates pinned at one printer's address:** `IppNetworkOptions.PrinterCertificateFingerprints` and `WindowsOptions.IppPrinterCertificateFingerprints`.
- **6. Loading and supply chain:**
  - `DefaultDllImportSearchPaths` on Latiprimere.Windows (System32) and Latiprimere.Rendering.Pdfium (its own folder and Windows' safe directories);
  - every workflow action pinned to a commit SHA.
- **Docs:** the changelog, the secure and network printer pages, the web app guide, a `SecurePrinters` sample for `RequireEncryption`, and the regenerated API reference.
- **Each choice made configurable** (owner: "Can you make each of the decision options configurable for the package end users where possible"):
  - new `IppNetworkOptions.AllowPlainIppAfterIpps` and `AllowRememberedAddressWithoutUuid`, both off;
  - the rest already had settings: `RequireEncryption`, `PdfiumOptions.MaxDocumentBytes` and `RenderTimeout`, and the per-address pins;
  - left fixed: the address rules, the native library search paths and the pinned actions, with ADR 0058 saying why.
- **Tests:** 3,592 in all on this Windows machine, none failing and 282 skipped, the integration tests whose printers it doesn't have. The new ones cover each fix, including a page of 5,000 shapes that outlasts a render timeout. Not run here: the Linux and macOS builds, the mDNS job and the Native AOT checks, which CI runs.

**Learned**
- **The review read only the docs,** and its CVE for SharpIppNext isn't in the advisory database NuGet uses. `dotnet list package --vulnerable --include-transitive` reports nothing.
- **PDFium pauses progressive rendering about every 100 page objects,** and not while reading a page's content, so `RenderTimeout` bounds drawing, not loading.

**Next**
- Owner: commit, and open the pull request.
- Later: render PDFs in a separate process, so a crash in PDFium can't take the application with it; and fuzz the IPP, PNG, JPEG and PDF readers.

---

## 2026-11-23: P4-6, docs, samples and the API review of Phase 4

**Done** (owner: "Start P4-6", then "Accept all")
- **API review:** [api-review-phase-4.md](api-review-phase-4.md) and [ADR 0057](decisions/0057-api-review-phase-4.md), accepted. The shape held. The new names stay; `PrintOptionKinds` is a `long`; IPP printers no longer list `print-quality`, `output-bin`, `media-type` and `multiple-document-handling` among their vendor options; `Finishing.None`, Windows finishings and output bins, and other additions wait for after 1.0. The vendor options example moved from print quality to pages per sheet.
- **Harness:** seven scenarios: `ipp.submission.typed-options`, `ipp.raster.page-ranges`, `ipp.submission.pdf-password-sent`, `ipp.raster.pdf-password-rendered`, `windows.pdf.password`, `windows.output.finishings-refused` and `cups.output.page-ranges`. The CLI and examples scenarios check the new samples.
- **Native AOT check:** adds a job followed by spooler notifications, the typed options, and password-protected PDFs through PDFium natively.
- **Samples:** the CLI's `print` takes `--pages` and `--quality`, and a PDF's password from `LATIPRIMERE_PDF_PASSWORD`; `caps` lists the new capabilities. The examples print some pages, follow a printer, and print a PDF label on a custom size.
- **Docs:** the options guide gains printing some pages and following a printer; the labels guide, PDF labels on a custom size; the vendor options and capabilities guides no longer say quality, finishing and media types need vendor options. An empty `Finishings` is documented as the printer's default, not none.

**Learned**
- **ippeveprinter lists `document-password-supported`,** so a password-protected PDF to it is sent with its password, not rendered.
- **Following a printer polls every five seconds,** where following a job polls every half second.
- **A PDFium test raced with others:** `GivenPdfiumLockFreeWhenReleasedWithoutWaitingThenClosesTheDocumentAtOnce` assumed PDFium's global lock was free, which tests rendering in parallel could break. It now holds the lock, which is reentrant, while it releases the document.
- **The CI mDNS job failed with 400 Bad Request,** from `ipp://runnervm0hi8g:8631`: the runner's announcement of the test container's ippeveprinter carried no IPv4 address, and Latiprimere fell back to the host name without its domain. Tmds.MDns gives an SRV target's first label as `Hostname` and the rest as `Domain`, and only `Hostname` was kept. ippeveprinter refuses any `Host` header that isn't an address, `localhost` or a `.local` name, its own bare name included (checked against the container). Now the name keeps its domain, and a routable IPv6 address comes before it. The scenario notes each announcement's host, domain, addresses and interface, as why the runner's had no IPv4 address isn't known: perhaps it arrived over IPv6 only, as Avahi doesn't publish IPv4 addresses over IPv6.
- **`windows.ipp-route.fallback-no-pdf` had broken since PCLm (P4-2):** its printer still listed PCLm, so the PDF went to it as raster instead of falling back to the spooler. It runs only with the Windows test printers, which earlier runs here hadn't set up. Its profile now leaves PCLm out.

**Next**
- A preview release with P4-6 and P4-8 to P4-10.
- Then the sign-off, extended by P4-7, and the stable switch.

---

## 2026-11-23: P4-10, typed options before 1.0

**Done** (owner: "Start P4-10"; [ADR 0056](decisions/0056-typed-options-before-1-0.md))
- **Public API:** `PageRange`; `PrintOptions.PageRanges`, `Collate`, `Quality` (`PrintQuality`), `Finishings` (`Finishing`), `OutputBin` and `MediaType`; `MediaSize.Custom` and `MediaSizeRange`; `PrinterCapabilities.CustomMediaSizes`, `MediaTypes`, `PrintQualities`, `DefaultPrintQuality`, `Finishings`, `OutputBins` and `SupportsPageRanges`; six `PrintOptionKinds` flags.
- **IPP and CUPS:** each as its attribute; media type and custom sizes in `media-col`; raster renders only the chosen pages, with sides by the pages printed, and doesn't send `page-ranges`; raw data with page ranges refused; the new capabilities, custom and roll ranges among them.
- **Windows:** only the chosen pages rendered; `dmCollate`, `dmPrintQuality` and `dmMediaType`; custom sizes as `DMPAPER_USER` within the driver's extents; the driver's media types. Finishings and output bins left out of `AppliedOptions`, so the catalog refuses them on Windows.
- **Docs:** the options and capabilities guides, the changelog, CONTRIBUTING.md's list of enums that may grow, the API reference.
- **Tests:** 60 new; 3,366 on both target frameworks, none failing, 268 skipped; the harness passes 60 scenarios, `windows.output.page-ranges` among them, printing pages 2 and 3 of three to Microsoft Print to PDF.

**Learned**
- **Back sides follow the pages printed:** with pages 2 and 3 of three, page 2 is a front. The raster encoder had used the page's place in the document.
- **`DC_MINEXTENT` and `DC_MAXEXTENT` aren't enough to say a driver takes custom sizes;** it must also list `DMPAPER_USER`.

**Next**
- P4-6: the docs, samples and API review of Phase 4's additions, P4-8 to P4-10 among them: the names, IPP's vendor options the typed ones now cover, and finishings and output bins on Windows.
- Then the sign-off, extended by P4-7, and the stable switch.

---

## 2026-11-23: P4-8 and P4-9, rasteriser options and the catalog's own printers

**Done** (owner: "Start P4-8 and P4-9")
- **P4-8, options for the PDF rasteriser** ([ADR 0054](decisions/0054-rasteriser-options.md)):
  - `RasterPixelFormat` and `RasterBand.PixelFormat`; `RasterBand.BytesPerPixel` an instance property, and `GetBytesPerPixel(format)`;
  - `RasterRenderOptions` (smoothing of text, lines and images) for `RenderBand`, and `PdfOpenOptions` (the password, never shown by `ToString`) for `Open`, as optional parameters;
  - `PrintContent.FromPdf(stream, password)`: the Windows spooler and raster render with it; IPP printers taking PDF are sent it as `document-password` when they list `document-password-supported`, and otherwise raster; a CUPS queue without password support refuses;
  - grey inside Latiprimere: IPP raster in grey and black, and Windows monochrome jobs as 8-bit bitmaps with a grey colour table. `GreyscaleConverter` is gone.
- **P4-9, the catalog's own printers** ([ADR 0055](decisions/0055-catalog-printers.md)):
  - `CatalogPrinter` and `CatalogPrintJob` around the print systems' printers and jobs;
  - `IPrinter.GetFeature<T>()` and `IPrintJob.GetFeature<T>()`; the samples, the vendor options guide and the harness ask for `IWindowsPrinter` that way;
  - `PrintOptionKinds` and `IPrinter.AppliedOptions`; a setting a printer doesn't apply is refused before anything is sent.
- **Docs:** the password in the print options guide; the API reference regenerated; CONTRIBUTING.md lists `RasterPixelFormat` among the enums that may grow; the changelog.
- **New test documents:** `line-art.pdf` and `line-art-password.pdf`, from `tools/test-documents/make-line-art.py`, Python's standard library only, as Docker wasn't running for Ghostscript.
- **Tests:** after P4-8, 3,230 on both target frameworks, none failing, 266 skipped for printers this machine doesn't have. After P4-9, 3,244 on both, none failing, 266 skipped, and the harness passes all 59 scenarios this machine can run, 31 skipped without the CUPS container.
- **Package validation:** suppressions for the two rasteriser methods in `Latiprimere.Abstractions` and `Latiprimere.Rendering.Pdfium`; the new interface members with default implementations need none.

**Learned**
- **SharpIppNext writes an octetString given as a string in ASCII,** so `é` became `?`. Given as bytes, it writes them as they are.
- **PDFium's grey rendering isn't Latiprimere's luma of its colour one:** solid colour agrees within a few levels, but text edges and scaled images differ, so grey tests compare with PDFium's grey.
- **Heredocs with apostrophes don't survive this machine's shell tool;** edits went through script files.

**Next**
- P4-10, the typed options, with its ADR. Each new option adds a `PrintOptionKinds` value and an `AppliedOptions` override in each adapter that applies it.
- Then P4-6, whose API review covers P4-8 to P4-10, including the names `GetFeature`, `AppliedOptions` and `PrintOptionKinds`.

---

## 2026-11-23: A capability review, and a companion product for documents

**Done** (owner: five capabilities to check, then "Add the relevant items to our plan. Could we perhaps consider the document generation as a viable compatible side-product that could compliment Latiprimere and work well with it?")
- **The review:** what Latiprimere supports today, against five capabilities:
  - **discovery, capabilities, paper, queues and submission:** mostly. The gaps: no list of a printer's queued jobs, no printers appearing and going, and no printer installation or queue settings, which is a non-goal;
  - **a document and layout API:** no, by design ([ADR 0001](decisions/0001-headless-ipp-first-scope.md));
  - **PDFs:** printed everywhere; made only internally, for images, text and PCLm;
  - **silent printing:** yes, as everything is headless. Preview and dialogs: the pieces are public (`IPdfRasterizer`, `PrinterCapabilities`), the add-ons are for later;
  - **label and receipt printers:** yes, as raw data, without helpers to build it and without raw port-9100 connections.
- **plan.md, "Later":**
  - a printer's queue of jobs (`IPrinter.GetJobsAsync`);
  - ZPL and ESC/POS builders;
  - raw connections over AppSocket, port 9100;
  - preview of any `PrintContent` without a UI framework;
  - a new "Companion product: documents" section, and a pointer to it from the non-goals.
- **[ADR 0053](decisions/0053-document-generation-companion.md), accepted:** document generation as `Latiprimere.Documents.*` packages beside the core, print-first, in stages: a guide to printing other libraries' PDFs, then the ZPL and ESC/POS builders, then a page model with PDF output, embedded fonts, and documents rendered to label and receipt printers.
  - **The owner's answers** (each as recommended): go ahead after 1.0; print-first, leaving reports to QuestPDF and MigraDoc; named `Latiprimere.Documents.*`; the standard 14 fonts first, embedded fonts after; stages 0 and 1 straight after the sign-off.
  - **plan.md** lists the stages as a checklist under "Companion product: documents (after 1.0)".
- **A second review of the core** (owner: "Which other core features do we currently have as gaps and could start planning onto the roadmap?", then "Add all, tiered"): 13 more gaps, in plan.md's "Later" in three tiers.
  - **Tier 1:** page ranges, custom and roll paper sizes, printable area and borderless printing, supplies and alerts, and tracing and metrics. Page ranges, roll sizes and printable area come first, as the Documents companion relies on the last two.
  - **Tier 2:** typed settings for what vendor options reach today, holding and releasing jobs, Kerberos, several documents in one job, and a durable outbox.
  - **Tier 3:** items earlier ADRs left for later: common paper sizes built in, which print systems the catalog skipped, IPP notifications, and more orientations and colour modes.
- **Changes worth making while there are no customers** (owner: "which core features are we missing that would change the interface, but would be worth adding anyway?", then "Yes, plan all three"): three work packages added to [phase-4-plan.md](phase-4-plan.md), with decisions D11 to D18, all accepted as recommended (owner: "Accept all"):
  - **P4-8, options for the PDF rasteriser:** grey as well as BGRA bands, smoothing that can be turned off, and passwords. `IPdfRasterizer` and `IRasterDocument` are frozen for good at 1.0;
  - **P4-9, a layer around every printer:** the catalog wraps printers and jobs, platform features are reached through a member instead of `is IWindowsPrinter`, and options an adapter doesn't say it applies are refused, so none is ignored without a word;
  - **P4-10, typed options before 1.0:** page ranges and collation, custom and roll paper, quality, finishings, output bin and media type. `MediaSize` stays only a size, so its equality never changes.
  - **Checked, and fine to add after 1.0:** listing jobs, holding and releasing them, several documents in one job, supplies, printers appearing and going, and "offline", which every print system already reports as `offline-report`.

**Learned**
- **Phase 4 built most of a document pipeline already,** internal to `Latiprimere.Ipp`: the PDF writer, image placement, text layout and dithering. A companion product could start from that, through shared source, without making it a public API.
- **The opening is labels and receipts, not reports.** QuestPDF, PDFsharp/MigraDoc and iText cover general PDFs, and their output prints today through `PrintContent.FromPdf`. None of them makes ZPL or ESC/POS from a page model.

**Next**
- P4-8 and P4-9, side by side, then P4-10, each with its ADR; then P4-6, whose API review covers them. 1.0 moves back by three to four weeks.
- Then, as before: the 1.0 sign-off and the stable switch, as in "Current status".
- After the sign-off: ADR 0053's stage 0, the guide, and stage 1, the ZPL and ESC/POS builders, with an ADR for the builders' API first.
- Then tier 1 of the core gaps, starting with page ranges, roll sizes and printable area, each with its ADR.

---

## 2026-11-21: A sweep for vulnerabilities and bugs, and its fixes

**Done** (owner: "perform a code sweep and look for any potential vulnerabilities, bugs, security/performance issues etc. Are the dependencies all up-to-date?", then "Yes, fix 1 to 4")
- **Dependencies:**
  - none has a known vulnerability, direct or transitive;
  - none of the libraries' is deprecated;
  - the only update is Microsoft.Extensions.* 11.0.0, held back on purpose (dependabot.yml);
  - Microsoft.Testing.Platform 2.5.2, which xunit.v3 4.0.3 brings into the tests only, is marked legacy.
- **1. Remembered printers had no limit** ([ADR 0038](decisions/0038-remembered-addresses.md), amended).
  - **The problem:** anyone on the network could announce printers without end. Each was kept in memory and in the address store, came back after every restart, and kept its address known.
  - **The fix:**
    - at most 256 are remembered, and past that the one seen longest ago is forgotten;
    - a printer not seen for 90 days is forgotten;
    - the store records `lastSeen`, and isn't rewritten for a printer seen again within a day;
    - an oversized store shrinks on loading.
- **2. Printers' answers have no size limit of Latiprimere's own: checked, and not a problem.**
  - **The check:** a test printer sent answers that never end, in three shapes, with values of 1,000 bytes. SharpIppNext stopped each within 2.5 to 5 MB, at about 5,000 values or 2,500 groups. A value longer than IPP allows was refused after about 100 bytes.
  - **So nothing was added,** but [SharpIppResponseLimitTests](../tests/Latiprimere.Ipp.Tests/SharpIppResponseLimitTests.cs) keeps these limits tested, so an update to SharpIppNext that drops them fails the build.
- **3. A band's row width was worked out as an `int`:** `PdfiumDocument` checked a band's stride against `PageWidth * 4` as an `int`. A page wider than about 537 million pixels wrapped it negative, so any stride passed, and only PDFium refusing the bitmap stood between the band and a write past the caller's pixels. A test showed exactly that before the fix. The sum is now a `long`.
- **4. The PDFium finaliser no longer waits for the PDFium lock.**
  - **The problem:** a document left undisposed was closed on the finaliser thread, which serves the whole process, while another thread might be rendering.
  - **The fix:** it now tries the lock. If the lock is held, the document is queued, and the next PDFium call closes it (`PdfiumLibrary.ReleasePending`).
- **Checked and sound:**
  - the PNG, JPEG and EXIF readers;
  - certificate trust, and credentials only over TLS;
  - the address store's atomic writes;
  - raster size limits;
  - Print Schema XML, with no DTDs;
  - spooler buffers and handles;
  - Happy Eyeballs, which cancels and closes losing attempts;
  - the response timeout;
  - logging, which writes no secrets.
- **Tests:** 3,200 in all, none failing and 266 skipped, the integration tests whose printers this machine doesn't have. Of those, 24 are new: 12 tests, run on both target frameworks. Turning off the forgetting fails three of the new tests. The Windows tests' `ManualTimeProvider` moved to `Latiprimere.Ipp.Testing` for both to share.

**Learned**
- **SharpIppNext limits what it reads** by count and by IPP's length rules, not by bytes, which bounds an answer at a few megabytes.

---

## 2026-11-21: More reliable and faster CI

**Done** (owner: "Can this be more reliable, streamlined, improved and perhaps sped up in any way without affecting what we're doing?", then "Yes, 1 to 6"). A look at the last 100 runs found why runs fail, and six changes. None changes what's tested or how releases work.
- **The release waits for nuget.org before the post-release steps** ([wait-for-packages.sh](../tools/releasing/wait-for-packages.sh)).
  - **The cause:** after 11.0.5, 11.0.7 and 11.0.8, the post-release builds failed at Restore on every runner, and the mDNS job with them. post-release.sh makes the version just released the package-validation baseline, and the build started before nuget.org had indexed it. Each needed re-running by hand, up to four times.
  - **The fix:** the finish job now polls nuget.org's flat container until all six packages are served, for up to 30 minutes.
- **Time limits:**
  - each job gets one: 45 minutes for a build and 20 for mDNS, and 10 to 45 for the release's jobs;
  - the test run gets 30 minutes.

  Before, a hang ran to GitHub's six-hour default, as a release did on 2026-11-17.
- **Test results are kept when a job fails:** each test project writes HTML and JUnit reports, uploaded as `test-results-<os>`. The log lists the ten slowest tests. Before, a flaky test couldn't be named without the raw log.
- **The CUPS test container:**
  - it's built with Docker's layer cache in GitHub's cache, shared by the build and mDNS jobs, rather than from scratch twice a run;
  - its `apt-get` retries downloads.
- **NuGet packages are cached between runs,** except in release builds, which restore everything from nuget.org.
- **A new push to a pull request cancels its previous build.** Pushes to main, release builds and builds started by hand are never cancelled.
- **[releasing.md](releasing.md)** says finish waits, rather than asking for a re-run.

**Learned**
- **The flaky tests themselves aren't named yet.** GitHub's logs need a sign-in to download, and CI kept no results. The test-results artifact will name them from the next failure.
- **Not tested locally:** the Docker cache steps, as Docker isn't installed here, and the workflow-level concurrency inside a release's call to build.yml. The first pull request build tests the first; the next release tests the second.

**Next**
- **Once some runs have the slowest-tests list and step timings:** consider running the Native AOT checks as parallel jobs.
- **Fix the flaky tests** that the test-results artifacts name.

---

## 2026-11-21: Security policy, SBOMs and groundwork for the Cyber Resilience Act

**Done** (owner: "Add the CRA groundwork now"), recorded in [ADR 0051](decisions/0051-security-policy-sbom-and-cra.md), proposed:
- **[SECURITY.md](../SECURITY.md):**
  - private reports through GitHub or `security@latiprimere.com`;
  - response goals for one developer;
  - 90-day coordinated disclosure, and advisories with CVEs;
  - supported versions and the support period;
  - scope;
  - the manufacturer's details.
- **An SBOM in every package:**
  - [generate-sboms.sh](../tools/releasing/generate-sboms.sh) writes a CycloneDX SBOM for each package, with the CycloneDX tool 6.3.1 from [dotnet-tools.json](../dotnet-tools.json).
  - build.yml's Pack step puts it in the package at `sbom/<package>.cdx.json`, checks that every package has one, and keeps the SBOMs as an artifact.
  - The release publishes those packages, so nuget.org gets the SBOMs too.
- **Security updates after a Commercial Licence ends:** change 12 of [COMMERCIAL-TERMS.md](../COMMERCIAL-TERMS.md). A lapsed buyer may ship Latiprimere's security updates to the copies it shipped while licensed, for the support period, but not use Latiprimere in its own operations. [COMMERCIAL.md](../COMMERCIAL.md) says so too.
- **[cyber-resilience-act.md](cyber-resilience-act.md):**
  - what the Act asks of Latiprimere, and what's done;
  - the steps for reporting an actively exploited vulnerability;
  - what's left before 11 December 2027.
- **README.md and the package README** gain a Security section.
- **The private list of questions for a lawyer** moved to `private/`, which git ignores. Public documents no longer link to it.

**Learned**
- **Microsoft.Sbom.Targets wasn't accurate enough.** It listed build-only analyzers, Latiprimere itself, and leftover pack files from `obj/` as dependencies. The CycloneDX tool works from the restore's assets file, and marks build-only packages `excluded`.
- **The CycloneDX tool reads its metadata template only as XML.**

**Next**
- **Owner:**
  - enable private vulnerability reporting on GitHub;
  - set up `security@latiprimere.com`;
  - register on ENISA's Single Reporting Platform;
  - confirm the support period and response goals in ADR 0051.

---

## 2026-11-21: Commercial Licence agreement drafted

**Done** (owner: "Go with drafting the commercial licence from Common Paper's agreement")
- **[COMMERCIAL-TERMS.md](../COMMERCIAL-TERMS.md):** the Commercial Licence as a Cover Page to Common Paper's Software License Agreement Standard Terms 1.1, under Dutch law. It's recorded in [ADR 0050](decisions/0050-commercial-licence-agreement.md), which is proposed. Owner decisions:
  - renewal: yearly, until cancelled;
  - liability: capped at a year's fees, with no IP indemnity;
  - support: a 3-business-day goal for replies, not a promise.
- **[COMMERCIAL.md](../COMMERCIAL.md):** links to the terms. It also gives the new renewal rule, refunds on renewals, and the support goal.
- **The Provider** is Jonathan Price, trading as Prodigy Software Solutions, in Rucphen. Support mail goes to `support@latiprimere.com`. The draft names the Rechtbank Zeeland-West-Brabant, the court for Rucphen, as the chosen court.
- **A private list of questions for a lawyer** (owner: "Keep a log of everything I need to go through with a lawyer for later"), outside the repository's history. It covers the three licence documents, the business, insurance, and the Cyber Resilience Act and Product Liability Directive.

**Learned**
- **Common Paper's licence is to install and use the software,** so a library needed more: the right to ship it inside the buyer's products, and to modify its public source.
- **The seller is the copyright holder:** a sole proprietorship is Jonathan Price himself, so no transfer of rights is needed. A BV would need one.
- **Latiprimere has no security policy or vulnerability contact.** The Cyber Resilience Act's reporting duties have applied since 11 September 2026.

**Next**
- **Owner:** confirm the drafted defaults listed in ADR 0050, then accept it.
- **Owner:** add the KvK number once the business is registered, and set up `support@latiprimere.com`.
- **A lawyer:** go through the owner's private list.

---

## 2026-11-20: Review of Phase 4's changes, and fixes

**Done** (owner: "Run a sweep for any potential bugs, security/performance issues etc introduced", then "Fix all of them")
- **A review of P4-1 to P4-5** found 10 issues; all are fixed. The changes are recorded as amendments to [ADR 0047](decisions/0047-printing-images.md), [ADR 0048](decisions/0048-printing-plain-text.md) and [ADR 0049](decisions/0049-following-printers-and-jobs.md).
- **Correctness:**
  - **refused text on Windows:** a Windows queue sent over IPP no longer takes text refused for a character for a raster refusal, so it no longer tries the driver and reports the wrong reason;
  - **a failed change notification:** it is closed and the watch polls, rather than spinning;
  - **network printers that list no formats:** they get JPEGs as they are and PNGs and text laid out as a PDF, rather than being treated as CUPS;
  - **printer watches:** they report a failed read as `Unknown` and go on;
  - **Windows capabilities:** with the IPP route on, they list what the queue's IPP printer takes.
- **Security:** images are capped at 300 million pixels in all, against decompression bombs.
- **Performance:**
  - CUPS text is re-encoded as it uploads (`Utf8TextStream`);
  - images are read once from seekable streams;
  - text-only drivers are refused before anything is converted;
  - a printer's default paper is kept for five minutes.
- **Tests:** a test for each fix, including a surrogate pair split across the transcoder's buffer, a notification that fails, a printer that goes and comes back, and the paper kept and read again. The Windows watch and routing tests were repeated five times without a failure.

**Learned**
- **The routing tests had built their IPP context with CUPS's formats,** where the real route uses the standard ones; that only mattered once CUPS's behaviour was keyed to the print system rather than to missing formats.

---

## 2026-11-20: P4-5, following printers and jobs

**Done** (owner: "Proceed with P4-5")
- **Public API** ([ADR 0049](decisions/0049-following-printers-and-jobs.md), accepted by the owner: "Accept ADR 0049"):
  - `PrinterStatus`: state, message, IPP reasons, whether it accepts jobs;
  - `IPrinter.GetStatusAsync` and `IPrinter.WatchStatusAsync`, and `IPrintJob.WatchStatusAsync` as a member, all with default implementations that poll, so classes of your own keep compiling;
  - `PrinterExtensions.WatchStatusAsync` with a 5-second `DefaultPollInterval`;
  - `PrintJobExtensions` kept. Its overload with an interval is hidden, so code compiled against earlier versions still runs; package validation passes against 11.0.6-preview.
- **Windows:**
  - jobs and printers followed by `FindFirstPrinterChangeNotification`, waited on by the thread pool;
  - re-read at least every 30 seconds in case a change isn't signalled;
  - polled when the spooler can't signal;
  - status flags given as the IPP reasons that match.
- **IPP and CUPS:** `GetStatusAsync` reads the printer's state attributes; watching polls.
- **Tests:**
  - the default members and extensions;
  - the IPP printer's status;
  - Windows notifications, with signals that change nothing, the fallback, and jobs to their end, each repeated five times without a failure.
- **Harness:** `cups.printer-status.paused`, `windows.printer-status.paused`, `ipp.printer-status.polled`, and `windows.jobs.watch-by-notification`, where a job followed with an hour between polls is seen cancelled at once. All pass here; the CUPS container was stopped afterwards.

**Learned**
- **CUPS 2.4 doesn't wait on Get-Notifications:** with `notify-wait` set, it answered at once with no events and `notify-get-interval` 60. With the HP and `ippeveprinter` offering no subscriptions, IPP notifications would have been slower than polling, so they weren't built (the plan expected otherwise).
- **Default interface members are reached only through the interface:** tests holding a fake class cast it to `IPrintJob`.

**Not done**
- **IPP notifications:** for a printer that honours `notify-wait`, when one is at hand.

---

## 2026-11-20: P4-4, plain text

**Done** (owner: "Go for P4-4")
- **Public API** ([ADR 0048](decisions/0048-printing-plain-text.md), accepted by the owner: "Accept ADR 0048"), all additive:
  - `DocumentFormat.Text`;
  - `PrintContent.FromText(string)` and `FromText(stream, encoding)`;
  - `PrintContent.TextEncoding`.
- **Where text goes:**
  - **CUPS:** as UTF-8 `text/plain`, re-encoded from any encoding; CUPS lays it out with any character its fonts have;
  - **direct IPP:** always a PDF Latiprimere lays out, sent as any PDF (or as raster);
  - **Windows:** the same PDF through the driver, or the IPP route.
- **The layout** (`TextDocumentConverter`, `TextPageWriter`):
  - Courier at 10 points within half-inch margins: 87 × 64 on A4, 90 × 60 on US Letter;
  - landscape when the job asks;
  - every kind of line end, wrapping at the last space that fits, tabs every 8 columns;
  - form feeds without stray blank pages, and one blank page for empty text.
- **Characters:** Windows-1252 only, with tab, line ends and form feed. Anything else is refused with `NotSupportedException` naming its code point, line and column, before anything is sent.
- **Shared with images:** `DocumentConverter` now turns both into PDFs, and the refusal for a printer that can take neither names the document's format.
- **Tests:** 40 new, run on both frameworks (3,092 in all, none failing):
  - each page's lines read back from the PDF: positions, wrapping, tabs, line ends, form feeds, encodings, refusals;
  - PDFium drawing a line where it belongs;
  - the scripted IPP client, the fake spooler and the IPP route.
- **Harness:** `ipp.text.as-pdf`, `ipp.text.character-refused`, `windows.text.print-to-pdf` and `cups.text.cups-pdf-output`, all passing here, the last in the CUPS container (stopped afterwards). Microsoft Print to PDF's output was looked at: the tabbed table lines up, accents and € show, the long line wraps at a space, and the form feed starts page 2. The Native AOT check now runs `ipp.text.as-pdf`.
- **Test document:** `text-2-pages.txt` in `tools/test-documents`.
- **CLI sample:** `.txt` files print as UTF-8 text.
- **Docs:** how it works, print options, network printers, CUPS and Windows; CHANGELOG.

**Not done**
- **Other scripts** outside CUPS: they need an embedded font (D6).
- **Text on the HP's paper:** in the sign-off.

---

## 2026-11-20: P4-3, JPEG and PNG images

**Done** (owner: "Start P4-3")
- **Public API** ([ADR 0047](decisions/0047-printing-images.md), accepted by the owner: "Accept ADR 0047"), all additive:
  - `DocumentFormat.Jpeg` and `Png`, `PrintContent.FromJpeg` and `FromPng`;
  - `ScalingMode` and `PrintOptions.Scaling`;
  - `PrinterCapabilities.DocumentFormats`, filled by every print system.
- **Where images go:**
  - **CUPS:** as they are, with `print-scaling`;
  - **direct IPP:** a JPEG as it is to a printer that takes JPEG; otherwise a one-page PDF on the job's paper or the printer's default, sent as any PDF (or as raster);
  - **Windows:** the same PDF, through PDFium and the driver in the page's orientation, or the IPP route for queues sent over IPP.
- **The page:** turned by EXIF; landscape when the image is wider than tall unless `Orientation` says otherwise. `Fit` and the default keep 5 mm margins, and an image that gives no resolution is 96 dpi.
- **JPEG:** headers read, never decoded; codings PDF can't carry refused; Adobe CMYK inverted.
- **PNG:** a decoder written here:
  - every colour type, depth, filter and Adam7, palettes and `tRNS`, CRCs checked;
  - transparency laid over white;
  - rows streamed into the PDF;
  - opaque 8-bit images passed through with PDF's PNG predictors.
- **Tests:** 135 new, run on both frameworks (3,004 in all, none failing):
  - the PNG decoder against a test encoder written apart from it, pixel by pixel;
  - JPEG and EXIF headers;
  - all eight EXIF orientations and PNG colours through PDFium;
  - the layout;
  - the scripted IPP client, the fake spooler and the IPP route.
- **Harness:** `ipp.images.jpeg-as-is`, `ipp.images.png-as-pdf`, `ipp.images.png-as-raster`, `windows.images.print-to-pdf` and `cups.images.cups-pdf-output`, all passing here, the last in the CUPS container. Microsoft Print to PDF's output was looked at: the bar on top, the red square bottom right, the transparent disc white. The Native AOT check now runs `ipp.images.png-as-raster`.
- **Test images:** `photo-300x200.png` and `.jpg` in `tools/test-documents`, made by `make-images.py` and `make-images.ps1`.
- **CLI sample:** `.png`, `.jpg` and `.jpeg` files print as images; `caps` lists the formats.
- **Docs:** how it works, print options, network printers, CUPS and Windows; CHANGELOG.

**Learned**
- **CUPS turns a wide image onto portrait paper itself:** CUPS-PDF wrote portrait A4 pages for the landscape test image, filled to 87%.

**Not done**
- **PngSuite,** which the plan named: its images need downloading, which I haven't done without asking. The test encoder covers the same colour types, depths, filters and interlacing.
- **A photo on the HP's paper:** in the sign-off.

---

## 2026-11-20: P4-2, PCLm and the order of raster formats

**Done** (owner: "Start P4-2")
- **PCLm** ([ADR 0046](decisions/0046-pclm-and-raster-format-order.md), accepted by the owner: "Accept ADR 0046"):
  - **`PclmWriter`,** beside the PWG and Apple raster writers, on P4-1's PDF writer: `%PCLm 1.0` on the second line, each page the paper's size, lines gathered into strips and written as each fills, so a page is never held whole;
  - **the planner** reads `pclm-source-resolution-*`, `pclm-strip-height-*` (at most 256 lines, else 16), `pclm-compression-method-preferred` (the first of `flate` and `rle`, else Flate) and `pclm-raster-back-side`. For the HP that's 600 dpi, strips of 32, run-length, rotated backs;
  - PCLm has no 1-bit form, so the planner never asks for it and the writer refuses it.
- **The order of raster formats:**
  - **new public API:** the enum `IppRasterFormat`, `IppNetworkOptions.RasterFormats` and `WindowsOptions.IppRasterFormats`. By default PWG raster, Apple raster, then PCLm, so nothing changes for printers that take either of the first two;
  - **an empty list** turns raster off. A PDF to a printer printed to directly is then refused, and when the printer takes a format the list leaves out, the message names the formats and the option. A Windows queue prints such PDFs through its driver.
- **Tests:** 26 new, on both frameworks:
  - PCLm strips decoded from their streams and compared line by line with PWG raster of the same PDFs: grey and colour, Flate and run-length, odd and over-tall strips, rotated back sides;
  - PDFium reading the PCLm back with the strips in place;
  - the planner's PCLm choices and the format order;
  - the scripted printer: PCLm only, PCLm first, and none allowed.
- **Harness:** `ipp.raster.pclm-pages` and `ipp.raster.format-order`, both passing. The Native AOT check now runs `ipp.raster.pclm-pages` too.
- **Docs:** the network printers guide on raster formats and `RasterFormats`; the Windows guide on `IppRasterFormats`; CHANGELOG.

**Learned**
- **PDFium smooths images it scales,** even by a hair: rendering a PCLm page at its own resolution gives pixels up to 2.6 levels off on average, not exact. Strips 32 lines out of place would be 5 to 19 levels off. So the exact check decodes the strips as a printer does, and PDFium only checks where they land.

**Not done**
- **The HP on paper,** with `RasterFormats` set to PCLm first: in the sign-off, as the plan has it. A printer may read PCLm more strictly than PDFium does.

---

## 2026-11-20: P4-1, a small PDF writer

**Done** (owner: "Start P4-1")
- **`Latiprimere.Ipp.Documents`** ([ADR 0045](decisions/0045-pdf-writer.md)), internal:
  - **`PdfObjectWriter`:** objects by reserved number in any order, streams, the cross-reference table and trailer. Offsets are counted as bytes go out, and a stream of unknown length gets an indirect `/Length`, so the output needn't seek and data is never held whole;
  - **`PdfDocumentWriter`:** pages, images drawn into rectangles, and Courier text, with each image written as it is drawn and each page as it ends;
  - **compression:** Flate through .NET, run-length through an encoder written here (the HP prefers it for PCLm), and data passed through, such as a JPEG;
  - **text:** in Windows-1252, by a table written here; any other character refused with its code point and position.
- **No new dependency;** the writers are `IDisposable` without closing the output.
- **Tests:** 66 new, on both frameworks:
  - byte-level checks of every cross-reference entry and indirect length;
  - documents rendered by PDFium and their pixels checked: Flate and run-length images, a JPEG passed through, escaped text, three page sizes;
  - a 4 MB image streamed to a stream that can't seek;
  - run-length against a separate decoder.
- **Test data:** `quadrants-64x48.jpg`, made with Windows' `System.Drawing`, for JPEG pass-through.
- **Tidied:** the test helper `RecordingStream` from earlier today moved out of its test class into `Fakes`, as CONTRIBUTING.md asks for helper types.

**Not done**
- **Nothing uses the writer yet:** P4-2, PCLm, is next.

---

## 2026-11-20: Phase 4 planned

**Done** (owner: "Let's also plan to add these now. There's no harm starting more feature-rich than the original plan.")
- **[phase-4-plan.md](phase-4-plan.md),** accepted with every decision as recommended (owner: "Accept all"), brings three later items forward so 1.0 starts with them:
  - PCLm, with a choice of raster format;
  - images and plain text;
  - printer and job events.

  Seven work packages, about four to five weeks, with ten decisions proposed. plan.md has the Phase 4 checklist.
- **Found while planning:** neither the HP nor `ippeveprinter` lists any IPP subscription operation. So events would come from CUPS (`ippget`) and the Windows spooler's change notifications, and most network printers would still be polled. The plan says so, and keeps polling as the fallback.

**Next**
- P4-1, the PDF writer, then P4-2, PCLm. P4-5, events, can run alongside them in another session.

---

## 2026-11-20: The CLI sample and IPPS printers

**Done** (owner: "Start the CLI")
- **`LATIPRIMERE_IPP_FINGERPRINTS`** lists, comma-separated, the SHA-256 fingerprints of printer certificates the CLI trusts over IPPS, as `IppNetworkOptions.TrustedCertificateFingerprints` does. Before, it couldn't print to a printer with the self-signed certificate printers nearly always have.
- **A refused certificate** is followed by the line to set, `LATIPRIMERE_IPP_FINGERPRINTS=<fingerprint>`, after checking it against the printer's web page. The library's message names `IppNetworkOptions`, which a CLI user doesn't set.
- **A fingerprint that isn't one** exits with 64, as a malformed address does.
- **Each job status line shows the job's reasons,** such as `[job-completed-successfully]` ([ADR 0043](decisions/0043-job-state-reasons.md)).
- **The harness scenario `samples.cli.ipps-printer`** runs the CLI program against the scripted printer over TLS, checking all of that: it passes, as does `samples.cli.ipp-printer`.

---

## 2026-11-20: CI coverage for today's work and the release scripts

**Done** (owner: "Yes, start those")
- **The Native AOT check runs three more scenarios** (`tools/aot-check/check-aot.sh`):
  - `ipp.faults.stalled-upload`, the stall guard's handler, content and stream ([ADR 0041](decisions/0041-upload-stall-timeout.md));
  - `ipp.lifecycle.cancel-waiting-job`, job reasons through the raw Get-Job-Attributes that SharpIppNext maps to its typed response ([ADR 0043](decisions/0043-job-state-reasons.md));
  - `windows.ipp-route.password-over-ipps`, TLS with a pinned certificate and a password on the Windows route, on the Windows runner ([ADR 0042](decisions/0042-ipps-for-windows-queues-and-uri-authentication.md)).

  Here, natively, on .NET 11 and .NET 10: no trimming or AOT warnings, and every scenario this PC can run passes, the three new ones among them.
- **The release scripts are checked in CI** on the Ubuntu runner, as they run only at a release, and the stable switch only once:
  - **ShellCheck** over `tools/releasing/*.sh`: clean, once two false alarms were marked as intended, as `prepare-release.sh` already marked a third;
  - **`tools/releasing/test-release-scripts.sh`** runs the stable switch and the README status check on scratch copies of the files, with LF and with CRLF line endings: 58 checks. A copy of `switch-to-stable.sh` with a step removed fails it. Once the README says stable, after 1.0, it tests only the status check, so the step keeps passing.
- **The README status check is a script of its own,** `tools/releasing/check-readme-status.sh`, which `prepare-release.sh` calls, so it can be tested without nuget.org. Rechecked through `prepare-release.sh` on the scratch clone: a stable version is still refused while the README says preview.

---

## 2026-11-20: The Windows test printers

**Done** (owner installed them: "Those windows route scenarios installs are sorted")
- **The five Windows route scenarios that need them all pass,** with `LATIPRIMERE_TEST_WINDOWS_OUTPUT` at `C:\ProgramData\Latiprimere\TestOutput`: `fallback-no-pdf`, `fallback-unreachable`, `fallback-password`, `refusal-not-resent`, and the new `fallback-password-for-jobs` ([ADR 0042](decisions/0042-ipps-for-windows-queues-and-uri-authentication.md)), whose printer answers questions freely and asks for a password only to print, and gets no Validate-Job or Print-Job.
- **Every harness scenario:** 55 passed, none failed, 19 skipped: CUPS (the container was stopped), mDNS, and the manual checks.
- **The Windows tests:** 786, none failed. One test was skipped on both frameworks, as it needs `ippeveprinter` from the container.
- **Then with the CUPS container as well** (owner: "Start the container and run the remaining scenarios"):
  - **every harness scenario,** on both .NET 11 and .NET 10: 66 passed, none failed, 8 skipped. The skips are mDNS discovery, which needs a Linux host's network, as Docker Desktop doesn't pass multicast through, and the seven manual checks, which need a person and a real printer;
  - **every test:** 2,534, none failed, 148 skipped. The Windows test that needs `ippeveprinter` passes too. Every skip is the end-to-end xUnit host, which runs only with `LATIPRIMERE_TEST_E2E=1`, and whose scenarios are the ones the console harness ran above.

---

## 2026-11-20: The CUPS integration test, against the container

**Done** (owner: "Start Docker and run the CUPS integration test")
- **Docker started,** and the CUPS test container with it.
- **The test failed as first written:** CUPS gives the cancelled two-sided job only `processing-to-stop-point`, which Latiprimere doesn't explain, and no `job-state-message`. Its reason is in `job-printer-state-message`, a CUPS extension: "Printer cannot print with supplied options."
- **Fixed** ([ADR 0043](decisions/0043-job-state-reasons.md), "Outcome against CUPS"):
  - Get-Job-Attributes asks for `job-printer-state-message` too, in its raw form, since SharpIppNext doesn't model it, with the typed response still read from the same answer;
  - a job with no `job-state-message` takes it instead, ahead of Latiprimere's explanation of the reasons;
  - the test passes on both target frameworks, and three new unit tests cover the fallback.
- **Every test against the container:** 2,534 tests, none failed, 158 skipped. That's 76 more run than without Docker: the CUPS queues, `ippeveprinter` over IPP and IPPS, and the raster integration tests. The skips are the Windows test printers, the Mac and the end-to-end host.

---

## 2026-11-20: The stable release, ready to switch

**Done** (owner: "Start item 7")
- **[releasing.md](releasing.md), "The first stable release (1.0)":** the sign-off, with the command for each check; the switch; the release.
- **`tools/releasing/switch-to-stable.sh`** makes the wording changes when the sign-off is done:
  - the README's and the package README's status say stable;
  - `--prerelease` goes from the install commands in four files;
  - the changelog's notes start with what 1.0 means.

  It's a script rather than changes kept on a branch, because the README's status line names the current version, which each release changes. Nothing user-facing says stable yet.
- **`prepare-release.sh` checks the README's status matches the release,** before changing anything: no preview while it says stable, no stable release while it says preview. So switching the wording and setting `RELEASE_PREVIEW` must go together.
- **Manual mode takes `--fingerprint`** for a printer over IPPS. Without it, the sign-off's IPPS check on the HP couldn't have run: its self-signed certificate would have been refused. The pin is trusted for direct IPP, for a Windows queue sent over IPPS, and for saving the printer's attributes, which used their own connection.
- **Tried on a scratch clone** of the 11.0.7-preview post-release commit:
  - the switch, once, then refusing a second run;
  - `prepare-release.sh` with each pairing of status and version: the two mismatches stop before any file changes; the two matches prepare the release, the stable one with the README saying "stable. Version 11.0.99".
- **Tried against the scripted printer over TLS:** manual mode fails with the fingerprint, then passes `manual.printer-details` and `manual.test-page` with it pinned.

**Not done**
- **The sign-off itself:** the HP and the Mac.
- **latiprimere.com's own pages,** in the website's repository, may still say preview: the checklist says to change them there.

---

## 2026-11-20: Printers anyone can announce, and room to grow after 1.0

**Done** (owner: "Start items 5 and 6")
- **Item 5, a new section in the network printers guide: "Anyone on the network can announce a printer."**
  - What it covers: multicast DNS can't check who's speaking; what Latiprimere guards (UUID checks, IPPS from the same host, shared passwords only to pinned printers); and what it can't.
  - Found while writing it: a printer looked up by name falls back to plain IPP when its certificate isn't trusted. So pinning protects only a printer printed to by its `ipps://` address, and the guide says so.
  - Pointers added from the secure printers page and the Windows WSD matching.
- **Item 6, [ADR 0044](decisions/0044-room-to-grow-after-1-0.md), accepted (owner, "Accept ADR 0044"): each "Later" item can be added after 1.0 without breaking anything.**
  - UI add-ons: new packages.
  - Image and text content: new `DocumentFormat` values and `PrintContent` factories.
  - Events: an optional interface beside `IPrintSystem`, and default-implemented members.
  - Android and iOS: new adapter packages and target frameworks.
- **Before 1.0, as a result:**
  - `DocumentFormat`, `ColorMode`, `PageOrientation` and `VendorOptionKind` say they may gain values, and how to treat one you don't know;
  - CONTRIBUTING.md says which enums may grow and where new features go;
  - IPP printing throws `NotSupportedException` for an unknown document format, as `SubmitAsync` documents, not `ArgumentOutOfRangeException`;
  - plan.md's Later list gives each item's route.
- **Checks:** all 329 links between the site's pages resolve, anchors included; every Latiprimere name the new text uses exists; the API reference is regenerated; 2,528 tests, none failed, 234 skipped.

---

## 2026-11-20: Why a job is in its state

**Done** (owner: "Start item 4")
- **`job-state-reasons` is read** with every IPP job status ([ADR 0043](decisions/0043-job-state-reasons.md), accepted: owner, "Accept ADR 0043"), for CUPS queues, printers printed to directly and Windows queues' jobs sent over IPP.
  - **`PrintJobStatus.StateReasons`,** new: the keywords, in lower case, in order, without `none`; empty for the Windows spooler.
  - **`StateMessage`,** when the printer gives none, explains the most telling of about 30 known reasons: the cause first, then who cancelled, then why a job waits. This closes Spike 1's open item: a job CUPS cancels no longer ends with no explanation.
  - **`WatchStatusAsync`** reports a change of reasons, comparing the lists by content.
- **Tests:**
  - new unit tests: reading and explaining reasons (37 cases), the mapping (4), `WatchStatusAsync` and the default;
  - the harness's four IPP lifecycle scenarios check reasons and messages: all pass;
  - a CUPS integration test for a two-sided job to the one-sided Virtual-IPP queue, which skipped here.
- **Docs:** the job states section of the print options and jobs guide, the API reference, the changelog.
- **Results:** 2,528 tests, none failed, 234 skipped (Docker, the Windows test printers and other platforms); the harness's 35 IPP and Windows-route scenarios: 29 passed, 6 skipped as before.

**Not done**
- **The CUPS integration test hasn't run:** Docker wasn't running here, and CI will run it. Which reasons CUPS gives for the cancelled job is still to see.
- **CUPS's `job-printer-state-message`** and **the Windows spooler's status flags** aren't read (ADR 0043).

---

## 2026-11-20: IPPS for Windows queues, and what printers say they ask for

**Done** (owner: "Start items 2 and 3")
- **Windows queues over IPPS** ([ADR 0042](decisions/0042-ipps-for-windows-queues-and-uri-authentication.md), accepted: owner, "Accept ADR 0042"). Four new settings: `WindowsOptions.IppTrustedCertificateFingerprints`, `IppServerCertificateValidationCallback`, `IppCredentials` and `IppPrinterCredentials`.
  - **The same rules as direct IPP,** from code now shared between the two: passwords only over TLS, and with a shared password a WSD queue's printer must be pinned.
  - **IPPS first** for a WSD queue's printer that announces it, once trust is configured, with plain IPP if the encrypted connection fails.
  - **Checked at creation,** with messages naming `WindowsOptions`.
  - **No more silent fallbacks:** a refused certificate is logged as a warning with its fingerprint; other failures at Information. A TLS failure at Print-Job now sends the job through the driver.
- **`uri-authentication-supported` is read,** with `printer-uri-supported`, matched by scheme, port and path, since printers name themselves rather than the address used.
  - **The Windows route:** a printer asking for a password it won't be sent, or for `digest`, `certificate` or `negotiate`, has its jobs printed by the driver, without Validate-Job or Print-Job. Before, a printer that answered questions freely but wanted a password to print failed the job with `PrintAuthenticationException`.
  - **Direct IPP doesn't refuse early,** so a printer that misreports still prints. Its authentication refusals now name its `ipps://` address, or say the authentication it wants can't be given.
- **Scripted printer:** `FakePrinterOptions.RequiresCredentialsForJobsOnly`, as most real printers ask.
- **Tests:**
  - new unit tests for the reader (13 cases), the refusal hints, the route's security (12), and the factory's checks (6);
  - harness: `windows.ipp-route.password-over-ipps` passes here; `windows.ipp-route.fallback-password-for-jobs` needs the Windows test printers, so it was skipped;
  - the existing security, route and fault scenarios still pass.
- **Docs:** the Windows section of the CUPS and Windows page, with a new example in `CupsAndWindows.cs`; the secure printers page; ADR 0037's "Not done"; the API reference; the changelog.
- **Results:** 2,440 tests, none failed, 232 skipped (Docker, the Windows test printers and other platforms); the harness's 35 IPP and Windows-route scenarios: 29 passed, 6 skipped for want of the Windows test printers and a virtual IPP printer.

**Not done**
- **`windows.ipp-route.fallback-password-for-jobs`** hasn't run: it needs the Windows test printers (`tools/windows-test-printers`, elevated).
- **Not tried on a real printer:** the HP asks for no password, and its WSD queue isn't on this PC.

---

## 2026-11-20: A time limit on a printer that stops taking a job

**Done** (owner: "Yes, start item 1", from the review of what's left before 1.0)
- **`IppNetworkOptions.UploadStallTimeout`** ([ADR 0041](decisions/0041-upload-stall-timeout.md), accepted), 5 minutes by default (owner: "Keep 5 minutes"). It closes what the sweep of 2026-11-19 left open: a Print-Job had no time limit if the printer stalled.
  - A job fails when one write of its document, or the wait for the answer once it's all sent, takes longer.
  - The time between writes isn't counted, so rendering raster pages isn't a stall, and a printer that keeps reading, however slowly, is never cut short.
  - The `PrintException` says whether the printer stopped taking the document or was sent it all and didn't answer.
  - It isn't a connect failure, so a routed Windows queue doesn't print the job through the spooler as well. It isn't resent.
  - Windows queues routed over IPP get the default. CUPS is unchanged.
- **Found on the way: Windows reports a single large socket write as finished once it's queued.** SharpIppNext writes a seekable document in one write, and 64 MB was "sent" in 24 ms to a printer reading nothing. So writes go on in 64 KiB pieces, each timed. Without them, a stall would have been reported as a missing answer, and a slow printer reading a large PDF cut off after 5 minutes.
- **Scripted printer:** `FakePrinterBehaviour.StallPrintJobUpload` stops reading a Print-Job after its IPP header. Stopping the printer releases such a request, as Kestrel no longer sees the client hang up.
- **Tests:**
  - 30 new unit tests: the watch, the stream's pieces, the handler, the option and factory checks, and the stall not counting as a connect failure;
  - three over real TCP: a printer that never reads, with a seekable and a forward-only 64 MB document, and one that never answers;
  - the harness scenario `ipp.faults.stalled-upload`, through the public API: it fails after the 2 s limit set there, and the job is sent once.
- **Docs:** the network printers settings table, the troubleshooting guide, the API reference and the changelog.
- **Results:** 2,366 tests, none failed, 228 skipped (Docker, the Windows test printers and other platforms); the harness's 33 IPP and Windows-route scenarios: 28 passed, 5 skipped for want of the Windows test printers and a virtual IPP printer.

**Not done**
- **Not tried on a real printer:** making the HP stall on purpose isn't practical.
- **The changelog** has a second `## Unreleased` heading above the one the 11.0.7-preview post-release pull request turns into that release's, so the entry isn't counted as 11.0.7's. Merge that pull request first.

---

## 2026-11-19: P3-9 begun: the 1.0 API, the docs against it, and housekeeping

- **Timing flakes:** a macOS CI failure in the busy-retry test was a race with the real clock; the test now uses a clock that moves on by a fixed step. A sweep found one more, in the Happy Eyeballs tests, now released by the test itself. The others have wide margins.
- **API review for 1.0** ([api-review-1.0.md](api-review-1.0.md), [ADR 0040](decisions/0040-api-review-1-0.md)): all seven decisions as recommended (owner). .NET spellings in public names; factory overloads taking an `ILoggerFactory`; `PrintCertificateException`; `IPrinter.GetJobAsync`, with Windows jobs routed over IPP given `ipp:` IDs; `VendorOption.MinValue` and `MaxValue`; the rules for changing the API after 1.0 in CONTRIBUTING.md.
- **11.0.6-preview** was released the same day, before the decisions were applied, and shipped names the review counted as unshipped. The renames went ahead with compatibility suppressions; the two-argument `IppNetworkPrintSystemFactory.Create` stays. The working tree has the release commit's changes, and `main` compares with 11.0.6-preview, as the 1.0 release will.
- **Releases and the compatibility baseline:** the release workflow left `main` comparing its packages with the release before the one just made, so a change to something that release first shipped passed on `main` and failed at the next release. Now the post-release pull request has a second commit, made by `tools/releasing/post-release.sh`, that sets the baseline to the version just released and removes the compatibility suppressions, which package validation would fail on once unneeded. `anything-to-release.sh` doesn't count that commit's changes. Both scripts were tried in a scratch repository, with LF and CRLF files; the workflow itself runs at the next release.
- **Docs review against the 1.0 API:** 295 links between pages resolve, 140 to anchors; every Latiprimere name in the pages exists; six pages updated for the new API. The website builds, type-checks, lints and passes its unit and smoke tests against them.
- **Housekeeping:**
  - The mDNS trial passed in all 17 CI runs since the fixes of 2026-11-17, so it's now a normal CI job that can fail the build ([ADR 0029](decisions/0029-end-to-end-test-harness.md)).
  - [plan.md](plan.md): Spikes 0 and 2, the end-to-end harness, the direct IPP adapter and the documentation are ticked. What stays open waits on the Mac (Spikes 1 and 4, P3-8), the HP (raster, IPPS) or 1.0 itself.
  - The risks tables in plan.md and phase-3-plan.md are brought up to date. New: only one real printer tried, and the API frozen at 1.0. The `Latiprimere.` ID prefix isn't reserved yet: the packages aren't shown as verified on nuget.org.

- **Sweep for bugs, security and performance issues:**
  - Fixed: the Windows `GetJobAsync` said a job sent over IPP was gone when its printer couldn't be reached; it now throws `PrintException`. `WindowsPrinter`'s cached IPP printer and its UUID are one object, replaced and forgotten atomically. An accepted certificate clears an earlier refusal, so errors don't quote a stale fingerprint. HTTP redirects aren't followed.
  - Updated PDFium to 160, SharpIppNext to 5.0.3, xunit.v3 to 4.0.3 and StyleCop.Analyzers to 1.3.180; added `.github/dependabot.yml`.
  - Checked and sound: TLS and credential handling, XML parsing (no DTDs), the address store, bounded raster and GDI memory, vendor options, catalog isolation and disposal.
  - Left open: a Print-Job upload has no time limit if a printer stalls, unless the caller passes a token; and nothing in the docs says a device on the network can pose as a discovered printer.

## 2026-11-18: P3-6, remembered addresses that survive a restart

**Done** (owner: "Yes, start P3-6")
- **`IppNetworkOptions.AddressStorePath`** ([ADR 0038](decisions/0038-remembered-addresses.md)) keeps each discovered printer's name, address, IPPS address, UUID, make and location in a JSON file:
  - read when the print system is created;
  - written, through a temporary file, whenever a listing or lookup finds a new address, IPPS address or UUID;
  - a missing, damaged or unwritable file is logged, never an error, and nonsensical entries are skipped;
  - a remembered address is still used only while the printer there reports its UUID.
- **Native AOT:** the JSON goes through a source-generated `System.Text.Json` context. CI's AOT check now also runs `ipp.discovery.remembered-address` natively.
- **Tests:**
  - 9 new unit tests, including a restart in which a printer silent on mDNS is found without a search, and one in which another printer answers at the address and isn't used;
  - the harness scenario `ipp.discovery.remembered-address`, through the public API: it passes.

**Not done**
- **Trust on first use,** which could keep its pins in the same file, isn't built: it's opt-in and no one has asked for it.
- **Not checked on the HP asleep;** the restart is checked against the scripted printer.

---

## 2026-11-18: P3-5, IPPS and authentication for direct IPP

**Done** (owner: "Yes, start P3-5")
- **Certificates** ([ADR 0037](decisions/0037-ipps-and-authentication.md)): the system's trust, then fingerprints pinned in `IppNetworkOptions.TrustedCertificateFingerprints`, then `ServerCertificateValidationCallback`. A refused certificate's `PrintException` gives its SHA-256 fingerprint, taken from the TLS handshake, and says where to pin it.
- **Credentials:** `IppNetworkOptions.Credentials` for every printer, and `PrinterCredentials` for one, are sent as HTTP Basic only over TLS. A printer asking for a password over plain IPP is refused, saying to use its `ipps://` address.
- **IPPS preferred once trust is configured (D4):** discovery keeps a printer's IPPS address as well, and lookups by name try it first, falling back to plain IPP when its certificate is refused.
- **Tests:**
  - 26 new unit tests;
  - harness: `ipp.security.pinned-certificate`, `ipp.security.password-over-ipps` and `ipp.security.password-not-sent-in-clear` are new, and `ipp.security.untrusted-certificate` checks the fingerprint. All five security scenarios pass over real TLS;
  - an integration test for `ippeveprinter` over IPPS: refused with a fingerprint, pinned, printed. It passed here against the scripted printer serving TLS; CI runs it against the container, and it skips, saying why, if that `ippeveprinter` doesn't offer TLS.

**Not done**
- **The HP over `ipps://…:443`** with its certificate pinned (the plan's "done when"): it needs the printer on. It prints a test page only when you say.
- **Trust on first use** waits for P3-6's store. **`uri-authentication-supported`** isn't read. **The Windows route** still uses the system's trust only.

---

## 2026-11-18: CI fixes after the Phase 3 push

**Done** (owner: "The macos and ubuntu ci jobs failed")
- **The raster integration tests** sent straight through the client, or through a context without busy retries. CI runs each target framework's tests at once against one `ippeveprinter`, which takes one job at a time, so one got `server-error-busy`. Both tests now resend for up to two minutes, as the print system does. Checked against the scripted printer turning three jobs away as busy.
- **Every CUPS integration test on macOS** was refused at `127.0.0.1:631`. The runner's CUPS log showed why:
  - launchd starts cupsd on demand, and cupsd exits after a minute with nothing to do ("will restart on demand");
  - launchd then starts it again only for its socket, `/private/var/run/cupsd`, not for TCP;
  - the build took longer than that minute, so the tests found nothing listening.

  The tests now reach a Mac's CUPS through the socket: CI, `tools/test-harness/run.sh`, the macOS printers README and CONTRIBUTING. That's what Latiprimere does on a Mac by default, so applications aren't affected; only the test settings forced TCP.
- **The Native AOT check then failed on macOS:** `check-aot.sh` wakes cupsd before the native harness runs, and also waited for the server to answer on its port. It read `/private/var/run/cupsd` as a host and port and waited for `/private/var/run/cupsd:631`. A socket path now needs only the scheduler to answer through it; a `host:port` server is still checked on its port. Checked locally with stand-ins for `lpstat` and `nc`.

**Not done**
- **Not run on a Mac here;** the next CI run checks it.

---

## 2026-11-18: P3-4, Windows WSD queues over IPP

**Done** (owner: "Start P3-4")
- **WSD queues matched by UUID** ([ADR 0036](decisions/0036-wsd-queues-over-ipp.md)): with `PrintPdfOverIpp` on, a queue without an exactly known URI routes when:
  - its WSD port's `Printer UUID`, read from the registry, is announced over mDNS by a printer;
  - and that printer reports the same `printer-uuid` when asked.

  The route remembers each printer found and forgets it when it stops answering or reports another UUID. A UUID nobody announced isn't searched for again for five minutes. A failed search leaves the job with the driver.
- **Raster through the route:** the route gets the Windows rasteriser and `MaxRenderDpi`, so a printer that takes PWG raster or Apple raster but not PDF routes too. A raster job that can't be planned, such as a printer whose default paper has no size, prints through the driver instead.
- **Seen on this PC:** the HP's WSD queue is gone; the Canon TS7500i's WSD port records `Printer UUID = 00000000-0000-1000-8000-00117d58570e`, read without administrator rights. No IPP printer answered mDNS during the session.
- **Tests:**
  - 28 new unit tests: the route's matching, remembering and five-minute wait, routed PDF and Apple raster jobs, mismatched and missing UUIDs, an unreachable match, a raster job that can't be planned, port-name checks, and mDNS's UUID pick;
  - a read of every WSD port's UUID on the machine, checked against the registry;
  - a manual harness scenario, `manual.windows-queue-over-ipp`, for the HP;
  - harness: `windows.ipp-route.raster-to-printer` is new, and `windows.ipp-route.fallback-no-pdf`'s printer now takes neither PDF nor a raster format.
- **Fixed, from P3-1:** `windows.ipp-route.refusal-not-resent` still expected the refused job's Print-Job, which Validate-Job now prevents. It runs only with the Windows test printers switched on (`LATIPRIMERE_TEST_WINDOWS_OUTPUT`), which P3-1's run didn't have. It now checks for one Validate-Job and no Print-Job.
- **Results:**
  - 2,054 tests, 1 failed, 204 skipped. The failure is the connection-racing test `GivenLoserConnectsAfterWinnerWhenConnectThenLoserConnectionIsClosed`: it passed 3 runs out of 3 on its own, its code is unchanged, and its 250 ms margin between the two addresses can be lost under the full run's load;
  - every harness scenario this PC can run passes with the Windows test printers on: 46 passed, 19 skipped;
  - the Native AOT check publishes without warnings and passes 6 scenarios. Routing a WSD queue isn't among them: it needs a real WSD queue and printer.

**Not done**
- **The HP's queue printing over IPP** (the plan's "done when"): the HP's WSD queue is no longer on this PC, and the HP was off. With the printer on and its queue re-added, run the manual scenario. It prints 2 sheets, and only after you say yes:

  ```powershell
  dotnet run --project tests/Latiprimere.TestHarness -- manual --printer "windows:HP ENVY 5640 series" manual.windows-queue-over-ipp
  ```
- **Microsoft's IPP Class Driver queues:** unexamined, since none is available.
- **Standard TCP/IP ports** aren't matched: a host alone doesn't identify a printer.

---

## 2026-11-18: P3-3, printing PDFs as raster

**Done** (owner: "Yes, start P3-3")
- **PDFs to direct IPP printers that can't take PDF** go as PWG raster, or else Apple raster, when a PDF rasteriser is registered ([ADR 0035](decisions/0035-printing-pdfs-as-raster.md)):
  - settings come from the job's options and the printer's raster attributes, read when the job is submitted: resolution (the printer's default, limited by the new `IppNetworkOptions.MaxRenderDpi`), colour space, paper, sides and back-side turns;
  - `orientation-requested` isn't sent, as the pages are already turned;
  - pages render on a worker thread into a pipe that is Print-Job's document, uploaded chunked and gzipped when the printer offers it;
  - a busy printer gets the pages rendered again; a rendering failure is thrown as itself.
- **Public API:** `IppNetworkOptions.MaxRenderDpi`, and `IppNetworkPrintSystemFactory.Create(IPdfRasterizer?, IppNetworkOptions?)`. `AddLatiprimereIpp()` uses a rasteriser registered with `AddLatiprimerePdfium()`. Without one, the job is refused, naming Latiprimere.Rendering.Pdfium.
- **Page sizes as libcups makes them:** pixels and points are worked out from hundredths of a millimetre, rounded down, so A4 at 600 dpi is 4,960 × 7,015. The first harness run caught the encoder making it 4,961 wide, a pixel more than CUPS and Apple send; P3-2's encoder and tests are corrected.
- **Tests:**
  - planning, with the HP's real `urf-supported` and `ippeveprinter`'s attributes;
  - the printer with a fake client, decoding what arrives page by page;
  - four harness scenarios on the scripted printer, over real HTTP: `ipp.raster.pwg-pages`, `ipp.raster.apple-raster-two-sided`, `ipp.raster.busy-renders-again` and `ipp.raster.no-rasterizer-refused`;
  - an integration test through the real client that the real `ippeveprinter` must read. It passes here against the scripted printer, standing in, and runs against `ippeveprinter` in CI.
- **Docs:** [network-printers.md](site/print-systems/network-printers.md) has a section on printers that don't take PDF, and the example registers PDFium.
- **Native AOT:** CI's check now also runs `ipp.raster.apple-raster-two-sided` natively, on every runner.

**Not done**
- **The HP on paper** (the plan's "done when"): a test page, colour and grey, A4, two-sided. Not printed: it needs the printer on and your go-ahead (D9).
- **`ippeveprinter` reading the streamed raster** wasn't checked here, because Docker wasn't running. CI runs it.
- **Windows queues** in front of raster-only printers still print through their driver, until P3-4.

---

## 2026-11-18: P3-2, raster encoders

**Done** (owner: "Yes, start P3-2")
- **PWG raster and Apple raster encoders** ([ADR 0034](decisions/0034-raster-encoders.md)), internal in `Latiprimere.Ipp/Raster`:
  - `RasterDocumentEncoder` streams an `IRasterDocument` to either format in 1-bit black (PWG raster only, Bayer-dithered), 8-bit grey or sRGB, at any square resolution;
  - each page is fitted, centred and never enlarged, onto the paper, and turned a quarter for landscape;
  - two-sided jobs' back sides are flipped or rotated as the printer's `pwg-raster-document-sheet-back` or `DM1` to `DM4` asks;
  - bands are capped at 16 MiB and repeated lines are folded, so a blank A4 page at 600 dpi is under 4 KB.
- **Public API:** `RasterBand.Rotation` and the `RasterRotation` enum. PDFium renders the page turned, with no extra memory.
- **Tests:**
  - `RasterDocumentReader` in Latiprimere.Testing decodes both formats, written apart from the encoder;
  - line encoding with known bytes and 200 random lines;
  - a synthetic four-quadrant document for every turn, fit, back side, band split and colour space;
  - three real PDFs through PDFium in both formats, grey and colour, matching PDFium's own render pixel for pixel;
  - `ippeveprinter` integration tests print PWG raster from a three-page PDF in grey at 300 dpi and black at 600 dpi, and expect the job to complete.
- **Results:**
  - 1,910 tests, 0 failed, 192 skipped;
  - the Native AOT check publishes without warnings and passes 5 scenarios (2 skipped: no CUPS or `ippeveprinter` container here). Nothing public reaches the encoders until P3-3, so for now the trimming analysers at build vouch for them, not the published harness.

**Not done**
- **The `ippeveprinter` tests** didn't run here: Docker wasn't running. They run in CI.
- **Paper:** no real printer has printed the encoders' output yet; P3-3 prints Apple raster to the HP.

---

## 2026-11-18: Phase 3 planned; P3-1, Validate-Job

**Done** (owner: "Let's make a start on phase three. Put together a plan for the work first", then "Yes, start P3-1")
- **[phase-3-plan.md](phase-3-plan.md):**
  - work packages P3-1 to P3-9, with what already exists for each, an order, risks, and decisions D1 to D9, all accepted as recommended;
  - recorded on the way: the HP takes Apple raster (`urf-supported`: `RS300-600`, `W8`, `SRGB24`, `DM3`) and PCLm but not PDF, and `ippeveprinter` takes PWG raster (`black_1`, `sgray_8`);
  - `IRasterDocument.RenderBand` already gives what the raster encoders need.
- **Validate-Job** ([ADR 0033](decisions/0033-validate-job.md)):
  - `IppPrinter` sends Validate-Job, vendor attributes included, before Print-Job when the context asks for it and the printer lists the operation, which is now read at lookup (`operations-supported`);
  - an IPP client error refuses the job before the upload; a server error such as busy skips the check, logged at Debug;
  - `IppNetworkOptions.ValidateJobs` (default `true`); Windows queues routed to IPP always validate; CUPS never does;
  - `IIppClient.ValidateJobAsync` sends it raw, as Print-Job with vendor attributes is.
- **The harness's `ipp.submission.unsupported-value-refused`** now checks that the refusal comes from Validate-Job and that no Print-Job is sent.
- **Fixed in passing, in the harness:** two CUPS-only example scenarios ran without a CUPS server and failed. `DescribeMissing` reported the missing container only when a CUPS server was set; it now says there's no CUPS test server.
- **Results:**
  - 1,842 tests, 0 failed, 188 skipped;
  - every harness scenario this PC can run passes;
  - the Native AOT harness publishes without warnings and passes the Validate-Job, busy and Windows-route scenarios.

**Not done**
- **The real HP:** not checked, because it was off: neither its link-local address nor mDNS answered. Validate-Job alone prints nothing, so the check is safe whenever it's on.

---

## 2026-11-18: Second review's fixes

**Done** (owner: "Let's run another sweep through", then "Fix all seven")
- **The CLI** reports `ArgumentException`s (a vendor option the printer lacks, a ticket that isn't one) and files it can't read as messages, exiting with code 64, instead of crashing; `print-capabilities --ticket` with no file says so.
- **Remembered refusals:**
  - an IPP printer that refuses the vendor option request, with a client error or an unreadable answer, isn't asked again by the same `IPrinter`;
  - a Windows driver without Print Ticket support isn't tried again;
  - so each costs one round trip and one log entry, not one per capabilities read;
  - looking up a job's paper and tray no longer opens a Print Ticket session.
- **The queue report:**
  - lists queues with a plain `Get-Printer`, which doesn't ask print servers;
  - reads each queue's details in its own try, so one that fails is recorded with its error rather than stopping the report;
  - matches `-PrinterName` exactly, warning about names it doesn't find.
- **IPP text values** aren't offered as choices, since they'd go back as names.
- **`PrintTicketSession.Dispose`** runs once, however often it is called.
- **Tests:**
  - each of the three AddLatiprimere… methods passes DI's logger factory on, and logs nothing without one;
  - a refusal is remembered and a connection failure isn't;
  - a double Dispose leaves COM working, checked with Microsoft Print to PDF.
- **Docs:** the guide's example shows limits that aren't reported as "any", and the guide says what isn't listed and what's refused.
- **Results:** 1,830 tests, 0 failed, 188 skipped. The Native AOT harness publishes without warnings and passes the vendor option scenarios.

---

## 2026-11-18: Review fixes for the vendor option work

**Done** (owner: "Let's run a sweep for bugs, potential performance, security issues etc.", then "Fix all ten")
- **IPP:**
  - resolution values are inferred only for attributes ending in `-resolution`, so `cups:resolution` = `600dpi` is sent as a name, as before;
  - the vendor option request runs after the typed one, not alongside it, since some printers take one connection at a time; if it fails, the capabilities list no vendor options and the failure is logged, instead of the whole capabilities read failing.
- **Windows:**
  - a missing `prntvpt.dll` is a `PrintException` at the interop boundary, so capabilities still work without it;
  - parameters the driver expects encrypted, such as Print to PDF's PDF passwords, aren't listed;
  - options the device or an administrator rules out are refused, as they aren't listed;
  - feature keys are checked against the Print Capabilities for the job's saved `windows:devmode`;
  - copied features carry namespace declarations made below the document's root;
  - the default DEVMODE is read once per capabilities read.
- **Logging:** `Latiprimere.Ipp`, `Latiprimere.Cups` and `Latiprimere.Windows` now reference `Microsoft.Extensions.Logging.Abstractions`. Their contexts carry a logger from DI's `ILoggerFactory`; the public factories log nothing. What used to be swallowed is now logged.
- **Conventions:** messages come from resources, literals are named constants, the XML reader settings and the keyword naming are each written once (`PrintSchemaText.CreateReaderSettings`, `IppKeywords.Describe`), and the tests share one `RecordingLogger` in Latiprimere.Testing.
- **CONTRIBUTING.md:** the dependency rule now names `Latiprimere.Ipp` as the one adapter the others may reference (ADRs 0006 and 0027), and the AOT note points to the scenario list in `check-aot.sh`.
- **Tests:** 1,808, 0 failed, 188 skipped. The Native AOT publishes for both targets have no trimming or AOT warnings, and the vendor option scenarios pass natively.

**Learned**
- **`cups:Resolution`, as first reported, was never accepted:** vendor option names must be lowercase IPP keywords, so CUPS's capitalised PPD option names can't be set through `cups:`. That's an existing limit of ADR 0026, worth knowing for drivers whose options are all PPD-named.

**Not done**
- A Windows capabilities read still opens a Print Ticket session, which for a queue shared from a print server may go to the server. It isn't cached, since the driver's settings can change between reads.

---

## 2026-11-18: Vendor options in the capabilities, Windows Print Capabilities, and the queue report

**Done** (owner: "Build both, but also include 2B and 2C rather than waiting for someone to ask for it. Let's go as fully featured as we can.")
- **The queue report** ([tools/windows-queue-report](../tools/windows-queue-report/README.md)): a read-only PowerShell script that records each Windows queue as JSON, in about 15 seconds:
  - `Get-Printer`, `Get-PrinterPort` and `Get-PrinterDriver`, every property;
  - the spooler and port monitor registry keys;
  - the queue's Plug and Play device and the other devices of the same printer.

  The first version looked each device's container up one at a time and took minutes; one batched call fixed it.
- **What it found** (ADR 0027, "Follow-up"):
  - Windows had set up a queue for the HP itself, `HPA495A2 (HP ENVY 5540 series)`, on a WSD port with HP's inbox class driver, not Microsoft's IPP Class Driver;
  - the WSD port's registry key records `Printer UUID = 1c852a4d-…`;
  - asked over IPP, the printer's `printer-uuid` is `urn:uuid:` and the same value.

  So WSD queues can be matched exactly to IPP printers found over mDNS. That's left for Phase 3, since this printer doesn't take PDF over IPP.
- **Vendor options in `PrinterCapabilities`** ([ADR 0032](decisions/0032-vendor-options-in-capabilities.md)): `VendorOption` and `VendorOptionChoice` records and a `VendorOptionKind` enum in Latiprimere.Abstractions. Every key and value listed can be set in `PrintOptions.VendorOptions` as it is.
  - **IPP:**
    - a raw Get-Printer-Attributes request for the `job-template` group, alongside the typed one, through the new `IIppClient.GetPrinterAttributeListAsync`;
    - attributes from `job-creation-attributes-supported`, minus what the typed options send;
    - collections skipped by their nesting, since the flattened encoding names a member's values after the member (`media-type` turned up from `media-col-database`);
    - resolutions written `600dpi`, which the vendor option encoder now sends as `resolution`.
  - **Windows:**
    - the driver's Print Capabilities and default ticket read in one Print Ticket session, with features as `windows:feature:` keys and parameters as `windows:parameter:` keys, under the document's own prefixes;
    - options without names identified by their scored properties (`psk:PagesPerSheet=4`);
    - at print time the keys are checked against the Print Capabilities and copied into a second delta ticket, merged after `windows:print-ticket`.
- **`IWindowsPrinter`** (public, in Latiprimere.Windows): `QueueName`, `GetPrintCapabilitiesAsync(printTicket)` and `GetDefaultPrintTicketAsync()`.
  - The Print Ticket code moved into a `PrintTicketSession` class: one provider and one COM initialisation, used on one thread.
  - `SafeComStreamHandle` gained `Read`, through the vtable as before.
- **Tools:**
  - the CLI's `caps` lists vendor options; `print-capabilities` writes the driver's documents; `print --ticket FILE` sends a ticket file;
  - the harness gained `ipp.vendor-options.listed-and-sent` and `windows.vendor-options.listed-and-sent`, both in CI's Native AOT check;
  - manual mode saves the vendor options and, on Windows, the driver's documents;
  - the docs' capabilities guide gained vendor option and Print Capabilities sections, with examples that run in the examples sample.
- **Tests:**
  - the solution: 1,790 tests, 0 failed, 188 skipped (no spooler test printers or CUPS container on this PC);
  - IPP vendor options against four captured responses (HP ENVY 5640, `ippeveprinter`, two CUPS 2.4 queues), each listed value sent back with the tag it was listed with;
  - Windows Print Capabilities against a document modelled on the HP and XPS drivers;
  - the five related harness scenarios, normally and as Native AOT builds for both targets, with no trimming or AOT warnings.

**Learned**
- **Microsoft Print to PDF's vendor namespace includes its driver version** (`…/Microsoft_Print To PDF/10.0.26100.9974/`), while the Print Ticket API's prefix for it is `ns0000`. Keys use prefixes, so a driver update doesn't break saved settings.
- **Through the HP's own driver,** pages per sheet 4, Best quality and HP Premium Plus Photo paper were merged and read back from the DEVMODE; nothing was printed.
- **XPS Document Writer keeps an image compression choice in its DEVMODE,** but its output files held the same images either way: page images drawn through GDI aren't recompressed. It's a poor printer for checking that a feature takes effect.
- **`print-quality` on IPP printers** came out as Draft, Normal and High for every capture. CUPS queues add `job-priority` (1 to 100), `job-sheets`, `number-up` and `job-hold-until`.

**Not done**
- Nothing committed or pushed.
- No vendor option has been printed on paper.
- Nested Print Schema features and IPP collections aren't listed; `windows:print-ticket` still reaches nested features.
- A queue on Microsoft's IPP Class Driver hasn't been examined.

---

## 2026-11-18: Print Tickets on Windows, and release numbering

**Done** (owner: "Let's continue with phase 2", "the release process is incrementing the version number twice rather than once … Can you also investigate that", and "Consider those ADRs accepted")
- **Why 11.0.4 was skipped:**
  - the build number was the Release workflow's run number;
  - pull request #6 merged `main` into `release` when `main` had nothing new but 11.0.3's post-release commit, so the merged commit had exactly `v11.0.3-preview`'s files;
  - that push started run 4, which pushed `post-release/v11.0.4-preview` and was then cancelled;
  - the next real release was run 5, so 11.0.5.
- **Fixed** ([ADR 0030](decisions/0030-release-numbering.md)):
  - [next-version.sh](../tools/releasing/next-version.sh) numbers a release one after the highest build number on nuget.org or in the tags, so failed and cancelled runs leave no gap; it prints `11.0.6-preview` now;
  - a new first job, `check`, stops a release whose files are exactly the latest release tag's ([anything-to-release.sh](../tools/releasing/anything-to-release.sh));
  - [published-versions.sh](../tools/releasing/published-versions.sh) reads nuget.org once for all three scripts.
- **A second gap, in the changelog:**
  - 11.0.5's notes had been added under the released `## 11.0.3-preview` heading, so 11.0.5 had no entry;
  - the next release would have refused to start: prepare-release.sh looked for a dated heading for the previous version, while a release without notes is allowed;
  - now prepare-release.sh checks that the previous release's tag is in the merged commit's history;
  - it refuses a changelog that changed since that tag without a `## Unreleased` heading;
  - the three 11.0.5 notes have their own entry.

  A dry run in a scratch worktree on main's tip prepared 11.0.6-preview. On the commit before 11.0.5's post-release merge, it stopped as it should.
- **ADRs accepted:** 0011, 0019, 0020, 0022, 0024, 0025, 0026 and 0028 (0028's build number now superseded by 0030).
- **Phase 2's last items:**
  - the capability model and Windows PDF printing via PDFium were already done: reading in Spike 5, choosing on 2026-11-16, real paper on 2026-11-17; ticked;
  - `windows:print-ticket` is built ([ADR 0031](decisions/0031-windows-print-tickets.md)). The value must be XML with a `psf:PrintTicket` root, and is re-encoded as UTF-8. PDF jobs merge it onto the driver's defaults, or onto `windows:devmode`, through `prntvpt.dll`, and the typed options are applied after it.
- **Tests:**
  - 12 new unit test cases;
  - 3 integration tests with Microsoft Print to PDF: a merged A5 ticket gives a DEVMODE with paper 11, the driver refuses a malformed ticket with `ArgumentException`, and a PDF printed with the ticket comes out A5;
  - the end-to-end scenario `windows.output.print-ticket`, which the Native AOT check now runs as well.
  - The whole solution: 1,686 tests, 0 failed, 182 skipped (no spooler test printers or CUPS container on this PC).
  - The CLI sample and the harness published with Native AOT without trimming or AOT warnings. The native harness passed `windows.output.print-ticket` and `windows.output.paper-and-orientation`.

**Learned**
- **COM without COM interop:** the Print Ticket API wants `IStream`s. Streams from `CreateStreamOnHGlobal`, with `Write`, `Seek` and `Release` called through unmanaged function pointers on the vtable, need no generated COM wrappers, and work under Native AOT.
- **A delta ticket can be tiny:** naming `psk:ISOA5` alone was enough. Microsoft Print to PDF's driver filled in the size's properties.

**Not done**
- Nothing pushed or committed; the leftover remote branch `post-release/v11.0.4-preview` is still there.
- The Print Ticket hasn't been tried with a vendor driver's own features, or on real paper.

---

## 2026-11-17: run.sh on Linux (WSL Ubuntu 20.04)

**Done** (owner: "run.sh on a Linux machine next", then "Install .NET in WSL")
- **Set-up:** the .NET 11.0.100 SDK, with the .NET 10 runtime and ASP.NET Core, installed in WSL's `~/.dotnet` with Microsoft's `dotnet-install.sh`, without `sudo`. The working tree, uncommitted changes included, was copied to `~/latiprimere-run`, leaving out `bin`, `obj` and `out`. Docker is Docker Desktop's, through WSL integration.
- **Results:**
  - normal mode, after the fix below: 37 passed, 19 skipped (Windows printers, manual, mDNS), 0 failed;
  - `--packages`: 37 passed, 19 skipped, 0 failed, with the packed version under test;
  - `--xunit` on net10.0 and net11.0: 74 passed, 38 skipped, 0 failed.
- **The container check** made the container afresh for the new output path and it came up healthy.
- **Fixed `ipp.faults.dual-stack`:** it assumed `localhost` resolves to both `::1` and `127.0.0.1`, as on Windows. Ubuntu's `/etc/hosts` gives it `127.0.0.1` only and names `::1` `ip6-localhost`, so the IPv6-only printer was rightly refused. The scenario now looks up `localhost` first. On each family it resolves to, the printer must be found within 1.5 s; on a family it doesn't, the lookup must fail with a `PrintException` within 1.5 s rather than wait. In WSL that took 5 ms; Windows still finds both. The limit in its check names no longer follows the machine's locale ("1,5 s").

**Learned**
- **WSLg's runtime folder:** .NET stopped at its first command because `XDG_RUNTIME_DIR` (`/mnt/wslg/runtime-dir`) lacked the owner and permissions its named mutexes expect. A private folder fixed it; the harness README says how. This comes from the WSL environment, not from the scripts.

**Not done**
- **`--aot`** needs `clang` and `zlib1g-dev`, which need `sudo apt`; not installed.
- **`--host-network`:** not tried. In WSL, the "host" network is Docker Desktop's virtual machine, not a real Linux host, so the CI `mdns` job remains its real test.
- **macOS:** `run.sh` hasn't run on a Mac.
- **Clean-up:** `~/.dotnet`, `~/dotnet-install` and `~/latiprimere-run` remain in WSL, about 2 GB with the build output, plus NuGet's package cache in `~/.nuget`; the test container now mounts the WSL copy's output folder, and `run.ps1` recreates it for Windows on its next run.

---

## 2026-11-17: The run scripts make a stopped test container afresh

**Done** (owner: "Update run.sh to recreate the container after a restart")
- **`tools/test-harness/run.sh`** reads the container's state before `docker compose up`. A container that exists but is stopped, or running but unhealthy, is made afresh from the image (`--force-recreate`). That is how it is left after the host restarts, since it has no restart policy, and starting it again would reuse what the last run left in it. A healthy or starting container is left running, so its job numbers aren't reset every run. `entrypoint.sh` already copes with a restart since the same morning; this gives a clean container either way.
- **Checked here** with the same lines in Git Bash: after `docker stop`, the state read "exited unhealthy", and the container was recreated and came back healthy. Run again while healthy, it was left running. The script's syntax is checked; the whole script still hasn't run on Linux or a Mac.

- **`run.ps1` does the same** (owner: "Yes, add it"). It first checks the container exists with `docker ps --all`, since `docker inspect` writes an error for a missing one, which Windows PowerShell 5.1 would turn into a stop under the script's `$ErrorActionPreference = 'Stop'`. Checked through the script itself, with one scenario each time: stopped, it was recreated and came back healthy; healthy, it was left running; with no container, it was created as before. All three runs passed.

---

## 2026-11-17: CI's Native AOT check runs the native harness

**Done** (owner: "Replace check-aot.sh's run section with AOT mode", choosing "Same checks as today" for CI)
- **`tools/aot-check/check-aot.sh`** still publishes the CLI sample with Native AOT and fails on any trimming or AOT warning. It now publishes the end-to-end harness the same way, and its run section runs four of the native harness's scenarios instead of the CLI's commands:
  - `catalog.list-across-print-systems` (was `list`);
  - `cups.submission.vendor-options` (was a PDF to Virtual-IPP with `--color mono` and vendor options), new;
  - `ipp.submission.virtual-printer-options` (was `caps`, a PDF to `ippeveprinter` with paper, tray, colour and vendor options, and `print-quality=99` refused), new;
  - `windows.output.paper-and-orientation` (was a PDF on A5 to Microsoft Print to PDF, its MediaBox checked).

  A scenario whose printers the runner doesn't have is skipped; one that runs must pass. ADR 0029 records the exception to decision 4; `build.yml`'s comment, CONTRIBUTING.md, plan.md and the harness plan updated.
- **Results here, as the Windows runner with the container's variables too:** net10.0 and net11.0 each 4 passed, no warnings, about 97 s for both publishes and the run; with no test printers configured, 2 passed and 2 skipped.

**Not done**
- Not yet run in CI; the Linux and macOS runners will publish the harness with Native AOT for the first time there, ASP.NET Core's Kestrel included.

---

## 2026-11-17: End-to-end test harness, H5: packaged, AOT and mDNS

**Done** (owner: "Start H5")
- **Packaged mode** (`run.ps1 -Packages`, `run.sh --packages`):
  - packs every `src` project into `out/harness-packages/<time>` with a version of its own (`<prefix>-preview.e2e.<time>`), so NuGet's cache can't hand back an older build;
  - writes a `nuget.config` there that takes `Latiprimere*` from the folder and everything else from nuget.org (the repository's maps everything to nuget.org);
  - `tests/Directory.Build.props` and `Directory.Packages.props` switch `Latiprimere.EndToEnd` and `Latiprimere.Testing` to package references when `LatiprimereFromPackages` is set, building into `out/packaged/{bin,obj}/<project>`. The first attempt built into each project's `bin/packaged` and `obj/packaged`, and the two builds then compiled each other's generated files.
- **AOT mode** (`run.ps1 -Aot`, `run.sh --aot`): builds the samples, publishes the harness with Native AOT, fails on any IL2xxx or IL3xxx warning, and runs the native program. It combines with packaged mode. To work there, the harness now finds the samples by the .NET it runs on, not by its own folder, and reports "Latiprimere under test" (its informational version) instead of an assembly path, which Native AOT doesn't have.
- **mDNS discovery:** scenario `ipp.discovery.mdns` (`TargetNeeds.MdnsPrinter`, `LATIPRIMERE_TEST_MDNS_NAME`). It finds the named printer as `ipp:dnssd:<name>`, looks it up and reads its capabilities, and prints nothing.
  - `compose.host-network.yaml` puts the test container on the host's network (Linux), so its Avahi announcement reaches the host.
  - A new `mdns` job in `build.yml` runs the scenario against the container's `ippeveprinter` on ubuntu-latest, keeps the report, and has `continue-on-error` while on trial (ADR 0029, decision 6).
  - `run.sh --host-network` does the same locally on Linux.
- **Results on the Windows dev machine,** with the container and the Windows test printers:
  - packaged: 47 passed, 6 skipped (manual); the report shows the packed version, the harness's dependencies point at the packages, and PDFium's native files came from its package;
  - Native AOT: 47 passed, 6 skipped, no trimming or AOT warnings;
  - packaged and Native AOT together: 47 passed, 7 skipped (manual, and mDNS with no name set);
  - `ipp.discovery.mdns` against the HP ENVY on this network: found as `ipp:dnssd:HP ENVY 5640 series [...]`, looked up, 26 paper sizes read;
  - the normal xUnit host afterwards: 54 scenarios on both frameworks, 94 passed, 14 skipped, 0 failed; Release build clean.

**Not done**
- **The `mdns` job hasn't run:** it needs a push. Docker Desktop on Windows doesn't pass multicast through, so it can't be tried here. If it proves flaky, drop it.
- `run.sh` is checked for syntax only; none of its modes has run on Linux or a Mac.
- `tools/aot-check/check-aot.sh` still ran the CLI sample natively; replaced the same day (next entry).

---

## 2026-11-17: First real-printer run: HP ENVY 5640 through its Windows queue

**Done** (owner, with the harness's manual mode: `manual --printer "windows:<the HP's queue>"`, 10:01, 6 minutes)
- **Every check passed:** 6 of 6 scenarios, 16 checks, the owner's answers among them. Ten sheets printed through the HP's own Windows driver (WSD port), PDFs rendered by PDFium.
  - **Details:** the name and model are right; the capabilities match the printer: 24 paper sizes (A4 by default), one-sided and both two-sided modes, monochrome and colour, three trays (Automatically Select, Main Tray, Photo Tray), up to 9999 copies.
  - **Test page:** one page, all of it there, nothing cut off, the picture in colour.
  - **A4 and monochrome:** on A4, grey only. This is the first check on real paper of the grey rendering added the same day.
  - **Two copies of three pages, long-edge two-sided:** both copies in page order, each two-sided, turning like a book. Took 2 minutes 18 seconds.
  - **Landscape:** printed across the paper.
  - **Tray:** the paper came from the Main Tray, as chosen.
- An earlier run at 09:45 checked the printer's details only, and passed.

**Learned**
- **No attributes captured:** a Windows queue on a WSD port has no IPP address Latiprimere knows, so `manual.printer-details` noted that and moved on. The HP's attributes are already in the test data, captured over IPP on 2026-11-12.
- **What Windows reports:** two jobs ended "Completed (The job has left the print queue. Windows does not record whether it printed successfully.)", as the Windows adapter reports any job it can't follow to the end (ADR 0007); the person's answers are what showed they printed.
- **Still unchecked on real paper:** a CUPS queue (Linux or macOS) and a printer reached directly over IPP. The README's status line and `RELEASE_PREVIEW` wait on "manual testing with real printers" (ADR 0028); whether this run is enough is the owner's call.

**Next**
- H5: packaged mode, AOT mode, and mDNS discovery on the Ubuntu runner.

---

## 2026-11-17: End-to-end test harness, H4: manual mode for real printers

**Done** (owner: "Start H4")
- **`manual --printer <id>`** in the console harness: a person confirms the printer, and then each test job, before anything prints. They answer `y`, `n` (with what they saw) or `s` about what came out, and the answers are checks in the usual report (`out/harness/...-manual/`). Anything but `y` at "Print it on paper now?" skips that job; when piped answers run out, the empty answer is a no.
- **Six scenarios** (`manual.*`, area Manual):
  - the printer's details and capabilities, with its IPP attributes captured; prints nothing;
  - a test page;
  - A4 or Letter in monochrome;
  - two copies of three pages, two-sided where the printer can;
  - landscape;
  - a tray the person chooses.

  Each checks only options the printer reports, and is skipped otherwise.
- **Safety:** the scenarios need `TargetNeeds.Person`, which only manual mode provides (`IPerson`, `ConsolePerson`), so `run`, the xUnit host and the scripts skip them. They print only to the named printer, through `ManualPrinting.PrintAsync`, which shows the document, pages, copies, sheets and options first. Microsoft Print to PDF jobs are saved in the report folder rather than opening a save dialog.
- **`capture <printer>`** and `IppCapture`: a printer's Get-Printer-Attributes response saved as `.ipp` and `.txt`, as the test data's captures are, with the values to replace before sharing them (UUIDs, device ID, names, addresses).
- **Docs:** [docs/manual-tests/README.md](manual-tests/README.md) (how to run it, what each scenario prints and asks); the harness README; CONTRIBUTING.md's manual-mode exception no longer "planned".
- **Tried on virtual printers only,** with answers piped in. The fake printer (ippeveprinter profile, trays and landscape): every scenario ran; a deliberate "no" was recorded as a failed check with its comment. CUPS-PDF through the container: attributes captured through CUPS; a declined job skipped; CUPS-PDF's single copy reported by a "no". `run 'manual.*'` skips all six; the xUnit host, all 53 scenarios on both frameworks: 94 passed, 12 skipped (the manual ones), 0 failed.

**Not done**
- **The first real-printer run,** H4's "done when": done the same day, in the entry above.

---

## 2026-11-17: Monochrome PDF jobs on Windows rendered grey

**Done** (owner: "Yes, render grey", after H3 found Microsoft Print to PDF ignores `dmColor`)
- **`GreyscaleConverter`** turns each rendered band grey, by ITU-R BT.601 weights (77, 150 and 29 out of 256, rounded), leaving the unused byte alone. `GdiRasterPrinter` calls it for a monochrome job before drawing each band; `dmColor` is still set. Recorded in [ADR 0025](decisions/0025-choose-paper-colour-and-tray.md); CHANGELOG updated.
- **Tests:** white, black, grey, red, green and blue pixels; no colour left in a band; only the band's part of the shared buffer changed; a partial pixel left as it is. The harness's `windows.output.colour-mode` now checks a monochrome job has no colour, instead of noting it.
- **Results:** the Windows tests 538 passed, 10 skipped, 0 failed; Microsoft Print to PDF's monochrome page now 0.0% colour with its content intact (ink 15.6% against 15.8% in colour); the Windows harness scenarios 5 of 5.

- **The capabilities guide brought up to date** (owner: "Yes, fix the guide"): `docs/site/guides/printer-capabilities.md` said `PrintOptions` couldn't choose paper, tray or colour, which was true in 0.1.0-preview but not since ADR 0025. Its "Paper size, trays and colour" section now shows choosing A4 and monochrome from the capabilities (the code added to `samples/Latiprimere.Examples/ReadingCapabilities.cs` as `ChoosePaperAndColourAsync`), how trays are chosen, and what CUPS and network printers, Windows and raw jobs each do with the options. Release build clean; the harness's samples scenarios 9 of 9.

---

## 2026-11-17: End-to-end test harness, H3: inspecting printed PDFs

**Done** (owner: "Start H3")
- **`PdfInspector`** renders each page of a printed PDF at 36 DPI through `Latiprimere.Rendering.Pdfium` and measures its ink (the share that isn't white) and colour (the share that isn't grey), with its size and orientation (`PdfPage`, `PdfSummary`). **`PdfPrinters`** prints a test document to CUPS-PDF or Microsoft Print to PDF and reads back what was written, keeping it as evidence.
- **Five scenarios:**
  - CUPS-PDF on its default A4 and on Letter;
  - CUPS-PDF in monochrome (no colour) and colour;
  - Microsoft Print to PDF on A4, on A5, and landscape;
  - two copies as two pages;
  - colour kept, with monochrome noted.

  Each printed page's content must be intact: ink within 30% of the page sent, colour kept. The two existing PDF scenarios now also check every page has content.
- **The CUPS test container survives a restart.** After the PC restarted, Docker restarted the container, and its start-up script failed: the previous run's CUPS socket file was still there, so the wait for CUPS passed before `cupsd` was listening, and `lpadmin` couldn't connect. `entrypoint.sh` now removes the stale socket and PID files, and waits for `lpstat -r` to say the scheduler is running. Checked by `docker restart`.
- **Results on the Windows dev machine:** the xUnit host, all 47 scenarios on both frameworks at once: 94 passed, 0 skipped, 0 failed. Release build clean.

**Learned** (with `mixed-1-page.pdf`: A4, 16% ink, 8% colour)
- **CUPS-PDF** honours paper size and monochrome (0.0% colour). It keeps a PDF's own orientation, as CUPS's PDF filter does, and writes one copy, so landscape and copies don't show in its output. It lists A5 as `custom_148.52x209.9mm`, because its PPD gives A5 as 421 x 595 points; that is CUPS's naming, passed through.
- **Microsoft Print to PDF** honours paper size, landscape (the portrait page scaled to fit across) and copies. **It ignores monochrome:** the page stays 8.2% colour. On Windows, Latiprimere only sets `dmColor` for monochrome (ADR 0025), and leaves it to the driver.
- The guides didn't say that CUPS keeps a PDF's own orientation. Added the same day (owner: "Add the CUPS orientation note to the guides"): a note on `Orientation` in the print options guide, and "PDFs keep their own orientation" in the CUPS section of the CUPS and Windows guide, saying what was seen with CUPS 2.4 and what to do instead.

**Next**
- H4: manual mode.

---

## 2026-11-16: PDFs refused to text-only drivers on Windows

**Done** (owner: "Yes, add the check", after the harness found a PDF stops a Generic / Text Only queue)
- **`DriverQuirks.PrintsTextOnly`,** starting with "Generic / Text Only"; `WindowsPrinter` refuses a PDF to such a driver with `NotSupportedException` before rendering or queueing anything, naming the driver and suggesting raw data. Only the spooler's path refuses: a PDF routed to the queue's IPP printer is sent as before. Recorded in [ADR 0019](decisions/0019-windows-error-mapping.md); CHANGELOG and the labels-and-receipts and errors guides updated.
- **Tests:** the quirk list; a PDF refused without opening the document or calling GDI; raw data to the same driver still sent; a text-only queue whose IPP printer takes PDF still routed there, and one whose printer doesn't take PDF refused.
- **Harness:** new scenario `windows.pdf.text-only-refused` (a PDF to "Latiprimere Raw" is refused and its port file doesn't change). The fallback scenarios now see the spooler's path by this refusal, since the paused test printer uses the same driver.
- **Results:** the Windows tests 506 passed, 10 skipped, 0 failed (on two runs out of three; see below); the harness's Windows scenarios 8 of 8.
- **The `GetDefaultPrinterName` race, fixed** (owner: "Yes, fix it"): `WinSpoolApi.ReadDefaultPrinterName` asks again with the new length while the name has grown, up to three times as the other spooler calls do, and returns no default if the default was removed in between. Seven unit tests play the race with a fake `GetDefaultPrinter`: a name that grows, shrinks, disappears or keeps growing, no default, and a spooler failure. CHANGELOG has it under Fixed. Afterwards: the Windows tests 520 passed, 10 skipped, 0 failed; the harness, all 42 scenarios on both frameworks, 84 of 84.

**Learned**
- **A race in `WinSpoolApi.GetDefaultPrinterName`.** One run of the Windows tests failed an existing integration test with "GetDefaultPrinter failed: The data area passed to a system call is too small". It asked for the default printer's name length, then read the name; this PC has "Let Windows manage my default printer" on, so Windows makes the last printer used the default, and with both test processes printing, the default changed to a longer name between the two calls. Fixed the same day, above.
- **Test runs change the default printer** on a PC where Windows manages it: after the harness, the default was "Latiprimere Paused".

**Next**
- H3, as before.

---

## 2026-11-16: End-to-end test harness, H2: packages together

**Done** (owner: "Start H2")
- **Catalog, four scenarios:**
  - one print system down: CUPS unreachable, the IPP and Windows printers still listed and found, a warning logged, a CUPS ID reported as unreachable rather than missing, and the default printer still found;
  - every print system down: CUPS's own error, naming the server;
  - none registered: `PrintSystemUnavailableException` from listing, lookup and the default printer;
  - unknown IDs: an unknown prefix, an IPP URI that isn't configured (never contacted, ADR 0024) and a missing Windows queue are each `PrinterNotFoundException` with the ID.
- **Windows IPP route, four scenarios** with a test printer mapped to the scripted printer: the spooler takes the job when the printer doesn't take PDF, can't be reached (after printing one job, so the rewind is used) or wants a password; a refusal is thrown and not printed through the spooler as well. The fallbacks use "Latiprimere Paused": the job waits in its queue as a spooler job and is cancelled; the refusal uses "Latiprimere Raw", whose file must not change.
- **The documentation's examples,** run as the program a reader would run: listing with and without DI, capabilities, first print job, print options, labels and receipts (the ZPL and ESC/POS found byte for byte in the Raw queue's output), error handling, Microsoft Print to PDF, and the web service. The web service scenario starts the ASP.NET Core example, lists printers over HTTP, has a non-PDF refused (415), sends a job for a missing printer that its hosted worker logs and survives, then one it prints to Completed.
- **The CLI sample** against the scripted printer: list, caps, a print whose paper, tray, colour and vendor option reach the printer, and exit code 1 for a refused value.
- **Safety:** `first-print-job` prints to the default printer, so the scenario first works out which printer that would be and skips unless it is a CUPS test queue. The samples get `CUPS_SERVER` set to the test server or removed.
- **Harness:** `ScenarioContext.Skip`; `RecordingLogger<T>`; `Support/Samples` and `RunningSample` run samples built alongside the harness (`Latiprimere.EndToEnd` builds them without referencing them); `OutputSearch` finds a sample's job in a printer's output by its content.
- **Results on the Windows dev machine,** with the container, before the Windows test printers were installed: the H2 scenarios 15 passed, 4 skipped; all 41 on both frameworks, 72 passed, 10 skipped, 0 failed. Release build clean.

**Learned**
- Here the Windows default printer is Microsoft Print to PDF, but `first-print-job` printed to CUPS-PDF: the example registers CUPS first, and the catalog takes the first print system's default. Had CUPS not been configured, the guard would have skipped it rather than open a save dialog.
- An unreachable CUPS server costs about 2 s per attempt on Windows, which retries a refused connection; listing with one down took about 6 s.

- **With the Windows test printers installed** (owner, 2026-11-16): every scenario ran. The xUnit host, both frameworks at once: 82 passed, 0 skipped, 0 failed; the test printers' queues empty afterwards.
- **A PDF to the Generic / Text Only driver stops its queue.** The first version of the fallbacks printed to "Latiprimere Raw". The routing was right, but the spooler job went Processing, then Error, and held the queue: that driver can't print the page images a PDF becomes. Each three-page job also made a 380 to 400 MB spool file, rendered at the driver's full resolution (ADR 0009's 139 MB per A4 page at 600 DPI). The scenarios now use the paused printer and `MaxRenderDpi = 72`; the three stuck jobs were removed.

**Not done**
- `run.sh` still hasn't run on Linux or a Mac.

**Next**
- H3: inspect printed output (page sizes, orientation, mono) from CUPS-PDF and Microsoft Print to PDF.
- Decide whether Latiprimere should refuse PDFs to the Generic / Text Only driver up front, as it refuses raw data to Microsoft Print to PDF (ADR 0019). Applications that print receipts through it would otherwise stop their queue with a PDF.

---

## 2026-11-16: End-to-end test harness, H1: the scripted IPP printer

**Done** (owner: "Start H1")
- **`tests/Latiprimere.FakeIppPrinter`:** `FakePrinter`, an IPP printer on Kestrel at a free loopback port (IPv4, IPv6, or every IPv4 address for use by hand), with `ipps` and a self-signed certificate on request. It references no Latiprimere project.
  - **Profiles:** printer attributes from the captured `ippeveprinter` and HP ENVY 5640 responses, changeable per scenario (`Set`, `Remove`); the attributes that depend on how it runs (URI, state, security, operations) are its own.
  - **Records** every request: operation, operation and job attributes as text (collections as `{member=value ...}`), document bytes, credentials sent, and the answer.
  - **Behaves** like a printer: checks requests as RFC 8011 asks, checks jobs against the profile's `*-supported` values, and moves jobs from pending through processing to completed or aborted.
  - **Misbehaves** on cue: busy for the next n jobs, slow answers, a dropped connection on Print-Job, stopped with a reason, jobs that take a while or abort with a message, Basic authentication.
  - **Conformance:** passes every applicable test in `ipptool`'s IPP/1.1 suite, run from the CUPS container. The first run found two encoding faults in the fake, now fixed: SharpIppNext writes a set's later collections, and values inside collections, with names, which `ipptool` reports as duplicates.
- **18 scenarios against it,** none needing Docker: options, tray and paper, raw bytes and refused values as the printer receives them; the HP ENVY's capabilities, with a PDF refused before sending; job states in order, aborted with a reason, a stopped printer and its waiting job, cancelling; busy then accepted, busy past the limit, a dropped connection, a slow printer, a switched-off printer, IPv4-only and IPv6-only `localhost`; a password demanded, an untrusted certificate; and, on Windows, a PDF routed from Microsoft Print to PDF's queue straight to an IPP printer.
- **`fake-printer` command** in the console harness, with options for each behaviour; `tools/test-harness/README.md` explains it.
- **`ScenarioContext.RegisterForDisposal`,** for helpers whose objects a scenario's later steps still use.
- **Results on the Windows dev machine:** 18 of 18 IPP scenarios pass in the console harness without the container; with it, the xUnit host runs all 24 on both frameworks at once: 46 passed, 2 skipped (the Windows test printers aren't installed). Release build clean.

**Learned**
- **Listing doesn't contact configured IPP printers,** so their state is Unknown until looked up, as `IppNetworkPrintSystem` documents. The first version of the stopped-printer scenario expected Stopped from the listing.
- **A misleading message:** an `ipps` printer with an untrusted certificate is refused with "CUPS servers use a self-signed certificate … check it in a server certificate validation callback", but direct IPP has no such callback, and the printer isn't a CUPS server.
- **The busy retry** waits 1 s, then 2 s, so a printer busy twice takes about 3 s to get the job.

**Not done**
- No connect-timeout scenario: it needs an address where nothing answers, which the fake can't provide.
- The Windows IPP route's fallbacks to the spooler: they would send the job to Microsoft Print to PDF without an output file, which needs a file name. They belong with H2's other cross-package checks.

**Next**
- H2: packages together: catalog failure isolation, the Windows IPP route's fallbacks, the samples and docs examples.
- Decide what to do about the certificate message for direct IPP printers.

---

## 2026-11-16: End-to-end test harness, H0

**Done** (owner: "build a test harness for end-to-end testing in all areas of the packages", then answers to the plan's six questions and "Start H0")
- **Plan and decision:** [test-harness-plan.md](test-harness-plan.md) (gaps, design, milestones H0 to H5) and [ADR 0029](decisions/0029-end-to-end-test-harness.md), accepted: scenarios run by two programs; ASP.NET Core allowed in tests; existing `Integration/` tests unchanged; not in CI for now; paper only in manual mode; mDNS tried in CI on Ubuntu.
- **`tests/Latiprimere.EndToEnd`:** the scenario model (`Scenario`, `TargetNeeds`, `HarnessTargets`, `ScenarioContext`, `ScenarioRunner`), Markdown and JSON reports (JSON through a source-generated context, so the harness stays AOT-safe), and support: test printer IDs, a job follower that keeps each change of state, PDF inspection through PDFium, and a cross-process lock that shares the Windows integration tests' semaphore names. Public API only.
- **Six scenarios:** one catalog over every print system lists each test printer once and looks it up again; raw bytes through the CUPS Raw queue; a PDF to CUPS-PDF comes out with three pages; a waiting job cancelled on CUPS and on the Windows test printers, after which a second cancel reports it finished; a PDF to Microsoft Print to PDF through PDFium comes out with three pages.
- **`tests/Latiprimere.EndToEnd.Tests`:** one xUnit case per scenario, all skipped unless `LATIPRIMERE_TEST_E2E` is set; an assembly fixture writes a report per process.
- **`tests/Latiprimere.TestHarness`:** `list`, `targets` and `run [filters]`, with progress as it happens and a report in `out/harness`.
- **`tools/test-harness/run.ps1` and `run.sh`:** start the container (or use a Mac's queues), find the Windows test printers, set the variables for the run only, run, and optionally stop the container.
- **CONTRIBUTING.md:** where end-to-end scenarios go, `LATIPRIMERE_TEST_E2E`, and the manual-mode exception to "never print on paper".
- **Results on the Windows dev machine,** with the container and Microsoft Print to PDF: console harness 5 passed, 1 skipped (the Windows test printers aren't installed; installing them needs elevation); xUnit host with both frameworks at once, 10 passed, 2 skipped; without `LATIPRIMERE_TEST_E2E`, all 12 skipped. With CUPS pointed at a closed port, the catalog scenario failed on the four CUPS queues, still listed the Windows printers, and the harness exited 1.

**Learned**
- CUPS-PDF names its files `<title>-job_<id>.pdf`. Job IDs start again when the container is recreated, while old files stay in `out/cups-output`, so the scenario also requires the file to be newer than the job.
- `run.ps1` recreated the running container the first time, because compose saw the output path written with backslashes rather than the forward slashes it had been started with.

**Not done**
- `run.sh` is checked for syntax only; it hasn't run on Linux or a Mac.
- The Windows cancel scenario hasn't run: it needs the Windows test printers.

**Next**
- H1: the scripted IPP printer, with scenarios for what printers receive, faults and security.

---

## 2026-11-16: Automated releases from the release branch

**Done** (owner: "simplify the versioning and release process … including the version numbering"; [ADR 0028](decisions/0028-automated-releases.md), proposed)
- **Versions are `<.NET major>.0.<build number>`,** such as `11.0.42`: the newest target in `<TargetFrameworks>`, and the Release workflow's run number ([next-version.sh](../tools/releasing/next-version.sh)).
- **Previews until real printers are tested** (owner: "add a preview mode for the time being"): releases end in `-preview` while the repository variable `RELEASE_PREVIEW` is `true` or unset; `false` makes them stable.
- **Merging into `release` publishes.** [release.yml](../.github/workflows/release.yml) makes the release commit with [prepare-release.sh](../tools/releasing/prepare-release.sh), builds and tests it with [build.yml](../.github/workflows/build.yml) on all three runners, publishes its packages, tags it, and opens a post-release pull request into `main`. "Prepare release" and the tag trigger are gone.
- **prepare-release.sh** now also dates the changelog's `## Unreleased` notes, keeps the README status line's wording, takes the package validation baseline from nuget.org rather than the newest tag, and refuses to start until the previous release's post-release pull request is merged.
- **[releasing.md](releasing.md)** has the new steps and one-time setup.

**Learned**
- 0.1.1-preview never published: its tag went on the commit before the release commit, so release.yml refused it. nuget.org has only 0.1.0-preview, which the first automated release takes as its baseline.

**Next**
- Owner: review ADR 0028; delete the `release/v0.1.1-preview` branch, create and protect `release`, and point the `release` environment at it (releasing.md, one-time setup).
- Owner: once manual testing with real printers is confirmed, set `RELEASE_PREVIEW` to `false` and revise the README status line.

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
