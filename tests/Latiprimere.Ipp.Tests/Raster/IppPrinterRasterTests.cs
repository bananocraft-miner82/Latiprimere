using System.IO.Compression;
using Latiprimere.Rendering;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Testing.Raster;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Raster;

/// <summary>
/// PDFs to printers that can't take PDF, sent as raster (ADR 0035).
/// </summary>
public sealed class IppPrinterRasterTests
{
	private static readonly Uri PrinterUri = new Uri("ipp://192.168.1.20:631/ipp/print"); // Printer under test

	[Fact]
	public async Task GivenAPwgRasterPrinterWhenAPdfIsSubmittedThenItsPagesAreSentAsPwgRaster()
	{
		// Arrange
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		QuadrantRasterizer rasterizer = new QuadrantRasterizer(pageCount: 3);
		IppPrinter printer = IppPrinterRasterTests.CreatePrinter(client, rasterizer, ["image/pwg-raster", "application/octet-stream"]);
		PrintOptions options = new PrintOptions { Copies = 2, Orientation = PageOrientation.Landscape };
		// Act
		await printer.SubmitAsync(IppPrinterRasterTests.Pdf(), options, TestContext.Current.CancellationToken);

		// Assert
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Equal("image/pwg-raster", request.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Null(request.OperationAttributes?.Compression);
		Assert.Equal(2, request.JobTemplateAttributes?.Copies?.Value);
		Assert.Null(request.JobTemplateAttributes?.OrientationRequested);
		RasterDocument raster = RasterDocumentReader.Read(Assert.Single(client.ReceivedDocuments));
		Assert.Equal("image/pwg-raster", raster.Format);
		Assert.Equal(3, raster.Pages.Count);
		Assert.All(raster.Pages, page => Assert.Equal((2480, 3507, 300), (page.Width, page.Height, page.DpiX)));

		// Landscape on portrait paper: turned a quarter
		QuadrantDocument document = Assert.Single(rasterizer.Opened);
		Assert.All(document.Bands, band => Assert.Equal(RasterRotation.Anticlockwise90, band.Rotation));
		Assert.True(document.IsDisposed);
	}

	[Fact]
	public async Task GivenAnAppleRasterPrinterOfferingGzipWhenAPdfIsSubmittedThenTheRasterIsGzipped()
	{
		// Arrange
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.HpAttributes());

		IppPrinter printer = IppPrinterRasterTests.CreatePrinter
		(
			client,
			new QuadrantRasterizer(pageCount: 2),
			["application/octet-stream", "image/urf", "image/jpeg"],
			maxRenderDpi: 300
		);

		// Act
		await printer.SubmitAsync(IppPrinterRasterTests.Pdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Equal("image/urf", request.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal(Compression.Gzip, request.OperationAttributes?.Compression?.Value);
		RasterDocument raster = RasterDocumentReader.Read(IppPrinterRasterTests.Gunzip(Assert.Single(client.ReceivedDocuments)));
		Assert.Equal("image/urf", raster.Format);
		Assert.Equal(2, raster.Pages.Count);
		Assert.All(raster.Pages, page => Assert.Equal((24, 300), (page.BitsPerPixel, page.DpiX)));
	}
	[Fact]
	public async Task GivenValidateJobWhenAPdfIsSentAsRasterThenTheRasterFormatIsValidated()
	{
		// Arrange
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));

		IppPrintContext context = new IppPrintContextBuilder()
			.WithClient(client)
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.WithPdfRasterizer(new QuadrantRasterizer(pageCount: 1))
			.WithValidateJobs(true)
			.Build();

		IppPrinter printer = new IppPrinter
		(
			context,
			IppPrinterRasterTests.PrinterUri,
			new PrinterInfo { Id = "ipp:x", Name = "x" },
			["image/pwg-raster"],
			[IppOperation.PrintJob, IppOperation.ValidateJob]
		);

		// Act
		await printer.SubmitAsync(IppPrinterRasterTests.Pdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		ValidateJobRequest validation = Assert.Single(client.ValidateJobRequests);
		Assert.Equal("image/pwg-raster", validation.OperationAttributes?.DocumentFormat?.Value.Value);
	}

	[Fact]
	public async Task GivenABusyPrinterWhenARasterJobIsResentThenThePagesAreRenderedAgainFromTheStart()
	{
		// Arrange
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		client.BusyPrintJobCount = 1;

		IppPrinter printer = IppPrinterRasterTests.CreatePrinter
		(
			client,
			new QuadrantRasterizer(pageCount: 2),
			["image/pwg-raster"],
			busyRetryDuration: TimeSpan.FromSeconds(5)
		);

		// Act
		await printer.SubmitAsync(IppPrinterRasterTests.Pdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the same whole document both times
		Assert.Equal(2, client.ReceivedDocuments.Count);
		Assert.Equal(client.ReceivedDocuments[0], client.ReceivedDocuments[1]);
		Assert.Equal(2, RasterDocumentReader.Read(client.ReceivedDocuments[1]).Pages.Count);
	}
	[Fact]
	public async Task GivenRenderingFailsWhenARasterJobIsSentThenThatFailureIsThrown()
	{
		// Arrange
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		QuadrantRasterizer rasterizer = new QuadrantRasterizer(pageCount: 1) { RenderFailure = new InvalidOperationException("Page 1 is damaged.") };
		IppPrinter printer = IppPrinterRasterTests.CreatePrinter(client, rasterizer, ["image/pwg-raster"]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(IppPrinterRasterTests.Pdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(submit);
		Assert.Equal("Page 1 is damaged.", exception.Message);
		Assert.True(Assert.Single(rasterizer.Opened).IsDisposed);
	}

	[Fact]
	public async Task GivenNoRasterizerWhenAPdfIsSubmittedToARasterPrinterThenNotSupportedNamesPdfium()
	{
		// Arrange
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithDocumentFormats(IppDocumentFormats.Standard).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterRasterTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, ["image/pwg-raster"]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(IppPrinterRasterTests.Pdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — nothing sent, not even the raster attributes request
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("Latiprimere.Rendering.Pdfium", exception.Message, StringComparison.Ordinal);
		Assert.Equal(0, client.AttributeListRequestCount);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenAPrinterTakingNeitherPdfNorRasterWhenAPdfIsSubmittedThenNotSupported()
	{
		// Arrange
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterRasterTests.CreatePrinter(client, new QuadrantRasterizer(pageCount: 1), ["application/octet-stream", "image/jpeg"]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(IppPrinterRasterTests.Pdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("application/pdf", exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenAPrinterTakingPdfWhenAPdfIsSubmittedThenItIsSentAsItIsEvenWithARasterizer()
	{
		// Arrange
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		QuadrantRasterizer rasterizer = new QuadrantRasterizer(pageCount: 1);
		IppPrinter printer = IppPrinterRasterTests.CreatePrinter(client, rasterizer, ["application/pdf", "image/pwg-raster"]);

		// Act
		await printer.SubmitAsync(IppPrinterRasterTests.Pdf(), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("application/pdf", client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Empty(rasterizer.Opened);
		Assert.Equal(0, client.AttributeListRequestCount);
	}
	[Fact]
	public async Task GivenRawDataWhenSubmittedToARasterPrinterWithoutOctetStreamThenNotSupported()
	{
		// Arrange — raw data is never rendered
		FakeIppClient client = IppPrinterRasterTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterRasterTests.CreatePrinter(client, new QuadrantRasterizer(pageCount: 1), ["image/pwg-raster"]);
		using MemoryStream raw = new MemoryStream([0x1B, 0x45]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(raw), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<NotSupportedException>(submit);
	}

	/// <summary>
	/// Creates a client with a printer whose raster attributes are given.
	/// </summary>
	/// <param name="attributes">The raster attributes.</param>
	/// <returns>The client.</returns>
	private static FakeIppClient CreateClient(IReadOnlyList<IppAttribute> attributes)
	{
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter(IppPrinterRasterTests.PrinterUri, new PrinterDescriptionAttributes());
		client.SetAttributeList(IppPrinterRasterTests.PrinterUri, attributes);

		return client;
	}

	/// <summary>
	/// Creates the printer under test, as direct IPP does.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="rasterizer">The PDF rasteriser.</param>
	/// <param name="formats">The formats the printer takes.</param>
	/// <param name="maxRenderDpi">The raster resolution limit, or <see langword="null"/>.</param>
	/// <param name="busyRetryDuration">How long to resend a job the printer is too busy for.</param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreatePrinter
	(
		FakeIppClient      client,
		QuadrantRasterizer rasterizer,
		string[]           formats,
		int?               maxRenderDpi = null,
		TimeSpan           busyRetryDuration = default
	)
	{
		IppPrintContextBuilder builder = new IppPrintContextBuilder()
			.WithClient(client)
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.WithPdfRasterizer(rasterizer)
			.WithBusyRetryDuration(busyRetryDuration);

		if (maxRenderDpi is not null)
		{
			builder.WithMaxRenderDpi(maxRenderDpi.Value);
		}

		return new IppPrinter(builder.Build(), IppPrinterRasterTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, formats);
	}

	/// <summary>
	/// A stand-in PDF, which the quadrant rasteriser doesn't read.
	/// </summary>
	/// <returns>The content.</returns>
	private static PrintContent Pdf()
	{
		return PrintContent.FromPdf(new MemoryStream("%PDF-1.7"u8.ToArray()));
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
