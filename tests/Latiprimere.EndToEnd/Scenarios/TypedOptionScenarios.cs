using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Testing.Raster;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// The typed options added before 1.0 (ADR 0056) and passwords for PDFs (ADR 0054), as a printer receives them over
/// direct IPP, and as the Windows spooler applies or refuses them.
/// </summary>
public static class TypedOptionScenarios
{
	/// <summary>
	/// Page ranges, collation, quality, finishings, an output bin, a media type and a custom size reach the printer
	/// as their IPP job attributes, after the capabilities list them.
	/// </summary>
	public static readonly Scenario IppTypedOptions = new Scenario
	{
		Id = "ipp.submission.typed-options",
		Area = ScenarioArea.Submission,
		Title = "Page ranges, uncollated copies, high quality, staples and punching, an output bin, labels and a 102 x 152 mm custom size are listed in the capabilities and reach the printer as page-ranges, multiple-document-handling, print-quality, finishings, output-bin and media-col; raw data with page ranges is refused",
		Needs = TargetNeeds.None,
		RunAsync = TypedOptionScenarios.IppTypedOptionsAsync
	};

	/// <summary>
	/// Raster renders only the pages chosen, and doesn't ask the printer to choose them again.
	/// </summary>
	public static readonly Scenario IppRasterPageRanges = new Scenario
	{
		Id = "ipp.raster.page-ranges",
		Area = ScenarioArea.Rendering,
		Title = "Pages 2 and 3 of a three-page PDF to a printer without PDF arrive as two pages of PWG raster, counted as two, with no page-ranges attribute",
		Needs = TargetNeeds.None,
		RunAsync = TypedOptionScenarios.IppRasterPageRangesAsync
	};

	/// <summary>
	/// A printer that takes passwords is sent the PDF as it is, with its password.
	/// </summary>
	public static readonly Scenario IppPdfPasswordSent = new Scenario
	{
		Id = "ipp.submission.pdf-password-sent",
		Area = ScenarioArea.Submission,
		Title = "A password-protected PDF to a printer listing document-password-supported arrives unchanged, with its password as document-password",
		Needs = TargetNeeds.None,
		RunAsync = TypedOptionScenarios.IppPdfPasswordSentAsync
	};

	/// <summary>
	/// A printer that doesn't take passwords is sent the pages as raster, opened here with the password.
	/// </summary>
	public static readonly Scenario IppPdfPasswordRendered = new Scenario
	{
		Id = "ipp.raster.pdf-password-rendered",
		Area = ScenarioArea.Rendering,
		Title = "A password-protected PDF to a printer that takes PDF but not passwords is opened by PDFium with the password and sent as PWG raster, without document-password; a wrong password is refused before anything is sent",
		Needs = TargetNeeds.None,
		RunAsync = TypedOptionScenarios.IppPdfPasswordRenderedAsync
	};

	/// <summary>
	/// Microsoft Print to PDF prints a password-protected PDF with its password.
	/// </summary>
	public static readonly Scenario WindowsPdfPassword = new Scenario
	{
		Id = "windows.pdf.password",
		Area = ScenarioArea.Rendering,
		Title = "A password-protected PDF printed with its password to Microsoft Print to PDF comes out as its one page of line art; a wrong password fails with PrintException and prints nothing",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = TypedOptionScenarios.WindowsPdfPasswordAsync
	};

	/// <summary>
	/// The Windows spooler doesn't apply finishings or output bins, so the catalog refuses them.
	/// </summary>
	public static readonly Scenario WindowsFinishingsRefused = new Scenario
	{
		Id = "windows.output.finishings-refused",
		Area = ScenarioArea.Submission,
		Title = "Microsoft Print to PDF leaves finishings and output bins out of AppliedOptions, and a job with staples or an output bin is refused with NotSupportedException naming them, before anything is sent",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = TypedOptionScenarios.WindowsFinishingsRefusedAsync
	};

	/// <summary>
	/// Runs <see cref="IppTypedOptions"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task IppTypedOptionsAsync(ScenarioContext context)
	{
		// ippeveprinter's profile, given each typed option's supported values, so the printer checks every one
		FakePrinterProfile profile = (await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken))
			.Set("print-quality-supported", Tag.Enum, 3, 4, 5)
			.Set("finishings-supported", Tag.Enum, 3, 5, 20)
			.Set("output-bin-supported", Tag.Keyword, "face-down", "face-up")
			.Set("media-type-supported", Tag.Keyword, "stationery", "labels")
			.Set("multiple-document-handling-supported", Tag.Keyword, "separate-documents-uncollated-copies", "separate-documents-collated-copies");

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		context.Check("the capabilities list draft, normal and high quality", capabilities.PrintQualities.SequenceEqual([PrintQuality.Draft, PrintQuality.Normal, PrintQuality.High]), string.Join(", ", capabilities.PrintQualities));
		context.Check("the capabilities list stapling at the top left and punching", capabilities.Finishings.Order().SequenceEqual([Finishing.StapleTopLeft, Finishing.Punch]), string.Join(", ", capabilities.Finishings));
		OutputBin? faceDown = capabilities.OutputBins.FirstOrDefault(bin => bin.Id == "face-down");
		MediaType? labels = capabilities.MediaTypes.FirstOrDefault(type => type.Id == "labels");
		context.Require("the capabilities list the face-down bin and labels", faceDown is not null && labels is not null);
		context.Check("the applied options include every typed option", printer.AppliedOptions.HasFlag(PrintOptionKinds.PageRanges | PrintOptionKinds.Collate | PrintOptionKinds.Quality | PrintOptionKinds.Finishings | PrintOptionKinds.OutputBin | PrintOptionKinds.MediaType), printer.AppliedOptions.ToString());

		byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.ThreePages), context.CancellationToken);
		using MemoryStream document = new MemoryStream(pdf);

		PrintOptions options = new PrintOptions
		{
			Copies = 2,
			PageRanges = [new PageRange(2, 3)],
			Collate = false,
			Quality = PrintQuality.High,
			Finishings = [Finishing.StapleTopLeft, Finishing.Punch],
			OutputBin = faceDown,
			MediaType = labels,
			MediaSize = MediaSize.Custom(102, 152),
		};

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("page-ranges is 2-3", "2-3", printJob.Find("page-ranges")?.Value);
		context.CheckEqual("multiple-document-handling is uncollated", "separate-documents-uncollated-copies", printJob.Find("multiple-document-handling")?.Value);
		context.CheckEqual("print-quality is high (5)", "5", printJob.Find("print-quality")?.Value);
		context.Check("finishings are staple-top-left (20) and punch (5)", printJob.Find("finishings")?.Values.SequenceEqual(["20", "5"]) == true, printJob.Find("finishings")?.ToString());
		context.CheckEqual("output-bin is face-down", "face-down", printJob.Find("output-bin")?.Value);

		string mediaCol = printJob.Find("media-col")?.Value ?? string.Empty;
		context.Check("media-col names labels", mediaCol.Contains("media-type=labels", StringComparison.Ordinal), mediaCol);
		context.Check("media-col gives the custom size as 10200 x 15200", mediaCol.Contains("x-dimension=10200", StringComparison.Ordinal) && mediaCol.Contains("y-dimension=15200", StringComparison.Ordinal), mediaCol);
		context.Check("media isn't sent as well", printJob.Find("media") is null);
		context.Check("the PDF arrives unchanged", printJob.Document.AsSpan().SequenceEqual(pdf), $"{printJob.Document.Length} bytes received, {pdf.Length} sent");

		// Raw data's pages can't be seen, so it can't be given page ranges
		using MemoryStream raw = new MemoryStream(RawData.Create(TypedOptionScenarios.IppTypedOptions.Id));
		NotSupportedException? refused = await TypedOptionScenarios.CatchAsync<NotSupportedException>(
			() => printer.SubmitAsync(PrintContent.FromRaw(raw), new PrintOptions { PageRanges = [new PageRange(1, 1)] }, context.CancellationToken));

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Check("raw data with page ranges is refused with NotSupportedException", refused is not null, refused?.Message);
		context.CheckEqual("no second Print-Job is sent", 1, fake.GetRequests(IppOperation.PrintJob).Count);
	}

	/// <summary>
	/// Runs <see cref="IppRasterPageRanges"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task IppRasterPageRangesAsync(ScenarioContext context)
	{
		// ippeveprinter's profile without PDF, as many printers are
		FakePrinterProfile profile = (await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken))
			.Set("document-format-supported", Tag.MimeMediaType, "application/octet-stream", "image/jpeg", "image/pwg-raster");

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, renderPdfs: true);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { PageRanges = [new PageRange(2, 3)] };

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is image/pwg-raster", "image/pwg-raster", printJob.Find("document-format")?.Value);
		context.Check("no page-ranges is sent, as only the pages chosen are rendered", printJob.Find("page-ranges") is null);

		RasterDocument raster = RasterDocumentReader.Read(IppRasterScenarios.Gunzip(printJob.Document));
		context.CheckEqual("it holds two pages", 2, raster.Pages.Count);

		foreach (RasterPage page in raster.Pages)
		{
			context.CheckEqual("each page counts two pages in the job", 2, page.TotalPages);
			context.Check("each page has something printed on it", page.GetInk() is > 0.001 and < 0.9, $"ink {page.GetInk():P2}");
		}
	}

	/// <summary>
	/// Runs <see cref="IppPdfPasswordSent"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task IppPdfPasswordSentAsync(ScenarioContext context)
	{
		// PWG 5100.13: the longest password the printer takes, in octets. ippeveprinter lists 1023 already
		FakePrinterProfile profile = (await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken))
			.Set("document-password-supported", Tag.Integer, 1023);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		byte[] pdf = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.LineArtWithPassword), context.CancellationToken);
		using MemoryStream document = new MemoryStream(pdf);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document, TestDocuments.LineArtPassword), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is application/pdf", "application/pdf", printJob.Find("document-format")?.Value);
		context.CheckEqual("document-password is the password", TestDocuments.LineArtPassword, printJob.Find("document-password")?.Value);
		context.Check("the PDF arrives unchanged", printJob.Document.AsSpan().SequenceEqual(pdf), $"{printJob.Document.Length} bytes received, {pdf.Length} sent");
	}

	/// <summary>
	/// Runs <see cref="IppPdfPasswordRendered"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task IppPdfPasswordRenderedAsync(ScenarioContext context)
	{
		// ippeveprinter's profile, which takes PDF and PWG raster, without document-password-supported
		FakePrinterProfile profile = (await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken))
			.Remove("document-password-supported");

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, renderPdfs: true);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		using (FileStream document = TestDocuments.Open(TestDocuments.LineArtWithPassword))
		{
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document, TestDocuments.LineArtPassword), PrintOptions.Default, context.CancellationToken);
			PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
			context.CheckEqual("the job completes", PrintJobState.Completed, status.State);
		}

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is image/pwg-raster", "image/pwg-raster", printJob.Find("document-format")?.Value);
		context.Check("no document-password is sent", printJob.Find("document-password") is null);

		RasterDocument raster = RasterDocumentReader.Read(IppRasterScenarios.Gunzip(printJob.Document));
		context.CheckEqual("it holds the PDF's one page", 1, raster.Pages.Count);
		context.Check("the page has the line art on it", raster.Pages[0].GetInk() is > 0.001 and < 0.9, $"ink {raster.Pages[0].GetInk():P2}");

		using FileStream again = TestDocuments.Open(TestDocuments.LineArtWithPassword);
		PrintException? refused = await TypedOptionScenarios.CatchAsync<PrintException>(
			() => printer.SubmitAsync(PrintContent.FromPdf(again, "not-the-password"), PrintOptions.Default, context.CancellationToken));

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Check("a wrong password is refused with PrintException", refused is not null, refused?.Message);
		context.Check("the message doesn't give the password", refused?.Message.Contains("not-the-password", StringComparison.Ordinal) == false, refused?.Message);
		context.CheckEqual("no second Print-Job is sent", 1, fake.GetRequests(IppOperation.PrintJob).Count);
	}

	/// <summary>
	/// Runs <see cref="WindowsPdfPassword"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsPdfPasswordAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);

		PdfSummary summary = await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, TestDocuments.LineArtWithPassword, PrintOptions.Default, "password", TestDocuments.LineArtPassword);
		context.CheckEqual("the PDF's one page comes out", 1, summary.PageCount);
		context.Check("the page has the line art on it", summary.Pages[0].Ink > 0.001, summary.Pages[0].ToString());

		using FileStream document = TestDocuments.Open(TestDocuments.LineArtWithPassword);
		PrintOptions options = new PrintOptions { OutputFile = context.GetEvidencePath("print-to-pdf-wrong-password.pdf") };
		PrintException? refused = await TypedOptionScenarios.CatchAsync<PrintException>(
			() => printer.SubmitAsync(PrintContent.FromPdf(document, "not-the-password"), options, context.CancellationToken));

		context.Check("a wrong password is refused with PrintException", refused is not null, refused?.Message);
		context.Check("the message doesn't give the password", refused?.Message.Contains("not-the-password", StringComparison.Ordinal) == false, refused?.Message);
		context.Check("nothing is written", !File.Exists(options.OutputFile));
	}

	/// <summary>
	/// Runs <see cref="WindowsFinishingsRefused"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsFinishingsRefusedAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.MicrosoftPrintToPdf, context.CancellationToken);
		context.Note("Applied options: " + printer.AppliedOptions);
		context.Check("finishings aren't among the applied options", !printer.AppliedOptions.HasFlag(PrintOptionKinds.Finishings));
		context.Check("output bins aren't among the applied options", !printer.AppliedOptions.HasFlag(PrintOptionKinds.OutputBin));
		context.Check("page ranges and quality are", printer.AppliedOptions.HasFlag(PrintOptionKinds.PageRanges | PrintOptionKinds.Quality));

		string outputFile = context.GetEvidencePath("print-to-pdf-finishings.pdf");
		PrintOptions[] refusedOptions =
		[
			new PrintOptions { Finishings = [Finishing.Staple], OutputFile = outputFile },
			new PrintOptions { OutputBin = new OutputBin { Id = "1", Name = "Top" }, OutputFile = outputFile },
		];

		foreach (PrintOptions options in refusedOptions)
		{
			using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
			NotSupportedException? refused = await TypedOptionScenarios.CatchAsync<NotSupportedException>(
				() => printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken));

			string option = options.Finishings.Count > 0 ? nameof(PrintOptions.Finishings) : nameof(PrintOptions.OutputBin);
			context.Check($"a job setting {option} is refused with NotSupportedException", refused is not null, refused?.Message);
			context.Check($"the message names {option}", refused?.Message.Contains(option, StringComparison.Ordinal) == true, refused?.Message);
		}

		context.Check("nothing is written", !File.Exists(outputFile));
	}

	/// <summary>
	/// Awaits a submission expected to fail, and catches the exception.
	/// </summary>
	/// <typeparam name="TException">The exception expected.</typeparam>
	/// <param name="submit">Submits the job.</param>
	/// <returns>The exception, or <see langword="null"/> if the submission succeeded.</returns>
	private static async Task<TException?> CatchAsync<TException>(Func<Task<IPrintJob>> submit)
		where TException : Exception
	{
		try
		{
			await submit();

			return null;
		}
		catch (TException caught)
		{
			return caught;
		}
	}
}
