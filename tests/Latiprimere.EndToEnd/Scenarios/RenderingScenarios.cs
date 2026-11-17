using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Rendering PDFs for printers that can't take them as they are: on Windows, through PDFium and the printer's driver.
/// </summary>
public static class RenderingScenarios
{
	/// <summary>
	/// A PDF printed to Microsoft Print to PDF, through PDFium and GDI, comes out with the same pages.
	/// </summary>
	public static readonly Scenario MicrosoftPrintToPdfOutput = new Scenario
	{
		Id = "windows.pdf.print-to-pdf-output",
		Area = ScenarioArea.Rendering,
		Title = "A three-page PDF printed to Microsoft Print to PDF, rendered by PDFium, completes and writes a PDF of three pages",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = RenderingScenarios.MicrosoftPrintToPdfOutputAsync
	};

	/// <summary>
	/// A PDF to a printer whose driver prints text only is refused before anything is queued.
	/// </summary>
	public static readonly Scenario TextOnlyDriverRefused = new Scenario
	{
		Id = "windows.pdf.text-only-refused",
		Area = ScenarioArea.Rendering,
		Title = "A PDF to a printer with the Generic / Text Only driver is refused with NotSupportedException naming the driver, and nothing reaches its port",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = RenderingScenarios.TextOnlyDriverRefusedAsync
	};

	/// <summary>
	/// Runs <see cref="MicrosoftPrintToPdfOutput"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task MicrosoftPrintToPdfOutputAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);

		PdfSummary summary = await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.ThreePages, PrintOptions.Default, "three-pages");
		context.CheckEqual("it has the document's three pages", 3, summary.PageCount);
		context.Check("every page has content", summary.Pages.All(page => page.Ink > 0.02), string.Join("; ", summary.Pages));
	}

	/// <summary>
	/// Runs <see cref="TextOnlyDriverRefused"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task TextOnlyDriverRefusedAsync(ScenarioContext context)
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("The Windows print system needs Windows.");
		}

		string outputFile = Path.Combine(context.Targets.WindowsOutputDirectory!, "Latiprimere-Raw.prn"); // Needed, so set
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.WindowsRaw, context.CancellationToken);

		// The port writes every job to one file, so only one process may print there at a time
		using SharedPrinterLock printerLock = new SharedPrinterLock("Latiprimere Raw");
		DateTime before = File.Exists(outputFile) ? File.GetLastWriteTimeUtc(outputFile) : DateTime.MinValue;
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		NotSupportedException? refusal = null;

		try
		{
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);

			// Never leave a job in the test printer's queue
			context.Note($"Job {job.Id} was queued; cancelling it.");
			await job.CancelAsync(CancellationToken.None);
		}
		catch (NotSupportedException exception)
		{
			refusal = exception;
		}

		context.Require("the PDF is refused with NotSupportedException", refusal is not null);
		context.Note("Message: " + refusal!.Message);
		context.Check("the message names the driver", refusal.Message.Contains("Generic / Text Only", StringComparison.Ordinal));
		context.Check("the message suggests raw data instead", refusal.Message.Contains("raw data", StringComparison.Ordinal));

		await Task.Delay(TimeSpan.FromSeconds(2), context.CancellationToken);
		DateTime after = File.Exists(outputFile) ? File.GetLastWriteTimeUtc(outputFile) : DateTime.MinValue;
		context.CheckEqual("nothing reached the printer's port", before, after);
	}
}
