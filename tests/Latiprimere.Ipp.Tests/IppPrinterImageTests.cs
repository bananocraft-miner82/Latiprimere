using System.Text;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Ipp.Tests.Documents;
using Latiprimere.Ipp.Tests.Raster;
using Latiprimere.Testing.Raster;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests;

/// <summary>
/// JPEG and PNG images to IPP printers and CUPS (ADR 0047): sent as they are where they can be, otherwise as a
/// one-page PDF, which goes as any PDF does.
/// </summary>
public sealed class IppPrinterImageTests
{
	private static readonly Uri PrinterUri = new Uri("ipp://192.168.1.20:631/ipp/print"); // Printer under test

	private static readonly MediaSize A5 = new MediaSize { Name = "iso_a5_148x210mm", DisplayName = "A5", WidthMillimeters = 148, HeightMillimeters = 210 };

	[Fact]
	public async Task GivenAPrinterTakingJpegWhenAJpegIsSubmittedThenItIsSentAsItIsWithPrintScaling()
	{
		// Arrange
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.HpAttributes());
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, ["application/octet-stream", "image/jpeg", "image/urf"]);
		byte[] jpeg = ImageTestData.QuadrantsJpeg();

		// Act
		await printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream(jpeg)), new PrintOptions { Scaling = ScalingMode.Fill }, TestContext.Current.CancellationToken);

		// Assert — not even the paper is asked for
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Equal("image/jpeg", request.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal(PrintScaling.Fill, request.JobTemplateAttributes?.PrintScaling?.Value);
		Assert.Equal(jpeg, Assert.Single(client.ReceivedDocuments));
		Assert.Equal(0, client.AttributeListRequestCount);
	}

	[Fact]
	public async Task GivenAPrinterTakingPdfWhenAPngIsSubmittedThenItIsSentAsAOnePagePdfOnItsDefaultPaper()
	{
		// Arrange — even a printer listing PNG gets a PDF, as PNG support is uneven (ADR 0047)
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, ["application/pdf", "image/png", "image/jpeg"]);
		byte[] png = PngTestEncoder.Encode(64, 48, 2, 8, ImageTestData.Quadrant);

		// Act
		await printer.SubmitAsync(PrintContent.FromPng(new MemoryStream(png)), new PrintOptions { Scaling = ScalingMode.Fit, Orientation = PageOrientation.Portrait }, TestContext.Current.CancellationToken);

		// Assert — laid out already, so no scaling or orientation is sent with it
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Equal("application/pdf", request.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Null(request.JobTemplateAttributes?.PrintScaling);
		Assert.Null(request.JobTemplateAttributes?.OrientationRequested);
		Assert.StartsWith("%PDF-1.7", Encoding.Latin1.GetString(Assert.Single(client.ReceivedDocuments), 0, 8), StringComparison.Ordinal);

		// A4, the printer's default: 595.2756 points wide
		Assert.Contains("/MediaBox [0 0 595.2756 841.8898]", Encoding.Latin1.GetString(client.ReceivedDocuments[0]), StringComparison.Ordinal);
		Assert.Equal(1, client.AttributeListRequestCount);
	}

	[Fact]
	public async Task GivenAPaperChosenWhenAnImageIsConvertedThenThePdfIsThatPaperAndTheDefaultIsntAskedFor()
	{
		// Arrange
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, ["application/pdf"]);
		PrintOptions options = new PrintOptions { MediaSize = IppPrinterImageTests.A5, Orientation = PageOrientation.Portrait };

		// Act
		await printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream(ImageTestData.QuadrantsJpeg())), options, TestContext.Current.CancellationToken);

		// Assert — the paper is still asked for by name
		Assert.Contains("/MediaBox [0 0 419.5276 595.2756]", Encoding.Latin1.GetString(Assert.Single(client.ReceivedDocuments)), StringComparison.Ordinal);
		Assert.Equal("iso_a5_148x210mm", client.LastPrintJobRequest?.JobTemplateAttributes?.Media?.Value.Value);
		Assert.Equal(0, client.AttributeListRequestCount);
	}

	[Fact]
	public async Task GivenARasterPrinterWhenAJpegIsSubmittedThenItsPdfIsRenderedAsRaster()
	{
		// Arrange — a PWG raster printer that takes neither JPEG nor PDF
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		QuadrantRasterizer rasterizer = new QuadrantRasterizer(pageCount: 1);
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, ["image/pwg-raster"], rasterizer);

		// Act
		await printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream(ImageTestData.QuadrantsJpeg())), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the rasteriser was given the PDF
		Assert.Equal("image/pwg-raster", client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Single(RasterDocumentReader.Read(Assert.Single(client.ReceivedDocuments)).Pages);
		Assert.Single(rasterizer.Opened);
	}

	[Theory]
	[InlineData(DocumentFormat.Png, "image/png")]
	[InlineData(DocumentFormat.Jpeg, "image/jpeg")]
	public async Task GivenAPrinterTakingNeitherPdfNorRasterWhenAnImageIsSubmittedThenNotSupportedAndNothingIsSent(DocumentFormat format, string mimeType)
	{
		// Arrange — raster, but no rasteriser to render it (provided by InlineData above)
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.HpAttributes());
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, ["application/octet-stream", "image/urf"]);
		PrintContent content = format == DocumentFormat.Png
			? PrintContent.FromPng(new MemoryStream(PngTestEncoder.Encode(2, 2, 0, 8, (x, y) => [0])))
			: PrintContent.FromJpeg(new MemoryStream(ImageTestData.QuadrantsJpeg()));

		// Act
		Func<Task> submit = () => printer.SubmitAsync(content, PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains(mimeType, exception.Message, StringComparison.Ordinal);
		Assert.Contains("Latiprimere.Rendering.Pdfium", exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
		Assert.Equal(0, client.AttributeListRequestCount);
	}

	[Fact]
	public async Task GivenAPrinterWhoseDefaultPaperHasNoSizeWhenAnImageIsSubmittedThenNotSupported()
	{
		// Arrange
		List<IppAttribute> attributes = RasterJobPlannerTests.PwgAttributes("sgray_8");
		attributes.RemoveAll(attribute => attribute.Name == "media-default");
		attributes.Add(new IppAttribute(Tag.Keyword, "media-default", "roll_current"));
		FakeIppClient client = IppPrinterImageTests.CreateClient(attributes);
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, ["application/pdf"]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream(ImageTestData.QuadrantsJpeg())), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("roll_current", exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenADamagedImageWhenConvertedThenThrowsPrintExceptionAndNothingIsSent()
	{
		// Arrange
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, ["application/pdf"]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPng(new MemoryStream("%PDF-1.7"u8.ToArray())), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Contains("isn't a PNG", exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Theory]
	[InlineData(DocumentFormat.Png, "image/png")]
	[InlineData(DocumentFormat.Jpeg, "image/jpeg")]
	public async Task GivenCupsWhenAnImageIsSubmittedThenItIsSentAsItIsForCupsToConvert(DocumentFormat format, string mimeType)
	{
		// Arrange — a queue whose formats aren't checked (provided by InlineData above)
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrinterImageTests.PrinterUri, new PrinterDescriptionAttributes());
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterImageTests.PrinterUri, new PrinterInfo { Id = "cups:Office", Name = "Office" });
		byte[] data = [1, 2, 3];
		PrintContent content = format == DocumentFormat.Png ? PrintContent.FromPng(new MemoryStream(data)) : PrintContent.FromJpeg(new MemoryStream(data));

		// Act
		await printer.SubmitAsync(content, new PrintOptions { Scaling = ScalingMode.None }, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(mimeType, client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal(PrintScaling.None, client.LastPrintJobRequest?.JobTemplateAttributes?.PrintScaling?.Value);
		Assert.Equal(data, Assert.Single(client.ReceivedDocuments));
	}

	[Theory]
	[InlineData(DocumentFormat.Jpeg, "image/jpeg")]
	[InlineData(DocumentFormat.Png, "application/pdf")]
	[InlineData(DocumentFormat.Text, "application/pdf")]
	public async Task GivenANetworkPrinterThatDoesntListItsFormatsWhenADocumentIsSubmittedThenJpegGoesAsItIsAndTheRestAsPdf(DocumentFormat format, string expected)
	{
		// Arrange — a printer reached directly that says nothing of its formats: IPP Everywhere and AirPrint printers
		// must take JPEG; anything else is laid out (provided by InlineData above)
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithDocumentFormats(IppDocumentFormats.Standard).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterImageTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, supportedFormats: null);

		PrintContent content = format switch
		{
			DocumentFormat.Jpeg => PrintContent.FromJpeg(new MemoryStream(ImageTestData.QuadrantsJpeg())),
			DocumentFormat.Png  => PrintContent.FromPng(new MemoryStream(PngTestEncoder.Encode(2, 2, 0, 8, (x, y) => [0]))),
			_                   => PrintContent.FromText("Hello"),
		};

		// Act
		await printer.SubmitAsync(content, PrintOptions.Default, TestContext.Current.CancellationToken);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert — and it may be sent anything, as it didn't say
		Assert.Equal(expected, client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal(5, capabilities.DocumentFormats.Count);
	}

	[Theory]
	[InlineData(1, 1)]
	[InlineData(10, 2)]
	public async Task GivenImagesSentOneAfterAnotherWhenTheirPaperIsChosenThenTheDefaultIsKeptForFiveMinutes(int minutesEachReading, int expectedReads)
	{
		// Arrange — every reading of the clock moves it on (provided by InlineData above)
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));

		IppPrintContext context = new IppPrintContextBuilder()
			.WithClient(client)
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.WithTimeProvider(new SteppingTimeProvider(TimeSpan.FromMinutes(minutesEachReading)))
			.Build();

		IppPrinter printer = new IppPrinter(context, IppPrinterImageTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, ["application/pdf"]);

		// Act
		await printer.SubmitAsync(PrintContent.FromText("one"), PrintOptions.Default, TestContext.Current.CancellationToken);
		await printer.SubmitAsync(PrintContent.FromText("two"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(expectedReads, client.AttributeListRequestCount);
		Assert.Equal(2, client.ReceivedDocuments.Count);
	}

	[Fact]
	public async Task GivenAPdfWhenSubmittedThenScalingIsntSent()
	{
		// Arrange — scaling is for images
		FakeIppClient client = IppPrinterImageTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, ["application/pdf"]);

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(new MemoryStream("%PDF-1.7"u8.ToArray())), new PrintOptions { Scaling = ScalingMode.Fill }, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(client.LastPrintJobRequest?.JobTemplateAttributes?.PrintScaling);
	}

	[Theory]
	[InlineData(new[] { "application/pdf", "application/octet-stream" }, false, new[] { DocumentFormat.Pdf, DocumentFormat.Raw, DocumentFormat.Jpeg, DocumentFormat.Png, DocumentFormat.Text })]
	[InlineData(new[] { "image/jpeg", "image/urf" }, false, new[] { DocumentFormat.Jpeg })]
	[InlineData(new[] { "image/jpeg", "image/urf" }, true, new[] { DocumentFormat.Pdf, DocumentFormat.Jpeg, DocumentFormat.Png, DocumentFormat.Text })]
	[InlineData(new[] { "application/vnd.hp-PCL" }, true, new DocumentFormat[0])]
	public async Task GivenAPrintersFormatsWhenCapabilitiesAreReadThenTheyListWhatItCanBeSent(string[] formats, bool rasterizer, DocumentFormat[] expected)
	{
		// Arrange — PDF itself, or as raster with a rasteriser; JPEG itself or as PDF; PNG and text only as PDF
		// (provided by InlineData above)
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrinterImageTests.PrinterUri, new PrinterAttributesBuilder().Build());
		IppPrinter printer = IppPrinterImageTests.CreatePrinter(client, formats, rasterizer ? new QuadrantRasterizer(pageCount: 1) : null);

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(expected, capabilities.DocumentFormats);
	}

	[Fact]
	public async Task GivenCupsWhenCapabilitiesAreReadThenEveryFormatIsListed()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrinterImageTests.PrinterUri, new PrinterAttributesBuilder().Build());
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterImageTests.PrinterUri, new PrinterInfo { Id = "cups:Office", Name = "Office" });

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal([DocumentFormat.Pdf, DocumentFormat.Raw, DocumentFormat.Jpeg, DocumentFormat.Png, DocumentFormat.Text], capabilities.DocumentFormats);
	}

	/// <summary>
	/// Creates a client with a printer whose attributes are given, as its full attribute list.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>The client.</returns>
	private static FakeIppClient CreateClient(IReadOnlyList<IppAttribute> attributes)
	{
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter(IppPrinterImageTests.PrinterUri, new PrinterDescriptionAttributes());
		client.SetAttributeList(IppPrinterImageTests.PrinterUri, attributes);

		return client;
	}

	/// <summary>
	/// Creates the printer under test, as direct IPP does.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="formats">The formats the printer takes.</param>
	/// <param name="rasterizer">The PDF rasteriser, or <see langword="null"/> for none.</param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreatePrinter(FakeIppClient client, string[] formats, QuadrantRasterizer? rasterizer = null)
	{
		IppPrintContextBuilder builder = new IppPrintContextBuilder()
			.WithClient(client)
			.WithDocumentFormats(IppDocumentFormats.Standard);

		if (rasterizer is not null)
		{
			builder.WithPdfRasterizer(rasterizer);
		}

		return new IppPrinter(builder.Build(), IppPrinterImageTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, formats);
	}
}
