using System.IO.Compression;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Testing.Raster;
using SharpIpp.Protocol.Models;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// JPEG and PNG images (ADR 0047): sent as they are to printers and CUPS that take them, otherwise placed on a page
/// as a PDF, which goes as any PDF does.
/// </summary>
public static class ImageScenarios
{
	/// <summary>
	/// A JPEG to a printer that takes JPEG arrives as it is, with the scaling asked for.
	/// </summary>
	public static readonly Scenario JpegAsItIs = new Scenario
	{
		Id = "ipp.images.jpeg-as-is",
		Area = ScenarioArea.Submission,
		Title = "A JPEG to the HP ENVY 5640's profile, which takes JPEG, arrives byte for byte as image/jpeg with print-scaling fit, and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = ImageScenarios.JpegAsItIsAsync
	};

	/// <summary>
	/// A PNG to a printer that takes PDF arrives as a one-page PDF on its default paper.
	/// </summary>
	public static readonly Scenario PngAsPdf = new Scenario
	{
		Id = "ipp.images.png-as-pdf",
		Area = ScenarioArea.Rendering,
		Title = "A wide PNG with a transparent disc to a printer with ippeveprinter's profile arrives as a one-page PDF: landscape US Letter, the printer's default paper, in colour, and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = ImageScenarios.PngAsPdfAsync
	};

	/// <summary>
	/// A PNG to a printer that takes neither PNG nor PDF arrives as raster, from the PDF it became.
	/// </summary>
	public static readonly Scenario PngAsRaster = new Scenario
	{
		Id = "ipp.images.png-as-raster",
		Area = ScenarioArea.Rendering,
		Title = "A PNG to the HP ENVY 5640's profile, which takes neither PNG nor PDF, arrives gzipped as one page of Apple raster at 600 dpi on US Letter, with colour on it, and the job completes",
		Needs = TargetNeeds.None,
		RunAsync = ImageScenarios.PngAsRasterAsync
	};

	/// <summary>
	/// A PNG and a JPEG printed to Microsoft Print to PDF, through a PDF of their own, PDFium and GDI.
	/// </summary>
	public static readonly Scenario MicrosoftPrintToPdf = new Scenario
	{
		Id = "windows.images.print-to-pdf",
		Area = ScenarioArea.Rendering,
		Title = "A wide PNG and JPEG, each fitted to the page, printed to Microsoft Print to PDF, each write a PDF of one landscape page in colour",
		Needs = TargetNeeds.MicrosoftPrintToPdf,
		RunAsync = ImageScenarios.MicrosoftPrintToPdfAsync
	};

	/// <summary>
	/// A PNG and a JPEG sent to CUPS as they are, which CUPS converts and CUPS-PDF writes.
	/// </summary>
	public static readonly Scenario CupsPdf = new Scenario
	{
		Id = "cups.images.cups-pdf-output",
		Area = ScenarioArea.Submission,
		Title = "A PNG and a JPEG sent to CUPS-PDF as they are, fitted to the page, each complete, and CUPS-PDF writes a PDF of one page in colour",
		Needs = TargetNeeds.CupsServer | TargetNeeds.CupsOutput | TargetNeeds.CupsContainer,
		RunAsync = ImageScenarios.CupsPdfAsync
	};

	/// <summary>
	/// Runs <see cref="JpegAsItIs"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task JpegAsItIsAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(context.CancellationToken);
		context.Check("the capabilities list JPEG", capabilities.DocumentFormats.Contains(DocumentFormat.Jpeg), string.Join(", ", capabilities.DocumentFormats));

		byte[] jpeg = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", TestDocuments.PhotoJpeg), context.CancellationToken);
		using MemoryStream document = new MemoryStream(jpeg);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromJpeg(document), new PrintOptions { Scaling = ScalingMode.Fit }, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is image/jpeg", "image/jpeg", printJob.Find("document-format")?.Value);
		context.CheckEqual("print-scaling is fit", "fit", printJob.Find("print-scaling")?.Value);
		context.Check("the JPEG arrives byte for byte", printJob.Document.AsSpan().SequenceEqual(jpeg), $"{printJob.Document.Length:N0} bytes received, {jpeg.Length:N0} sent");
	}

	/// <summary>
	/// Runs <see cref="PngAsPdf"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PngAsPdfAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.PhotoPng);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPng(document), new PrintOptions { Scaling = ScalingMode.Fit }, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is application/pdf", "application/pdf", printJob.Find("document-format")?.Value);
		context.Check("no print-scaling is sent, as the page is laid out already", printJob.Find("print-scaling") is null);
		await context.SaveEvidenceAsync("received.pdf", printJob.Document, "The PDF the printer received");

		PdfSummary summary = PdfInspector.Inspect(printJob.Document);

		foreach (string line in summary.Describe())
		{
			context.Note(line);
		}

		context.RequireEqual("it is one page", 1, summary.PageCount);
		PdfPage page = summary.Pages[0];
		context.Check("the page is landscape US Letter", page.IsLandscape && page.IsPaper(279.4, 215.9), page.ToString());
		context.Check("the page has colour on it", page.Colour > 0.2, page.ToString());
	}

	/// <summary>
	/// Runs <see cref="PngAsRaster"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task PngAsRasterAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadBuiltInAsync(FakePrinterProfile.HpEnvy5640, context.CancellationToken);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri, renderPdfs: true);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.PhotoPng);

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPng(document), new PrintOptions { Scaling = ScalingMode.Fit, ColorMode = ColorMode.Color }, context.CancellationToken);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("the job completes", PrintJobState.Completed, status.State);

		RecordedRequest printJob = fake.GetRequests(IppOperation.PrintJob).Single();
		context.CheckEqual("document-format is image/urf", "image/urf", printJob.Find("document-format")?.Value);
		context.CheckEqual("compression is gzip", "gzip", printJob.Find("compression")?.Value);

		RasterDocument raster = RasterDocumentReader.Read(ImageScenarios.Gunzip(printJob.Document));
		context.Note($"{printJob.Document.Length:N0} bytes received for {raster.Pages.Count} pages");
		context.RequireEqual("it holds one page", 1, raster.Pages.Count);

		RasterPage page = raster.Pages[0];
		context.CheckEqual("the page is US Letter at 600 dpi in 24-bit colour", (5100, 6600, 600, 24), (page.Width, page.Height, page.DpiX, page.BitsPerPixel));
		context.Check("the page has the image on it", page.GetInk() is > 0.2 and < 0.95, $"ink {page.GetInk():P2}");
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
		PrintOptions options = new PrintOptions { Scaling = ScalingMode.Fit, ColorMode = ColorMode.Color };

		foreach ((string documentName, string label) in new[] { (TestDocuments.PhotoPng, "png"), (TestDocuments.PhotoJpeg, "jpeg") })
		{
			PdfSummary summary = await PdfPrinters.PrintToMicrosoftPdfAsync(context, printer, documentName, options, label);
			context.RequireEqual($"the {label} job writes one page", 1, summary.PageCount);
			context.Check($"the {label} page is landscape", summary.Pages[0].IsLandscape, summary.Pages[0].ToString());
			context.Check($"the {label} page has colour on it", summary.Pages[0].Colour > 0.2, summary.Pages[0].ToString());
		}
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
		PrintOptions options = new PrintOptions { Scaling = ScalingMode.Fit, ColorMode = ColorMode.Color };

		foreach ((string documentName, string label) in new[] { (TestDocuments.PhotoPng, "png"), (TestDocuments.PhotoJpeg, "jpeg") })
		{
			PdfSummary summary = await PdfPrinters.PrintToCupsPdfAsync(context, printer, documentName, options, label);
			context.RequireEqual($"the {label} job writes one page", 1, summary.PageCount);
			context.Check($"the {label} page has colour on it", summary.Pages[0].Colour > 0.05, summary.Pages[0].ToString());
		}
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
