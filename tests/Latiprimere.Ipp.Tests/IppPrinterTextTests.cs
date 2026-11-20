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
/// Plain text to IPP printers and CUPS (ADR 0048): CUPS lays it out itself; a network printer is sent a PDF that
/// Latiprimere lays out, which goes as any PDF does.
/// </summary>
public sealed class IppPrinterTextTests
{
	private static readonly Uri PrinterUri = new Uri("ipp://192.168.1.20:631/ipp/print"); // Printer under test

	[Fact]
	public async Task GivenAPrinterTakingPdfAndTextWhenTextIsSubmittedThenItIsSentAsAPdfLaidOutOnItsDefaultPaper()
	{
		// Arrange — even a printer listing text/plain gets a PDF, so the layout is the same everywhere
		FakeIppClient client = IppPrinterTextTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterTextTests.CreatePrinter(client, ["application/pdf", "text/plain"]);

		// Act
		await printer.SubmitAsync(PrintContent.FromText("Total\t£5\nThanks"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — A4, the printer's default
		PrintJobRequest request = Assert.IsType<PrintJobRequest>(client.LastPrintJobRequest);
		Assert.Equal("application/pdf", request.OperationAttributes?.DocumentFormat?.Value.Value);
		byte[] pdf = Assert.Single(client.ReceivedDocuments);
		Assert.Contains("/MediaBox [0 0 595.2756 841.8898]", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
		Assert.Equal(["Total   £5", "Thanks"], Assert.Single(PdfTextLines.Read(pdf)).Select(line => line.Text));
		Assert.Equal(1, client.AttributeListRequestCount);
	}

	[Fact]
	public async Task GivenLandscapeWhenTextIsSubmittedThenItsPagesAreLandscapeAndNoOrientationIsSent()
	{
		// Arrange
		FakeIppClient client = IppPrinterTextTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterTextTests.CreatePrinter(client, ["application/pdf"]);

		// Act
		await printer.SubmitAsync(PrintContent.FromText("wide"), new PrintOptions { Orientation = PageOrientation.Landscape }, TestContext.Current.CancellationToken);

		// Assert — laid out already, so the printer turns it only to fit
		Assert.Contains("/MediaBox [0 0 841.8898 595.2756]", Encoding.Latin1.GetString(Assert.Single(client.ReceivedDocuments)), StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest?.JobTemplateAttributes?.OrientationRequested);
	}

	[Fact]
	public async Task GivenARasterPrinterWhenTextIsSubmittedThenItsPdfIsRenderedAsRaster()
	{
		// Arrange
		FakeIppClient client = IppPrinterTextTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		QuadrantRasterizer rasterizer = new QuadrantRasterizer(pageCount: 1);
		IppPrinter printer = IppPrinterTextTests.CreatePrinter(client, ["image/pwg-raster"], rasterizer);

		// Act
		await printer.SubmitAsync(PrintContent.FromText("Hello"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("image/pwg-raster", client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Single(RasterDocumentReader.Read(Assert.Single(client.ReceivedDocuments)).Pages);
	}

	[Fact]
	public async Task GivenAPrinterTakingNeitherPdfNorRasterWhenTextIsSubmittedThenNotSupportedAndNothingIsSent()
	{
		// Arrange — it lists text, but Latiprimere sends network printers a PDF
		FakeIppClient client = IppPrinterTextTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterTextTests.CreatePrinter(client, ["text/plain", "application/octet-stream"]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromText("Hello"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("text/plain documents", exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenACharacterCourierCantShowWhenTextIsSubmittedToANetworkPrinterThenNotSupportedAndNothingIsSent()
	{
		// Arrange
		FakeIppClient client = IppPrinterTextTests.CreateClient(RasterJobPlannerTests.PwgAttributes("sgray_8"));
		IppPrinter printer = IppPrinterTextTests.CreatePrinter(client, ["application/pdf"]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromText("Grüße, Привет"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — ü and ß are Windows-1252; П isn't
		NotSupportedException exception = await Assert.ThrowsAnyAsync<NotSupportedException>(submit);
		Assert.Contains("U+041F", exception.Message, StringComparison.Ordinal);
		Assert.Contains("line 1, column 8", exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenCupsWhenTextInAnotherEncodingIsSubmittedThenItIsSentAsTextPlainInUtf8ForAnyCharacter()
	{
		// Arrange — UTF-16 with its byte order mark, and characters Courier couldn't show, which CUPS can
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrinterTextTests.PrinterUri, new PrinterDescriptionAttributes());
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();
		IppPrinter printer = new IppPrinter(context, IppPrinterTextTests.PrinterUri, new PrinterInfo { Id = "cups:Office", Name = "Office" });
		using MemoryStream document = new MemoryStream([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("Привет\n")]);

		// Act
		await printer.SubmitAsync(PrintContent.FromText(document, Encoding.Unicode), new PrintOptions { Scaling = ScalingMode.Fit }, TestContext.Current.CancellationToken);

		// Assert — no byte order mark, and no scaling, which is for images
		Assert.Equal("text/plain", client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Null(client.LastPrintJobRequest?.JobTemplateAttributes?.PrintScaling);
		Assert.Equal(Encoding.UTF8.GetBytes("Привет\n"), Assert.Single(client.ReceivedDocuments));
	}

	/// <summary>
	/// Creates a client with a printer whose attributes are given, as its full attribute list.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>The client.</returns>
	private static FakeIppClient CreateClient(IReadOnlyList<IppAttribute> attributes)
	{
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter(IppPrinterTextTests.PrinterUri, new PrinterDescriptionAttributes());
		client.SetAttributeList(IppPrinterTextTests.PrinterUri, attributes);

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

		return new IppPrinter(builder.Build(), IppPrinterTextTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, formats);
	}
}
