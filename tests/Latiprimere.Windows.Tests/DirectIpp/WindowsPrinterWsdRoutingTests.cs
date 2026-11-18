using Latiprimere.Ipp;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Testing.Raster;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Windows.Tests.DirectIpp;

/// <summary>
/// PDF jobs to WSD queues, sent over IPP to the printer announcing the UUID the queue's port records, as PDF or as
/// raster (ADR 0036).
/// </summary>
public sealed class WindowsPrinterWsdRoutingTests : IDisposable
{
	private const string PrinterName = "HP ENVY 5640 series";                       // The queue
	private const string PortName = "WSD-a316ac12-318f-4e7e-ae4c-f523327f4f2d";      // Its WSD port
	private const string Uuid = "1c852a4d-b800-1f08-abcd-a0d3c1d9e51b";              // The UUID the port records

	private static readonly Uri PrinterUri = new Uri("ipp://hp-envy.local:631/ipp/print"); // Where it's announced

	private readonly FakeIppClient client = new FakeIppClient { PrintJobId = 88 };       // The printer, over IPP
	private readonly FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();  // The network
	private readonly FakeWinSpool spool = new FakeWinSpool();                            // The spooler and registry
	private readonly FakeGdiPrinter gdi = new FakeGdiPrinter { NextJobId = 5 };          // The driver's path
	private readonly FakePdfRasterizer rasterizer = new FakePdfRasterizer { PageCount = 2 }; // Renders for both paths
	private readonly WindowsIppRoute route;                                              // The route under test

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsPrinterWsdRoutingTests"/> class.
	/// </summary>
	public WindowsPrinterWsdRoutingTests()
	{
		// As the factory builds it: the Windows rasteriser renders raster for printers that can't take PDF
		IppPrintContext context = new IppPrintContextBuilder()
			.WithClient(this.client)
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.WithPdfRasterizer(this.rasterizer)
			.Build();

		this.route = new WindowsIppRoute(context, [], this.discovery);
		this.spool.WsdPrinterUuids[WindowsPrinterWsdRoutingTests.PortName] = "urn:uuid:" + WindowsPrinterWsdRoutingTests.Uuid.ToUpperInvariant();
		this.discovery.AddPrinter("HP ENVY 5640 series [D9E51B]", WindowsPrinterWsdRoutingTests.PrinterUri, WindowsPrinterWsdRoutingTests.Uuid);
	}

	[Fact]
	public async Task GivenAWsdQueueWhosePrinterTakesPdfWhenAPdfIsSubmittedThenItGoesOverIppAsPdf()
	{
		// Arrange
		this.AddPrinter(WindowsPrinterWsdRoutingTests.Uuid, "application/pdf", "image/urf");
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterWsdRoutingTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(("88", "windows:" + WindowsPrinterWsdRoutingTests.PrinterName), (job.Id, job.PrinterId));
		Assert.Equal(WindowsPrinterWsdRoutingTests.PrinterUri, this.client.LastPrintJobRequest?.OperationAttributes?.PrinterUri?.Value);
		Assert.Equal("application/pdf", this.client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenAWsdQueueWhosePrinterTakesOnlyAppleRasterWhenAPdfIsSubmittedThenItGoesOverIppAsAppleRaster()
	{
		// Arrange — as the HP ENVY 5640 does
		this.AddPrinter(WindowsPrinterWsdRoutingTests.Uuid, "application/vnd.hp-PCL", "image/urf", "application/PCLm");
		this.client.SetAttributeList(WindowsPrinterWsdRoutingTests.PrinterUri, WindowsPrinterWsdRoutingTests.UrfAttributes(withPaper: true));
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterWsdRoutingTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the job is the printer's, and the driver isn't used
		Assert.Equal("88", job.Id);
		Assert.Equal("image/urf", this.client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Null(this.gdi.LastRequest);

		RasterDocument raster = RasterDocumentReader.Read(Assert.Single(this.client.ReceivedDocuments));
		Assert.Equal(2, raster.Pages.Count);
		Assert.All(raster.Pages, page => Assert.Equal((2480, 3507, 300), (page.Width, page.Height, page.DpiX)));
	}

	[Fact]
	public async Task GivenARasterJobThePrinterCantBePlannedForWhenAPdfIsSubmittedThenTheDriverPrintsIt()
	{
		// Arrange — no default paper the encoder can size, and none chosen
		this.AddPrinter(WindowsPrinterWsdRoutingTests.Uuid, "image/urf");
		this.client.SetAttributeList(WindowsPrinterWsdRoutingTests.PrinterUri, WindowsPrinterWsdRoutingTests.UrfAttributes(withPaper: false));
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterWsdRoutingTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("5", job.Id);
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenAnotherPrinterAnswersWhereTheUuidWasFoundWhenAPdfIsSubmittedThenTheDriverPrintsItAndTheMatchIsForgotten()
	{
		// Arrange — a different printer at the announced address now
		this.AddPrinter("99999999-0000-0000-0000-000000000000", "application/pdf");
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream first = WindowsPrinterWsdRoutingTests.CreatePdf();
		using MemoryStream second = WindowsPrinterWsdRoutingTests.CreatePdf();

		// Act
		IPrintJob firstJob = await printer.SubmitAsync(PrintContent.FromPdf(first), PrintOptions.Default, TestContext.Current.CancellationToken);
		await printer.SubmitAsync(PrintContent.FromPdf(second), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — searched for again at the second job
		Assert.Equal("5", firstJob.Id);
		Assert.Null(this.client.LastPrintJobRequest);
		Assert.Equal(2, this.discovery.SearchedUuids.Count);
	}

	[Fact]
	public async Task GivenAPrinterThatReportsNoUuidWhenAPdfIsSubmittedThenTheDriverPrintsIt()
	{
		// Arrange — an answer without printer-uuid can't be checked, so isn't trusted
		this.client.AddPrinter(WindowsPrinterWsdRoutingTests.PrinterUri, new PrinterAttributesBuilder().WithDocumentFormats("application/pdf").Build());
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterWsdRoutingTests.CreatePdf();

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenTheFoundPrinterIsUnreachableWhenAPdfIsSubmittedThenTheDriverPrintsItAndTheMatchIsForgotten()
	{
		// Arrange
		this.client.AddUnreachable(WindowsPrinterWsdRoutingTests.PrinterUri);
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream first = WindowsPrinterWsdRoutingTests.CreatePdf();
		using MemoryStream second = WindowsPrinterWsdRoutingTests.CreatePdf();

		// Act
		IPrintJob firstJob = await printer.SubmitAsync(PrintContent.FromPdf(first), PrintOptions.Default, TestContext.Current.CancellationToken);
		await printer.SubmitAsync(PrintContent.FromPdf(second), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("5", firstJob.Id);
		Assert.Equal(2, this.discovery.SearchedUuids.Count);
	}

	[Fact]
	public async Task GivenNoRasterizerOnTheRouteWhenAPdfIsSubmittedToARasterOnlyPrinterThenTheDriverPrintsIt()
	{
		// Arrange — the route as for an application without a rasteriser for IPP
		using WindowsIppRoute withoutRaster = new WindowsIppRoute
		(
			new IppPrintContextBuilder().WithClient(this.client).WithDocumentFormats(IppDocumentFormats.Standard).Build(),
			[],
			this.discovery
		);

		this.AddPrinter(WindowsPrinterWsdRoutingTests.Uuid, "image/urf");
		WindowsPrinter printer = this.CreatePrinter(withoutRaster);
		using MemoryStream document = WindowsPrinterWsdRoutingTests.CreatePdf();

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.route.Dispose();
	}

	/// <summary>
	/// Creates a small stand-in PDF, which the fake rasteriser doesn't read.
	/// </summary>
	/// <returns>The stream, at its start.</returns>
	private static MemoryStream CreatePdf()
	{
		return new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D]);
	}

	/// <summary>
	/// An Apple raster printer's raster attributes, at 300 dpi.
	/// </summary>
	/// <param name="withPaper">Whether its default paper is A4, or a roll without a size.</param>
	/// <returns>The attributes.</returns>
	private static List<IppAttribute> UrfAttributes(bool withPaper)
	{
		return
		[
			new IppAttribute(Tag.Keyword, "urf-supported", "RS300"),
			new IppAttribute(Tag.Keyword, string.Empty, "W8"),
			new IppAttribute(Tag.Keyword, string.Empty, "SRGB24"),
			new IppAttribute(Tag.Keyword, string.Empty, "DM3"),
			new IppAttribute(Tag.Keyword, "media-default", withPaper ? "iso_a4_210x297mm" : "roll_current"),
		];
	}

	/// <summary>
	/// Puts a printer at the announced address, reporting a UUID and taking the given formats.
	/// </summary>
	/// <param name="uuid">The <c>printer-uuid</c> it reports.</param>
	/// <param name="formats">The MIME types it accepts.</param>
	private void AddPrinter(string uuid, params string[] formats)
	{
		this.client.AddPrinter
		(
			WindowsPrinterWsdRoutingTests.PrinterUri,
			new PrinterAttributesBuilder().WithUuid("urn:uuid:" + uuid).WithDocumentFormats(formats).Build()
		);
	}

	/// <summary>
	/// Creates the WSD queue under test.
	/// </summary>
	/// <param name="route">The route, or <see langword="null"/> for the test's.</param>
	/// <returns>The printer.</returns>
	private WindowsPrinter CreatePrinter(WindowsIppRoute? route = null)
	{
		WindowsPrintContext context = new WindowsPrintContextBuilder()
			.WithSpool(this.spool)
			.WithGdi(this.gdi)
			.WithPdfRasterizer(this.rasterizer)
			.WithIppRoute(route ?? this.route)
			.Build();

		PrinterInfo info = new PrinterInfo { Id = "windows:" + WindowsPrinterWsdRoutingTests.PrinterName, Name = WindowsPrinterWsdRoutingTests.PrinterName };

		return new WindowsPrinter(context, WindowsPrinterWsdRoutingTests.PrinterName, null, WindowsPrinterWsdRoutingTests.PortName, info);
	}
}
