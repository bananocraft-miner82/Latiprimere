using System.IO.Compression;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Testing.Raster;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// PDFs to printers that can't take PDF, sent as PWG raster or Apple raster and read back page by page on the
/// scripted printer (ADR 0035).
/// </summary>
public static class IppRasterScenarios
{
	/// <summary>
	/// A PDF to a printer with ippeveprinter's attributes, less PDF, arrives as PWG raster.
	/// </summary>
	public static readonly Scenario PwgRasterPages = new Scenario
	{
		Id = "ipp.raster.pwg-pages",
		Area = ScenarioArea.Rendering,
		Title = "A three-page PDF to a printer that takes PWG raster but not PDF arrives gzipped as three grey PWG raster pages at the printer's default 600 dpi on its default paper, and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = IppRasterScenarios.PwgRasterPagesAsync
	};

	/// <summary>
	/// A PDF to a printer with the HP ENVY 5640's attributes arrives as Apple raster, two-sided and in colour.
	/// </summary>
	public static readonly Scenario AppleRasterTwoSided = new Scenario
	{
		Id = "ipp.raster.apple-raster-two-sided",
		Area = ScenarioArea.Rendering,
		Title = "A three-page PDF to the HP ENVY 5640's profile, in colour, two-sided on A4, arrives gzipped as Apple raster: three 24-bit pages at 600 dpi marked long-edge, and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = IppRasterScenarios.AppleRasterTwoSidedAsync
	};

	/// <summary>
	/// A raster job turned away as busy is rendered again and sent whole.
	/// </summary>
	public static readonly Scenario BusyRendersAgain = new Scenario
	{
		Id = "ipp.raster.busy-renders-again",
		Area = ScenarioArea.Rendering,
		Title = "A raster job turned away twice as busy is rendered again each time, and every attempt carries the same whole raster document",
		Needs = TargetNeeds.None,
		RunAsync = IppRasterScenarios.BusyRendersAgainAsync
	};

	/// <summary>
	/// Without a PDF rasteriser, a PDF to a printer that can't take PDF is refused before anything is sent.
	/// </summary>
	public static readonly Scenario NoRasterizerRefused = new Scenario
	{
		Id = "ipp.raster.no-rasterizer-refused",
		Area = ScenarioArea.Rendering,
		Title = "Without Latiprimere.Rendering.Pdfium registered, a PDF to a printer that can't take PDF is refused with NotSupportedException naming it, and no job is sent",
		Needs = TargetNeeds.None,
		RunAsync = IppRasterScenarios.NoRasterizerRefusedAsync
	};

	/// <summary>
	/// Runs <see cref="PwgRasterPages"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PwgRasterPagesAsync(ScenarioContext context)
	{
		// ippeveprinter's profile without PDF, as many printers are
		FakePrinterProfile profile = (await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken))
			.Set("document-format-supported", Tag.MimeMediaType, "application/octet-stream", "image/jpeg", "image/pwg-raster");

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, renderPdfs: true);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is image/pwg-raster", "image/pwg-raster", printJob.Find("document-format")?.Value);
		context.CheckEqual("compression is gzip", "gzip", printJob.Find("compression")?.Value);

		RasterDocument raster = RasterDocumentReader.Read(IppRasterScenarios.Gunzip(printJob.Document));
		context.Note($"{printJob.Document.Length:N0} bytes received for {raster.Pages.Count} pages");
		context.CheckEqual("it holds three pages", 3, raster.Pages.Count);

		foreach (RasterPage page in raster.Pages)
		{
			context.CheckEqual("each page is US Letter at 600 dpi", (5100, 6600, 600), (page.Width, page.Height, page.DpiX));
			context.CheckEqual("each page is 8-bit sGray (colour space 18)", (8, 18), (page.BitsPerPixel, page.ColorSpace));
			context.CheckEqual("each page names its paper", "na_letter_8.5x11in", page.PageSizeName);
			context.CheckEqual("each page knows the page count", 3, page.TotalPages);
			context.Check("each page has something printed on it", page.GetInk() is > 0.001 and < 0.9, $"ink {page.GetInk():P2}");
		}
	}

	/// <summary>
	/// Runs <see cref="AppleRasterTwoSided"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task AppleRasterTwoSidedAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, renderPdfs: true);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		MediaSize? a4 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a4_210x297mm");
		context.Require("the capabilities include A4", a4 is not null);

		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { ColorMode = ColorMode.Color, Duplex = DuplexMode.LongEdge, MediaSize = a4 };

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is image/urf", "image/urf", printJob.Find("document-format")?.Value);
		context.CheckEqual("compression is gzip", "gzip", printJob.Find("compression")?.Value);
		context.CheckEqual("sides is two-sided-long-edge", "two-sided-long-edge", printJob.Find("sides")?.Value);
		context.Check("no orientation-requested is sent, as the pages are already laid out", printJob.Find("orientation-requested") is null);

		RasterDocument raster = RasterDocumentReader.Read(IppRasterScenarios.Gunzip(printJob.Document));
		context.Note($"{printJob.Document.Length:N0} bytes received for {raster.Pages.Count} pages");
		context.CheckEqual("the header counts three pages", 3, raster.PageCount);
		context.CheckEqual("it holds three pages", 3, raster.Pages.Count);

		foreach (RasterPage page in raster.Pages)
		{
			context.CheckEqual("each page is A4 at 600 dpi, sized as libcups sizes it", (4960, 7015, 600), (page.Width, page.Height, page.DpiX));
			context.CheckEqual("each page is 24-bit sRGB (colour space 1)", (24, 1), (page.BitsPerPixel, page.ColorSpace));
			context.CheckEqual("each page is marked two-sided on the long edge (3)", 3, page.AppleDuplexMode);
			context.Check("each page has something printed on it", page.GetInk() is > 0.001 and < 0.9, $"ink {page.GetInk():P2}");
		}
	}

	/// <summary>
	/// Runs <see cref="BusyRendersAgain"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task BusyRendersAgainAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = (await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken))
			.Set("document-format-supported", Tag.MimeMediaType, "image/pwg-raster");

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		fake.Behaviour.BusyPrintJobs = 2;
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, options => options.MaxRenderDpi = 300, renderPdfs: true);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		IReadOnlyList<RecordedRequest> attempts = fake.GetRequests(IppOperation.PrintJob);
		context.CheckEqual("the job is sent three times", 3, attempts.Count);
		context.Check("the first two are turned away as busy", attempts.Take(2).All(attempt => attempt.StatusCode == IppStatusCode.ServerErrorBusy));

		byte[] last = IppRasterScenarios.Gunzip(attempts[^1].Document);
		context.Check("every attempt carries the same raster", attempts.All(attempt => IppRasterScenarios.Gunzip(attempt.Document).AsSpan().SequenceEqual(last)));

		RasterDocument raster = RasterDocumentReader.Read(last);
		context.CheckEqual("which is three pages at 300 dpi, the limit set", (3, 300), (raster.Pages.Count, raster.Pages[0].DpiX));
	}

	/// <summary>
	/// Runs <see cref="NoRasterizerRefused"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task NoRasterizerRefusedAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		NotSupportedException? exception = null;

		try
		{
			await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		}
		catch (NotSupportedException caught)
		{
			exception = caught;
		}

		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Require("submitting throws NotSupportedException", exception is not null);
		context.Note("Message: " + exception!.Message);
		context.Check("the message names Latiprimere.Rendering.Pdfium", exception.Message.Contains("Latiprimere.Rendering.Pdfium", StringComparison.Ordinal));
		context.CheckEqual("no Print-Job is sent", 0, fake.GetRequests(IppOperation.PrintJob).Count);
		context.CheckEqual("no Validate-Job is sent", 0, fake.GetRequests(IppOperation.ValidateJob).Count);
	}

	/// <summary>
	/// Decompresses a gzipped upload.
	/// </summary>
	/// <param name="gzipped">The upload.</param>
	/// <returns>The data.</returns>
	private static byte[] Gunzip(byte[] gzipped)
	{
		using GZipStream gzip = new GZipStream(new MemoryStream(gzipped), CompressionMode.Decompress);
		using MemoryStream output = new MemoryStream();
		gzip.CopyTo(output);

		return output.ToArray();
	}
}
