using System.Text;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Plain text (ADR 0048): laid out by CUPS itself, and by Latiprimere as a PDF for network printers and Windows.
/// </summary>
public static class TextScenarios
{
	/// <summary>
	/// Text to a network printer that takes PDF arrives as a PDF Latiprimere laid out.
	/// </summary>
	public static readonly Scenario TextAsPdf = new Scenario
	{
		Id = "ipp.text.as-pdf",
		Area = ScenarioArea.Rendering,
		Title = "Two pages of UTF-8 text split by a form feed, to a printer with ippeveprinter's profile, arrive as a PDF of two portrait US Letter pages set in Courier, and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = TextScenarios.TextAsPdfAsync
	};

	/// <summary>
	/// Text with a character Courier can't show is refused before anything is sent to a network printer.
	/// </summary>
	public static readonly Scenario CharacterRefused = new Scenario
	{
		Id = "ipp.text.character-refused",
		Area = ScenarioArea.Rendering,
		Title = "Text with Cyrillic to a network printer is refused with NotSupportedException naming the character and where it is, and no job is sent",
		Needs = TargetNeeds.None,
		RunAsync = TextScenarios.CharacterRefusedAsync
	};

	/// <summary>
	/// Text printed to Microsoft Print to PDF, through the PDF Latiprimere laid out, PDFium and GDI.
	/// </summary>
	public static readonly Scenario MicrosoftPrintToPdf = new Scenario
	{
		Id = "windows.text.print-to-pdf",
		Area = ScenarioArea.Rendering,
		Title = "Two pages of text split by a form feed, printed to Microsoft Print to PDF, write a PDF of two portrait pages with text on each",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = TextScenarios.MicrosoftPrintToPdfAsync
	};

	/// <summary>
	/// Text sent to CUPS as it is, which CUPS lays out and CUPS-PDF writes.
	/// </summary>
	public static readonly Scenario CupsPdf = new Scenario
	{
		Id = "cups.text.cups-pdf-output",
		Area = ScenarioArea.Submission,
		Title = "Two pages of text split by a form feed, sent to CUPS-PDF as text/plain, complete, and CUPS-PDF writes a PDF of two pages with text on each",
		Needs = TargetNeeds.CupsServer | TargetNeeds.CupsOutput | TargetNeeds.CupsContainer,
		RunAsync = TextScenarios.CupsPdfAsync
	};

	/// <summary>
	/// Runs <see cref="TextAsPdf"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task TextAsPdfAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		context.Check("the capabilities list text", capabilities.DocumentFormats.Contains(DocumentFormat.Text), string.Join(", ", capabilities.DocumentFormats));

		using FileStream document = TestDocuments.Open(TestDocuments.TwoPagesText);
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromText(document, Encoding.UTF8), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is application/pdf", "application/pdf", printJob.Find("document-format")?.Value);
		context.Check("the PDF sets its text in Courier", Encoding.Latin1.GetString(printJob.Document).Contains("/BaseFont /Courier", StringComparison.Ordinal));
		await context.SaveEvidenceAsync("received.pdf", printJob.Document, "The PDF the printer received");

		PdfSummary summary = PdfInspector.Inspect(printJob.Document);

		foreach (string line in summary.Describe())
		{
			context.Note(line);
		}

		context.RequireEqual("it is two pages, split at the form feed", 2, summary.PageCount);
		context.Check("both are portrait US Letter", summary.Pages.All(page => !page.IsLandscape && page.IsPaper(215.9, 279.4)), string.Join("; ", summary.Pages));
		context.Check("both have text on them", summary.Pages.All(page => page.Ink > 0.002), string.Join("; ", summary.Pages));
	}

	/// <summary>
	/// Runs <see cref="CharacterRefused"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CharacterRefusedAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		NotSupportedException? refusal = null;

		try
		{
			await printer.SubmitAsync(PrintContent.FromText("Price list\nПрайс-лист"), PrintOptions.Default, context.CancellationToken);
		}
		catch (NotSupportedException exception)
		{
			refusal = exception;
		}

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Require("submitting throws NotSupportedException", refusal is not null);
		context.Note("Message: " + refusal!.Message);
		context.Check("the message names the character and where it is", refusal.Message.Contains("U+041F", StringComparison.Ordinal) && refusal.Message.Contains("line 2, column 1", StringComparison.Ordinal));
		context.CheckEqual("no Print-Job is sent", 0, fake.GetRequests(IppOperation.PrintJob).Count);
	}

	/// <summary>
	/// Runs <see cref="MicrosoftPrintToPdf"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task MicrosoftPrintToPdfAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);

		PdfSummary summary = await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.TwoPagesText, PrintOptions.Default, "text");
		context.RequireEqual("it is two pages, split at the form feed", 2, summary.PageCount);
		context.Check("both are portrait", summary.Pages.All(page => !page.IsLandscape), string.Join("; ", summary.Pages));
		context.Check("both have text on them", summary.Pages.All(page => page.Ink > 0.002), string.Join("; ", summary.Pages));
	}

	/// <summary>
	/// Runs <see cref="CupsPdf"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CupsPdfAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsPdf, context.CancellationToken);

		PdfSummary summary = await PdfPrinters.PrintToCupsPdfAsync(context, printer, TestDocuments.TwoPagesText, PrintOptions.Default, "text");
		context.RequireEqual("it is two pages, split at the form feed", 2, summary.PageCount);
		context.Check("both have text on them", summary.Pages.All(page => page.Ink > 0.002), string.Join("; ", summary.Pages));
	}
}
