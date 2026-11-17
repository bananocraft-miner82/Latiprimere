using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// The Windows print system sending PDFs straight to a queue's IPP printer (ADR 0027), with the scripted printer
/// standing in for it.
/// </summary>
public static class WindowsIppRouteScenarios
{
	/// <summary>
	/// A PDF to a Windows queue mapped to an IPP printer goes to the printer, unrendered, and reports its outcome.
	/// </summary>
	public static readonly Scenario PdfToPrinter = new Scenario
	{
		Id = "windows.ipp-route.pdf-to-printer",
		Area = ScenarioArea.Submission,
		Title = "A PDF to Microsoft Print to PDF's queue, mapped to an IPP printer, reaches that printer unchanged under the Windows printer ID, and reports the printer's outcome",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = WindowsIppRouteScenarios.PdfToPrinterAsync
	};

	/// <summary>
	/// Runs <see cref="PdfToPrinter"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PdfToPrinterAsync(ScenarioContext context)
	{
		if (!OperatingSystem.IsWindows())
		{
			throw new PlatformNotSupportedException("The Windows print system needs Windows.");
		}

		await using FakePrinter fake = await FakePrinters.StartAsync(context);

		await using CatalogFixture fixture = new CatalogFixture
		(
			services => services.AddLatiprimerePdfium().AddLatiprimereWindows
			(
				options =>
				{
					options.PrintPdfOverIpp = true;
					options.IppPrinterUris[TestPrinters.MicrosoftPrintToPdfName] = fake.PrinterUri;
				}
			)
		);

		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);
		byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.ThreePages), context.CancellationToken);
		using MemoryStream document = new MemoryStream(pdf);

		// Held in case the job falls back to the driver, which can't share with another process
		using SharedPrinterLock printerLock = new SharedPrinterLock(TestPrinters.MicrosoftPrintToPdfName);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), new PrintOptions { Copies = 2 }, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.CheckEqual("the job keeps the Windows printer ID", TestPrinters.MicrosoftPrintToPdf, job.PrinterId);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);
		context.Check("its status is the printer's, not the spooler's", status.StateMessage?.Contains("Windows does not record", StringComparison.Ordinal) != true, status.StateMessage ?? "no message");

		IReadOnlyList<RecordedRequest> printJobs = fake.GetRequests(IppOperation.PrintJob);
		context.Require("the printer received one Print-Job", printJobs.Count == 1, $"{printJobs.Count}");
		context.CheckEqual("as application/pdf", "application/pdf", printJobs[0].Find("document-format")?.Value);
		context.CheckEqual("with copies 2", "2", printJobs[0].Find("copies")?.Value);
		context.Check("the PDF arrives unchanged, not rendered", printJobs[0].Document.AsSpan().SequenceEqual(pdf), $"{printJobs[0].Document.Length} bytes received, {pdf.Length} sent");
	}
}
