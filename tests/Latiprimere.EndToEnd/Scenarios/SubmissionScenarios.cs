using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Sending documents, and what the printer receives.
/// </summary>
public static class SubmissionScenarios
{
	/// <summary>
	/// Raw data sent to the CUPS Raw queue arrives byte for byte.
	/// </summary>
	public static readonly Scenario CupsRawSameBytes = new Scenario
	{
		Id = "cups.raw.same-bytes",
		Area = ScenarioArea.Submission,
		Title = "Raw data sent to the CUPS Raw queue completes and arrives byte for byte, line endings included",
		Needs = TargetNeeds.CupsServer | TargetNeeds.CupsOutput,
		RunAsync = SubmissionScenarios.CupsRawSameBytesAsync
	};

	/// <summary>
	/// A PDF sent to CUPS-PDF comes out as a PDF with the same pages.
	/// </summary>
	public static readonly Scenario CupsPdfOutput = new Scenario
	{
		Id = "cups.pdf.cups-pdf-output",
		Area = ScenarioArea.Submission,
		Title = "A three-page PDF sent to CUPS-PDF completes, and CUPS-PDF writes a PDF of three pages",
		Needs = TargetNeeds.CupsServer | TargetNeeds.CupsOutput | TargetNeeds.CupsContainer,
		RunAsync = SubmissionScenarios.CupsPdfOutputAsync
	};

	/// <summary>
	/// Runs <see cref="CupsRawSameBytes"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CupsRawSameBytesAsync(ScenarioContext context)
	{
		string outputDirectory = context.Targets.CupsOutputDirectory!; // Needed, so set
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsRaw, context.CancellationToken);
		byte[] expected = RawData.Create(SubmissionScenarios.CupsRawSameBytes.Id);
		using MemoryStream data = new MemoryStream(expected);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, context.CancellationToken);
		context.Note($"Submitted job {job.Id}.");
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		// The container's capture backend names the file after the job; a Mac's capture server numbers its own
		byte[]? received = context.Targets.CupsSetup == CupsTestSetup.Mac
			? await OutputFileWaiter.WaitForFileWithContentAsync(outputDirectory, expected, context.CancellationToken)
			: await OutputFileWaiter.WaitForContentAsync(Path.Combine(outputDirectory, $"Raw-{job.Id}.prn"), expected, context.CancellationToken);

		if (received is not null)
		{
			await context.SaveEvidenceAsync("received.prn", received, "What the Raw queue saved");
		}

		context.Check
		(
			"the queue received the same bytes",
			received is not null && received.AsSpan().SequenceEqual(expected),
			received is null ? "nothing arrived" : $"{received.Length} bytes received, {expected.Length} sent"
		);
	}

	/// <summary>
	/// Runs <see cref="CupsPdfOutput"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CupsPdfOutputAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsPdf, context.CancellationToken);

		PdfSummary summary = await PdfPrinters.PrintToCupsPdfAsync(context, printer, TestDocuments.ThreePages, PrintOptions.Default, "three-pages");
		context.CheckEqual("it has the document's three pages", 3, summary.PageCount);
		context.Check("every page has content", summary.Pages.All(page => page.Ink > 0.02), string.Join("; ", summary.Pages));
	}
}
