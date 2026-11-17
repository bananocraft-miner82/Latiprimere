using System.Globalization;
using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// What the PDF-writing virtual printers actually print: page sizes, orientation, page count, and whether the content
/// and its colour arrive, measured from the PDFs they write.
/// </summary>
/// <remarks>
/// <para>
/// Each print system is checked only for what it does with each option, found on 2026-11-17 with
/// <c>mixed-1-page.pdf</c> (A4, 16% ink, 8% colour):
/// </para>
/// <list type="bullet">
/// <item><description>CUPS-PDF honours paper size and monochrome. It keeps a PDF's own orientation, as CUPS's PDF
/// filter does, and writes one copy, so landscape and copies aren't checked there.</description></item>
/// <item><description>Microsoft Print to PDF honours paper size, landscape and copies. It ignores the DEVMODE's colour
/// setting, so since 2026-11-17 Latiprimere greys a monochrome job's pages itself before the driver sees them
/// (ADR 0025).</description></item>
/// </list>
/// </remarks>
public static class OutputScenarios
{
	/// <summary>
	/// CUPS-PDF prints on the default and the chosen paper, with the content intact.
	/// </summary>
	public static readonly Scenario CupsPaper = new Scenario
	{
		Id = "cups.output.paper",
		Area = ScenarioArea.Rendering,
		Title = "CUPS-PDF prints a colour page on its default A4 and on Letter chosen from its capabilities, with the content and colour intact",
		Needs = TargetNeeds.CupsServer | TargetNeeds.CupsOutput | TargetNeeds.CupsContainer,
		RunAsync = OutputScenarios.CupsPaperAsync
	};

	/// <summary>
	/// CUPS-PDF prints in grey when monochrome is chosen, and in colour when colour is.
	/// </summary>
	public static readonly Scenario CupsColourMode = new Scenario
	{
		Id = "cups.output.colour-mode",
		Area = ScenarioArea.Rendering,
		Title = "CUPS-PDF prints a colour page with no colour when monochrome is chosen, and keeps its colour when colour is",
		Needs = TargetNeeds.CupsServer | TargetNeeds.CupsOutput | TargetNeeds.CupsContainer,
		RunAsync = OutputScenarios.CupsColourModeAsync
	};

	/// <summary>
	/// Microsoft Print to PDF prints on the chosen paper, turned for landscape, with the content intact.
	/// </summary>
	public static readonly Scenario WindowsPaperAndOrientation = new Scenario
	{
		Id = "windows.output.paper-and-orientation",
		Area = ScenarioArea.Rendering,
		Title = "Microsoft Print to PDF prints a colour page with its content and colour intact, on A5 when chosen, and on a landscape page when landscape is",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = OutputScenarios.WindowsPaperAndOrientationAsync
	};

	/// <summary>
	/// Microsoft Print to PDF prints each copy.
	/// </summary>
	public static readonly Scenario WindowsCopies = new Scenario
	{
		Id = "windows.output.copies",
		Area = ScenarioArea.Rendering,
		Title = "Two copies of a one-page PDF to Microsoft Print to PDF come out as two pages, each with the page's content",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = OutputScenarios.WindowsCopiesAsync
	};

	/// <summary>
	/// Microsoft Print to PDF prints in grey when monochrome is chosen, and in colour when colour is.
	/// </summary>
	public static readonly Scenario WindowsColourMode = new Scenario
	{
		Id = "windows.output.colour-mode",
		Area = ScenarioArea.Rendering,
		Title = "Microsoft Print to PDF prints a colour page with no colour when monochrome is chosen, though the driver ignores the DEVMODE's colour setting, and keeps its colour when colour is",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = OutputScenarios.WindowsColourModeAsync
	};

	/// <summary>
	/// The most a page's ink may differ from the source's, as a share of it, for its content to count as intact on
	/// paper of the same shape: allows for scaling to fit and for anti-aliasing.
	/// </summary>
	private const double InkTolerance = 0.3;

	/// <summary>
	/// The colour share above which a page counts as printed in colour; the test page is about 8%.
	/// </summary>
	private const double HasColour = 0.02;

	/// <summary>
	/// The colour share below which a page counts as printed without colour.
	/// </summary>
	private const double NoColour = 0.005;

	/// <summary>
	/// The ink share above which a page counts as not blank.
	/// </summary>
	private const double NotBlank = 0.02;

	/// <summary>
	/// Runs <see cref="CupsPaper"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CupsPaperAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsPdf, context.CancellationToken);
		PdfPage source = OutputScenarios.InspectSourcePage(context);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		MediaSize? letter = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "na_letter_8.5x11in");
		context.Require("the capabilities include Letter", letter is not null);

		PdfPage onDefault = (await PdfPrinters.PrintToCupsPdfAsync(context, printer, TestDocuments.MixedOnePage, PrintOptions.Default, "default")).Pages.Single();
		context.Check("by default the page is A4, portrait", onDefault.IsPaper(210, 297) && !onDefault.IsLandscape, onDefault.ToString());
		OutputScenarios.CheckContent(context, "on A4", source, onDefault);

		PdfPage onLetter = (await PdfPrinters.PrintToCupsPdfAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { MediaSize = letter }, "letter")).Pages.Single();
		context.Check("with Letter chosen the page is Letter, 216 x 279 mm", onLetter.IsPaper(215.9, 279.4), onLetter.ToString());
		OutputScenarios.CheckContent(context, "on Letter", source, onLetter);
	}

	/// <summary>
	/// Runs <see cref="CupsColourMode"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CupsColourModeAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsPdf, context.CancellationToken);

		PdfPage mono = (await PdfPrinters.PrintToCupsPdfAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { ColorMode = ColorMode.Monochrome }, "monochrome")).Pages.Single();
		context.Check("with monochrome, the page has content", mono.Ink > OutputScenarios.NotBlank, mono.ToString());
		context.Check("with monochrome, the page has no colour", mono.Colour < OutputScenarios.NoColour, OutputScenarios.Percent(mono.Colour));

		PdfPage colour = (await PdfPrinters.PrintToCupsPdfAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { ColorMode = ColorMode.Color }, "colour")).Pages.Single();
		context.Check("with colour, the page keeps its colour", colour.Colour > OutputScenarios.HasColour, OutputScenarios.Percent(colour.Colour));
	}

	/// <summary>
	/// Runs <see cref="WindowsPaperAndOrientation"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsPaperAndOrientationAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);
		PdfPage source = OutputScenarios.InspectSourcePage(context);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		MediaSize? a4 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm");
		MediaSize? a5 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a5_148x210mm");
		context.Require("the capabilities include A4 and A5", a4 is not null && a5 is not null);

		// A4 chosen, since the driver's default paper depends on the PC's region
		PdfPage onA4 = (await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { MediaSize = a4 }, "a4")).Pages.Single();
		context.Check("with A4 chosen the page is A4, portrait", onA4.IsPaper(210, 297) && !onA4.IsLandscape, onA4.ToString());
		OutputScenarios.CheckContent(context, "on A4", source, onA4);

		PdfPage onA5 = (await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { MediaSize = a5 }, "a5")).Pages.Single();
		context.Check("with A5 chosen the page is A5, 148 x 210 mm", onA5.IsPaper(148, 210), onA5.ToString());
		OutputScenarios.CheckContent(context, "on A5", source, onA5);

		PrintOptions landscapeOptions = new PrintOptions { MediaSize = a4, Orientation = PageOrientation.Landscape };
		PdfPage landscape = (await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.MixedOnePage, landscapeOptions, "landscape")).Pages.Single();
		context.Check("with landscape the page is A4 turned, 297 x 210 mm", landscape.IsPaper(210, 297) && landscape.IsLandscape, landscape.ToString());

		// The portrait page is scaled down to fit across the landscape one, so it covers less of it
		context.Check("with landscape the page has content", landscape.Ink > OutputScenarios.NotBlank, landscape.ToString());
	}

	/// <summary>
	/// Runs <see cref="WindowsCopies"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsCopiesAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);
		PdfPage source = OutputScenarios.InspectSourcePage(context);

		PdfSummary summary = await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { Copies = 2 }, "copies");
		context.CheckEqual("two copies come out as two pages", 2, summary.PageCount);

		for (int index = 0; index < summary.PageCount; index++)
		{
			OutputScenarios.CheckContent(context, $"in copy {index + 1}", source, summary.Pages[index]);
		}
	}

	/// <summary>
	/// Runs <see cref="WindowsColourMode"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsColourModeAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);

		PdfPage colour = (await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { ColorMode = ColorMode.Color }, "colour")).Pages.Single();
		context.Check("with colour, the page keeps its colour", colour.Colour > OutputScenarios.HasColour, OutputScenarios.Percent(colour.Colour));

		PdfPage mono = (await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.MixedOnePage, new PrintOptions { ColorMode = ColorMode.Monochrome }, "monochrome")).Pages.Single();
		context.Check("with monochrome, the page has content", mono.Ink > OutputScenarios.NotBlank, mono.ToString());
		context.Check("with monochrome, the page has no colour", mono.Colour < OutputScenarios.NoColour, OutputScenarios.Percent(mono.Colour));
	}

	/// <summary>
	/// Checks that a printed page has the source page's content: not blank, its ink within
	/// <see cref="InkTolerance"/> of the source's, and its colour kept.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="where">Which page, for the checks' names, such as <c>on A4</c>.</param>
	/// <param name="source">The source page.</param>
	/// <param name="printed">The printed page.</param>
	private static void CheckContent(ScenarioContext context, string where, PdfPage source, PdfPage printed)
	{
		double ratio = printed.Ink / source.Ink;
		string detail = string.Create(CultureInfo.InvariantCulture, $"ink {OutputScenarios.Percent(printed.Ink)} against {OutputScenarios.Percent(source.Ink)} sent");
		context.Check($"{where}, the content arrives intact", ratio >= 1 - OutputScenarios.InkTolerance && ratio <= 1 + OutputScenarios.InkTolerance, detail);
		context.Check($"{where}, the colour arrives", printed.Colour > OutputScenarios.HasColour, OutputScenarios.Percent(printed.Colour));
	}

	/// <summary>
	/// Reads the test page as it is sent, and notes it.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <returns>Its only page.</returns>
	private static PdfPage InspectSourcePage(ScenarioContext context)
	{
		PdfPage source = PdfPrinters.InspectSource(TestDocuments.MixedOnePage).Pages.Single();
		context.Note($"Sent: {source}");

		return source;
	}

	/// <summary>
	/// Formats a share as a percentage.
	/// </summary>
	/// <param name="share">The share, from 0 to 1.</param>
	/// <returns>The percentage, such as <c>8.2%</c>.</returns>
	private static string Percent(double share)
	{
		return share.ToString("P1", CultureInfo.InvariantCulture);
	}
}
