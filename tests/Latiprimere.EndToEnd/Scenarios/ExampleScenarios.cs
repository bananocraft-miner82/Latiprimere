using System.Text;
using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// The documentation's examples (<c>samples/Latiprimere.Examples</c>, one file per page of docs/site), run as the
/// program a reader would run, against the test printers. Each prints only to a test printer named on its command
/// line, except <see cref="FirstPrintJob"/>, which uses the default printer and so runs only where that is one.
/// </summary>
public static class ExampleScenarios
{
	/// <summary>
	/// "Finding printers" and "Installation": listing with and without dependency injection.
	/// </summary>
	public static readonly Scenario Listing = new Scenario
	{
		Id = "samples.examples.list",
		Area = ScenarioArea.Samples,
		Title = "The listing examples, with and without dependency injection, run and list this machine's test printers",
		Needs = TargetNeeds.None,
		RunAsync = ExampleScenarios.ListingAsync
	};

	/// <summary>
	/// "Reading printer capabilities".
	/// </summary>
	public static readonly Scenario Capabilities = new Scenario
	{
		Id = "samples.examples.capabilities",
		Area = ScenarioArea.Samples,
		Title = "The capabilities example runs against a test printer and shows its paper sizes",
		Needs = TargetNeeds.None,
		RunAsync = ExampleScenarios.CapabilitiesAsync
	};

	/// <summary>
	/// "Vendor options and Windows Print Capabilities".
	/// </summary>
	public static readonly Scenario VendorOptions = new Scenario
	{
		Id = "samples.examples.vendor-options",
		Area = ScenarioArea.Samples,
		Title = "The vendor options example runs against a test printer and lists its options under that print system's prefix",
		Needs = TargetNeeds.None,
		RunAsync = ExampleScenarios.VendorOptionsAsync
	};

	/// <summary>
	/// "Logging", with and without dependency injection.
	/// </summary>
	public static readonly Scenario Logging = new Scenario
	{
		Id = "samples.examples.logging",
		Area = ScenarioArea.Samples,
		Title = "The logging examples, with and without dependency injection, log the catalog's print systems at Debug and list printers",
		Needs = TargetNeeds.None,
		RunAsync = ExampleScenarios.LoggingAsync
	};

	/// <summary>
	/// "Your first print job", which prints to the default printer.
	/// </summary>
	public static readonly Scenario FirstPrintJob = new Scenario
	{
		Id = "samples.examples.first-print-job",
		Area = ScenarioArea.Samples,
		Title = "The first-print-job example prints to the default printer and follows the job to Completed (run only where the default printer is a CUPS test queue)",
		Needs = TargetNeeds.CupsContainer,
		RunAsync = ExampleScenarios.FirstPrintJobAsync
	};

	/// <summary>
	/// "Print options and following jobs".
	/// </summary>
	public static readonly Scenario PrintOptionsAndJobs = new Scenario
	{
		Id = "samples.examples.print-options",
		Area = ScenarioArea.Samples,
		Title = "The print-options examples print two copies, two-sided and landscape, to CUPS-PDF and follow the job to Completed; print pages 2 and 3 collated; and follow the printer until stopped",
		Needs = TargetNeeds.CupsContainer,
		RunAsync = ExampleScenarios.PrintOptionsAndJobsAsync
	};

	/// <summary>
	/// "Labels and receipts".
	/// </summary>
	public static readonly Scenario LabelsAndReceipts = new Scenario
	{
		Id = "samples.examples.labels-and-receipts",
		Area = ScenarioArea.Samples,
		Title = "The label and receipt examples send their ZPL and ESC/POS to the Raw queue, which saves them unchanged",
		Needs = TargetNeeds.CupsServer | TargetNeeds.CupsOutput,
		RunAsync = ExampleScenarios.LabelsAndReceiptsAsync
	};

	/// <summary>
	/// "Errors and troubleshooting".
	/// </summary>
	public static readonly Scenario Errors = new Scenario
	{
		Id = "samples.examples.errors",
		Area = ScenarioArea.Samples,
		Title = "The error-handling example explains a printer ID that doesn't exist, and (with the CUPS test server) sends a job to one that does",
		Needs = TargetNeeds.None,
		RunAsync = ExampleScenarios.ErrorsAsync
	};

	/// <summary>
	/// "CUPS servers and the Windows spooler": saving with Microsoft Print to PDF.
	/// </summary>
	public static readonly Scenario PrintToPdf = new Scenario
	{
		Id = "samples.examples.print-to-pdf",
		Area = ScenarioArea.Samples,
		Title = "The Microsoft Print to PDF example writes a PDF of the document's three pages",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = ExampleScenarios.PrintToPdfAsync
	};

	/// <summary>
	/// The CUPS test queues, the only printers safe to be the default for <see cref="FirstPrintJob"/>: Microsoft
	/// Print to PDF would ask where to save, and any other printer may be real.
	/// </summary>
	private static readonly string[] SafeDefaultPrinters = [TestPrinters.CupsPdf, TestPrinters.CupsRaw, TestPrinters.CupsVirtualIpp];

	/// <summary>
	/// Runs <see cref="Listing"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task ListingAsync(ScenarioContext context)
	{
		List<string> expected = new List<string>();

		if (context.Targets.CupsServer is not null)
		{
			expected.Add(TestPrinters.CupsRaw);
		}

		if (context.Targets.HasMicrosoftPrintToPdf)
		{
			expected.Add(TestPrinters.MicrosoftPrintToPdf);
		}

		foreach (string example in new[] { "list", "without-di" })
		{
			SampleRun run = await Samples.RunAsync(context, Samples.Examples, [example]);
			context.CheckEqual($"'{example}' exits with 0", 0, run.ExitCode);

			foreach (string printerId in expected)
			{
				context.Check($"'{example}' lists {printerId}", run.Output.Contains(printerId, StringComparison.Ordinal));
			}
		}
	}

	/// <summary>
	/// Runs <see cref="Capabilities"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CapabilitiesAsync(ScenarioContext context)
	{
		string? printerId = context.Targets.Available.HasFlag(TargetNeeds.CupsContainer) ? TestPrinters.CupsPdf
			: context.Targets.HasMicrosoftPrintToPdf ? TestPrinters.MicrosoftPrintToPdf
			: null;

		if (printerId is null)
		{
			context.Skip("no test printer to read: needs the CUPS test container or Microsoft Print to PDF");
		}

		SampleRun run = await Samples.RunAsync(context, Samples.Examples, ["capabilities", printerId]);
		context.CheckEqual("it exits with 0", 0, run.ExitCode);
		context.Check("it shows the paper sizes, A4 among them", run.Output.Contains("Paper sizes:", StringComparison.Ordinal) && run.Output.Contains("A4", StringComparison.Ordinal));
	}

	/// <summary>
	/// Runs <see cref="VendorOptions"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task VendorOptionsAsync(ScenarioContext context)
	{
		// CUPS-PDF lists ipp: keys; Microsoft Print to PDF lists its driver's windows: features
		(string PrinterId, string Prefix)? target = context.Targets.Available.HasFlag(TargetNeeds.CupsContainer) ? (TestPrinters.CupsPdf, "ipp:")
			: context.Targets.HasMicrosoftPrintToPdf ? (TestPrinters.MicrosoftPrintToPdf, "windows:feature:")
			: null;

		if (target is null)
		{
			context.Skip("no test printer to read: needs the CUPS test container or Microsoft Print to PDF");
		}

		SampleRun run = await Samples.RunAsync(context, Samples.Examples, ["vendor-options", target.Value.PrinterId]);
		context.CheckEqual("it exits with 0", 0, run.ExitCode);
		context.Check($"it lists options as {target.Value.Prefix} keys", run.Output.Contains(Environment.NewLine + target.Value.Prefix, StringComparison.Ordinal) || run.Output.StartsWith(target.Value.Prefix, StringComparison.Ordinal));
		context.Check("it says how it would ask for two pages per sheet", run.Output.Contains("Two pages per sheet:", StringComparison.Ordinal));
	}

	/// <summary>
	/// Runs <see cref="Logging"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task LoggingAsync(ScenarioContext context)
	{
		foreach (string example in new[] { "logging", "logging-without-di" })
		{
			SampleRun run = await Samples.RunAsync(context, Samples.Examples, [example]);
			context.CheckEqual($"'{example}' exits with 0", 0, run.ExitCode);
			context.Check($"'{example}' logs under Latiprimere.PrinterCatalog", run.Output.Contains("Latiprimere.PrinterCatalog", StringComparison.Ordinal));
			context.Check($"'{example}' logs which print systems are available", run.Output.Contains("is available.", StringComparison.Ordinal));
		}
	}

	/// <summary>
	/// Runs <see cref="FirstPrintJob"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task FirstPrintJobAsync(ScenarioContext context)
	{
		// The same print systems as the example, in its order, to see which printer it would use
		string server = context.Targets.CupsServer!; // Needed, so set
		string? defaultId;

		await using (CatalogFixture fixture = new CatalogFixture(services => services.AddLatiprimereCups(options => options.Server = server).AddLatiprimereWindows().AddLatiprimerePdfium()))
		{
			IPrinter? defaultPrinter = await fixture.Catalog.GetDefaultPrinterAsync(context.CancellationToken);
			defaultId = defaultPrinter?.Info.Id;
		}

		if (defaultId is null || !ExampleScenarios.SafeDefaultPrinters.Contains(defaultId))
		{
			context.Skip($"the default printer here is {defaultId ?? "none"}, not a CUPS test queue, so the example could print on paper");
		}

		context.Note($"The default printer is {defaultId}.");
		SampleRun run = await Samples.RunAsync(context, Samples.Examples, ["first-print-job", Samples.GetDocumentPath(TestDocuments.ThreePages)]);
		context.CheckEqual("it exits with 0", 0, run.ExitCode);
		context.Check("it prints on the test queue", run.Output.Contains("Printing on ", StringComparison.Ordinal) && run.Output.Contains(defaultId[(defaultId.IndexOf(':', StringComparison.Ordinal) + 1)..], StringComparison.Ordinal));
		context.Check("it follows the job to Completed", run.Output.Contains(": Completed", StringComparison.Ordinal));
	}

	/// <summary>
	/// Runs <see cref="PrintOptionsAndJobs"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PrintOptionsAndJobsAsync(ScenarioContext context)
	{
		SampleRun run = await Samples.RunAsync(context, Samples.Examples, ["options", TestPrinters.CupsPdf, Samples.GetDocumentPath(TestDocuments.ThreePages)]);
		context.CheckEqual("it exits with 0", 0, run.ExitCode);
		context.Check("it follows the job to Completed", run.Output.Contains("Completed", StringComparison.Ordinal));
		context.Check("it doesn't give up following", !run.Output.Contains("Stopped following", StringComparison.Ordinal));

		SampleRun pages = await Samples.RunAsync(context, Samples.Examples, ["pages", TestPrinters.CupsPdf, Samples.GetDocumentPath(TestDocuments.ThreePages)]);
		context.CheckEqual("the pages example exits with 0", 0, pages.ExitCode);
		context.Check("it sends pages 2 and 3", pages.Output.Contains("Printing pages 2 and 3 as job", StringComparison.Ordinal));

		SampleRun status = await Samples.RunAsync(context, Samples.Examples, ["printer-status", TestPrinters.CupsPdf]);
		context.CheckEqual("the printer-status example exits with 0", 0, status.ExitCode);
		context.Check("it reports the printer's state", status.Output.Contains("Idle", StringComparison.Ordinal) || status.Output.Contains("Processing", StringComparison.Ordinal), status.Output);
		context.Check("it stops following when cancelled", status.Output.Contains("Stopped following the printer.", StringComparison.Ordinal));
	}

	/// <summary>
	/// Runs <see cref="LabelsAndReceipts"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task LabelsAndReceiptsAsync(ScenarioContext context)
	{
		string outputDirectory = context.Targets.CupsOutputDirectory!; // Needed, so set
		string orderNumber = "E2E" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
		DateTime startedAt = DateTime.UtcNow.AddSeconds(-2);

		SampleRun label = await Samples.RunAsync(context, Samples.Examples, ["label", TestPrinters.CupsRaw, orderNumber]);
		context.CheckEqual("the label example exits with 0", 0, label.ExitCode);
		string? labelFile = await OutputSearch.FindNewFileContainingAsync(outputDirectory, startedAt, Encoding.ASCII.GetBytes($"^FDOrder {orderNumber}^FS"), context.CancellationToken);
		context.Check("the Raw queue saved the ZPL label, with the order number", labelFile is not null, labelFile ?? "not found");

		SampleRun receipt = await Samples.RunAsync(context, Samples.Examples, ["receipt", TestPrinters.CupsRaw]);
		context.CheckEqual("the receipt example exits with 0", 0, receipt.ExitCode);
		byte[] receiptStart = [0x1B, 0x40, 0x1B, 0x45, 0x01, .. Encoding.ASCII.GetBytes("Corner Cafe\n")];
		string? receiptFile = await OutputSearch.FindNewFileContainingAsync(outputDirectory, startedAt, receiptStart, context.CancellationToken);
		context.Check("the Raw queue saved the ESC/POS receipt, control codes included", receiptFile is not null, receiptFile ?? "not found");
	}

	/// <summary>
	/// Runs <see cref="Errors"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task ErrorsAsync(ScenarioContext context)
	{
		string document = Samples.GetDocumentPath(TestDocuments.ThreePages);
		SampleRun missing = await Samples.RunAsync(context, Samples.Examples, ["errors", "nosuch:printer", document]);
		context.CheckEqual("it exits with 0", 0, missing.ExitCode);
		context.Check("it explains the missing printer", missing.Output.Contains("There's no printer with the ID nosuch:printer.", StringComparison.Ordinal));

		if (context.Targets.CupsServer is not null)
		{
			SampleRun sent = await Samples.RunAsync(context, Samples.Examples, ["errors", TestPrinters.CupsRaw, document]);
			context.CheckEqual("with a test printer, it exits with 0", 0, sent.ExitCode);
			context.Check("and sends the job", sent.Output.Contains("Sent as job", StringComparison.Ordinal));
		}
	}

	/// <summary>
	/// Runs <see cref="PrintToPdf"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PrintToPdfAsync(ScenarioContext context)
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("Microsoft Print to PDF is a Windows printer.");
		}

		string outputFile = context.GetEvidencePath("example-print-to-pdf.pdf");

		// The example's process prints while this one holds the printer for it
		using (SharedPrinterLock printerLock = new SharedPrinterLock(TestPrinters.MicrosoftPrintToPdfName))
		{
			SampleRun run = await Samples.RunAsync(context, Samples.Examples, ["print-to-pdf", Samples.GetDocumentPath(TestDocuments.ThreePages), outputFile]);
			context.CheckEqual("it exits with 0", 0, run.ExitCode);
			context.Check("it follows the job to Completed", run.Output.Contains("Completed", StringComparison.Ordinal));
		}

		byte[]? written = await OutputFileWaiter.WaitForAnyContentAsync(outputFile, context.CancellationToken);
		context.Require("it writes the file", written is not null && written.Length > 0, outputFile);
		context.AddEvidence(outputFile, "What the example saved");
		context.CheckEqual("the PDF has the document's three pages", 3, PdfInspector.Inspect(written!).PageCount);
	}
}
