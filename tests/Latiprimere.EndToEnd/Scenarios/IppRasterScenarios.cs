using System.IO.Compression;
using System.Text;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Ipp;
using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Testing.Raster;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// PDFs to printers that can't take PDF, sent as PWG raster, Apple raster or PCLm and read back page by page on the
/// scripted printer (ADR 0035, ADR 0046).
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
	/// A PDF to a printer with the HP ENVY 5640's attributes, less Apple raster, arrives as PCLm.
	/// </summary>
	public static readonly Scenario PclmPages = new Scenario
	{
		Id = "ipp.raster.pclm-pages",
		Area = ScenarioArea.Rendering,
		Title = "A three-page PDF to the HP ENVY 5640's profile without Apple raster arrives gzipped as PCLm: a PDF marked PCLm 1.0 whose three US Letter pages at 600 dpi are each 207 colour strips of up to 32 lines, run-length encoded as the printer prefers, which PDFium reads back with ink on every page; and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = IppRasterScenarios.PclmPagesAsync
	};

	/// <summary>
	/// The application's raster format order decides between formats the printer takes.
	/// </summary>
	public static readonly Scenario RasterFormatOrder = new Scenario
	{
		Id = "ipp.raster.format-order",
		Area = ScenarioArea.Rendering,
		Title = "To the HP ENVY 5640's profile, which takes Apple raster and PCLm, a PDF goes as Apple raster by default, as PCLm when IppNetworkOptions.RasterFormats puts PCLm first, and is refused with NotSupportedException, sending nothing, when the list is empty",
		Needs = TargetNeeds.None,
		RunAsync = IppRasterScenarios.RasterFormatOrderAsync
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
	/// Runs <see cref="PclmPages"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PclmPagesAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = (await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken))
			.Set("document-format-supported", Tag.MimeMediaType, "application/octet-stream", "image/jpeg", "application/PCLm");

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, renderPdfs: true);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), new PrintOptions { ColorMode = ColorMode.Color }, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is application/PCLm", "application/PCLm", printJob.Find("document-format")?.Value);
		context.CheckEqual("compression is gzip", "gzip", printJob.Find("compression")?.Value);

		// US Letter at 600 dpi is 5100 by 6600: 206 strips of 32 lines and one of 8 a page
		byte[] pclm = IppRasterScenarios.Gunzip(printJob.Document);
		string text = Encoding.Latin1.GetString(pclm);
		context.Note($"{printJob.Document.Length:N0} bytes received, {pclm.Length:N0} after gzip");
		context.Check("it is a PDF marked PCLm 1.0 on its second line", text.StartsWith("%PDF-1.7\n%PCLm 1.0\n", StringComparison.Ordinal));
		context.CheckEqual("it holds 3 × 207 strips", 3 * 207, IppRasterScenarios.Count(text, "/Subtype /Image "));
		context.CheckEqual("206 a page are 32 lines of 5100 colour pixels", 3 * 206, IppRasterScenarios.Count(text, "/Width 5100 /Height 32 /ColorSpace /DeviceRGB "));
		context.CheckEqual("each page's last is 8 lines", 3, IppRasterScenarios.Count(text, "/Width 5100 /Height 8 /ColorSpace /DeviceRGB "));
		context.CheckEqual("every strip is run-length encoded, which the printer prefers", 3 * 207, IppRasterScenarios.Count(text, "/Filter /RunLengthDecode "));

		using IRasterDocument rendered = new PdfiumRasterizer().Open(new MemoryStream(pclm));
		context.CheckEqual("PDFium reads three pages", 3, rendered.PageCount);

		for (int pageIndex = 0; pageIndex < rendered.PageCount; pageIndex++)
		{
			PageSize size = rendered.GetPageSize(pageIndex);
			context.CheckEqual("each page is US Letter, 612 by 792 points", (612.0, 792.0), (Math.Round(size.WidthPoints, 2), Math.Round(size.HeightPoints, 2)));

			double ink = IppRasterScenarios.MeasureInk(rendered, pageIndex);
			context.Check("each page has something printed on it", ink is > 0.001 and < 0.9, $"ink {ink:P2}");
		}
	}

	/// <summary>
	/// Runs <see cref="RasterFormatOrder"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task RasterFormatOrderAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken);
		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });

		string? byDefault = await IppRasterScenarios.SubmitAsync(context, fake, formats => { });
		context.CheckEqual("by default it goes as Apple raster", "image/urf", byDefault);

		string? pclmFirst = await IppRasterScenarios.SubmitAsync(context, fake, formats =>
		{
			formats.Clear();
			formats.Add(IppRasterFormat.Pclm);
			formats.Add(IppRasterFormat.AppleRaster);
		});

		context.CheckEqual("with PCLm first it goes as PCLm", "application/PCLm", pclmFirst);

		string? none = await IppRasterScenarios.SubmitAsync(context, fake, formats => formats.Clear());
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("with none it is refused, saying why", "refused: IppNetworkOptions.RasterFormats", none);
		context.CheckEqual("only the two accepted jobs are sent", 2, fake.GetRequests(IppOperation.PrintJob).Count);
	}

	/// <summary>
	/// Prints a one-page PDF at up to 150 dpi through a new catalog whose raster formats are set as given.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <param name="fake">The printer.</param>
	/// <param name="setFormats">Sets <see cref="IppNetworkOptions.RasterFormats"/>.</param>
	/// <returns>The format it was sent in; or, if submitting threw <see cref="NotSupportedException"/>, "refused", with
	/// the option its message names.</returns>
	private static async Task<string?> SubmitAsync(ScenarioContext context, FakePrinter fake, Action<IList<IppRasterFormat>> setFormats)
	{
		await using CatalogFixture fixture = FakePrinters.CreateCatalog
		(
			fake.PrinterUri,
			options =>
			{
				options.MaxRenderDpi = 150;
				setFormats(options.RasterFormats);
			},
			renderPdfs: true
		);

		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.MixedOnePage);

		try
		{
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
			await JobFollower.WaitUntilFinishedAsync(context, job);
		}
		catch (NotSupportedException exception)
		{
			context.Note("Refused: " + exception.Message);

			return exception.Message.Contains("IppNetworkOptions.RasterFormats", StringComparison.Ordinal) ? "refused: IppNetworkOptions.RasterFormats" : "refused";
		}

		return fake.GetRequests(IppOperation.PrintJob)[^1].Find("document-format")?.Value;
	}

	/// <summary>
	/// Renders a page at 50 dpi and measures how much of it isn't white.
	/// </summary>
	/// <param name="document">The document.</param>
	/// <param name="pageIndex">The page.</param>
	/// <returns>The share of pixels with a component below 250.</returns>
	private static double MeasureInk(IRasterDocument document, int pageIndex)
	{
		PageSize size = document.GetPageSize(pageIndex);
		int width = (int)(size.WidthPoints * 50 / 72);
		int height = (int)(size.HeightPoints * 50 / 72);
		RasterBand band = RasterBand.Create(width, height, 0, height);
		byte[] pixels = new byte[band.ByteCount];
		document.RenderBand(pageIndex, band, pixels);

		int inked = 0;

		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				int index = (y * band.Stride) + (x * RasterBand.GetBytesPerPixel(RasterPixelFormat.Bgra32));
				inked += pixels[index] < 250 || pixels[index + 1] < 250 || pixels[index + 2] < 250 ? 1 : 0;
			}
		}

		return (double)inked / (width * height);
	}

	/// <summary>
	/// Counts a text's occurrences.
	/// </summary>
	/// <param name="text">Where to look.</param>
	/// <param name="value">What to count.</param>
	/// <returns>How often it occurs.</returns>
	private static int Count(string text, string value)
	{
		int count = 0;
		int index = text.IndexOf(value, StringComparison.Ordinal);

		while (index >= 0)
		{
			count++;
			index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
		}

		return count;
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
	internal static byte[] Gunzip(byte[] gzipped)
	{
		using GZipStream gzip = new GZipStream(new MemoryStream(gzipped), CompressionMode.Decompress);
		using MemoryStream output = new MemoryStream();
		gzip.CopyTo(output);

		return output.ToArray();
	}
}
