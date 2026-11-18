using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// What a printer receives over direct IPP, checked on the scripted printer, which records every attribute.
/// </summary>
public static class IppSubmissionScenarios
{
	/// <summary>
	/// The typed print options and a vendor option reach the printer as the IPP job attributes they stand for.
	/// </summary>
	public static readonly Scenario OptionsArrive = new Scenario
	{
		Id = "ipp.submission.options-arrive",
		Area = ScenarioArea.Submission,
		Title = "Copies, two-sided, monochrome, landscape, A4 and ipp:print-quality reach the printer as IPP job attributes, with the PDF unchanged",
		Needs = TargetNeeds.None,
		RunAsync = IppSubmissionScenarios.OptionsArriveAsync
	};

	/// <summary>
	/// A tray goes in <c>media-col</c> with the paper size, rather than <c>media</c>.
	/// </summary>
	public static readonly Scenario TrayAndSize = new Scenario
	{
		Id = "ipp.submission.tray-and-size",
		Area = ScenarioArea.Submission,
		Title = "A tray and paper size chosen from the printer's capabilities arrive together in media-col, in hundredths of a millimetre",
		Needs = TargetNeeds.None,
		RunAsync = IppSubmissionScenarios.TrayAndSizeAsync
	};

	/// <summary>
	/// Raw data arrives byte for byte as <c>application/octet-stream</c>.
	/// </summary>
	public static readonly Scenario RawBytes = new Scenario
	{
		Id = "ipp.submission.raw-bytes",
		Area = ScenarioArea.Submission,
		Title = "Raw data reaches the printer byte for byte as application/octet-stream",
		Needs = TargetNeeds.None,
		RunAsync = IppSubmissionScenarios.RawBytesAsync
	};

	/// <summary>
	/// A value the printer doesn't support is refused with the printer's own status.
	/// </summary>
	public static readonly Scenario UnsupportedValueRefused = new Scenario
	{
		Id = "ipp.submission.unsupported-value-refused",
		Area = ScenarioArea.Submission,
		Title = "Two-sided printing on a one-sided printer is refused with client-error-attributes-or-values-not-supported by Validate-Job, before the document is sent, and no job is created",
		Needs = TargetNeeds.None,
		RunAsync = IppSubmissionScenarios.UnsupportedValueRefusedAsync
	};

	/// <summary>
	/// The IPP status a printer sends for a job asking for something it can't do.
	/// </summary>
	private const int AttributesOrValuesNotSupported = 0x040B;

	/// <summary>
	/// Runs <see cref="OptionsArrive"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task OptionsArriveAsync(ScenarioContext context)
	{
		// ippeveprinter's profile, made two-sided and colour so every option is one the printer supports
		FakePrinterProfile profile = (await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken))
			.Set("sides-supported", Tag.Keyword, "one-sided", "two-sided-long-edge", "two-sided-short-edge")
			.Set("print-color-mode-supported", Tag.Keyword, "monochrome", "color");

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		context.Check("the capabilities include long-edge two-sided printing", capabilities.DuplexModes.Contains(DuplexMode.LongEdge));
		MediaSize? a4 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm");
		context.Require("the capabilities include A4", a4 is not null);

		byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.ThreePages), context.CancellationToken);
		using MemoryStream document = new MemoryStream(pdf);

		PrintOptions options = new PrintOptions
		{
			JobName = "Latiprimere end-to-end options",
			Copies = 2,
			Duplex = DuplexMode.LongEdge,
			ColorMode = ColorMode.Monochrome,
			Orientation = PageOrientation.Landscape,
			MediaSize = a4,
			VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "5" }
		};

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("job-name is the job's name", "Latiprimere end-to-end options", printJob.Find("job-name")?.Value);
		context.CheckEqual("document-format is application/pdf", "application/pdf", printJob.Find("document-format")?.Value);
		context.Check("requesting-user-name is sent", !string.IsNullOrEmpty(printJob.Find("requesting-user-name")?.Value));
		context.CheckEqual("copies is 2", "2", printJob.Find("copies")?.Value);
		context.CheckEqual("sides is two-sided-long-edge", "two-sided-long-edge", printJob.Find("sides")?.Value);
		context.CheckEqual("print-color-mode is monochrome", "monochrome", printJob.Find("print-color-mode")?.Value);
		context.CheckEqual("orientation-requested is landscape (4)", "4", printJob.Find("orientation-requested")?.Value);
		context.CheckEqual("media is A4", "iso_a4_210x297mm", printJob.Find("media")?.Value);
		context.Check("no media-col is sent with media", printJob.Find("media-col") is null);
		context.CheckEqual("print-quality is high (5), from the vendor option", "5", printJob.Find("print-quality")?.Value);
		context.Check("the PDF arrives unchanged", printJob.Document.AsSpan().SequenceEqual(pdf), $"{printJob.Document.Length} bytes received, {pdf.Length} sent");
	}

	/// <summary>
	/// Runs <see cref="TrayAndSize"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task TrayAndSizeAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		InputTray? manual = capabilities.InputTrays.FirstOrDefault(tray => tray.Id == "manual");
		MediaSize? a4 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm");
		context.Require("the capabilities include the manual tray and A4", manual is not null && a4 is not null);

		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { InputTray = manual, MediaSize = a4 };
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		string mediaCol = printJob.Find("media-col")?.Value ?? string.Empty;
		context.Check("media-col names the manual tray", mediaCol.Contains("media-source=manual", StringComparison.Ordinal), mediaCol);
		context.Check("media-col gives A4 as 21000 x 29700", mediaCol.Contains("x-dimension=21000", StringComparison.Ordinal) && mediaCol.Contains("y-dimension=29700", StringComparison.Ordinal), mediaCol);
		context.Check("media isn't sent as well", printJob.Find("media") is null);
	}

	/// <summary>
	/// Runs <see cref="RawBytes"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task RawBytesAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		byte[] expected = RawData.Create(IppSubmissionScenarios.RawBytes.Id);
		using MemoryStream data = new MemoryStream(expected);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is application/octet-stream", "application/octet-stream", printJob.Find("document-format")?.Value);
		context.Check("the printer received the same bytes", printJob.Document.AsSpan().SequenceEqual(expected), $"{printJob.Document.Length} bytes received, {expected.Length} sent");
		context.CheckEqual("no job template attributes are sent for the defaults", 0, printJob.JobAttributes.Count);
	}

	/// <summary>
	/// Runs <see cref="UnsupportedValueRefused"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task UnsupportedValueRefusedAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { Duplex = DuplexMode.LongEdge };

		PrintException? exception = null;

		try
		{
			await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		}
		catch (PrintException caught)
		{
			exception = caught;
		}

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Require("submitting throws a PrintException", exception is not null);
		context.Note("Message: " + exception!.Message);
		context.CheckEqual("its error code is the printer's status 0x040B", (int?)IppSubmissionScenarios.AttributesOrValuesNotSupported, exception.NativeErrorCode);

		// Validate-Job carries the job's attributes without the document, so the refusal costs no upload (ADR 0033)
		IReadOnlyList<RecordedRequest> validations = fake.GetRequests(IppOperation.ValidateJob);
		context.CheckEqual("the job's attributes are validated once", 1, validations.Count);
		context.Check("Validate-Job is what the printer refused", validations.All(request => request.StatusCode == (IppStatusCode)IppSubmissionScenarios.AttributesOrValuesNotSupported));
		context.Check("Validate-Job asked for two-sided printing", validations.Single().Find("sides")?.Value == "two-sided-long-edge");
		context.CheckEqual("the document is never sent", 0, fake.GetRequests(IppOperation.PrintJob).Count);
	}
}
