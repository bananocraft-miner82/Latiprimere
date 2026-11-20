using Latiprimere.Ipp;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Windows.Tests.DirectIpp;

/// <summary>
/// PDF jobs sent straight to a queue's IPP printer (ADR 0027), with a fake IPP client and a fake GDI printer.
/// </summary>
public sealed class WindowsPrinterIppRoutingTests : IDisposable
{
	private const string PrinterName = "Office";                                            // The queue's name
	private const string PortName = "http://printer.local:631/ipp/print";                    // A queue added by URL
	private static readonly Uri PrinterUri = new Uri("ipp://printer.local:631/ipp/print");   // Its IPP printer

	private readonly FakeIppClient client = new FakeIppClient { PrintJobId = 77 }; // The printer, over IPP
	private readonly FakeGdiPrinter gdi = new FakeGdiPrinter { NextJobId = 5 };   // The spooler's PDF path
	private readonly FakePdfRasterizer rasterizer = new FakePdfRasterizer();     // Opened only on the spooler path
	private readonly WindowsIppRoute route;                                        // The route under test

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsPrinterIppRoutingTests"/> class.
	/// </summary>
	public WindowsPrinterIppRoutingTests()
	{
		this.route = new WindowsIppRoute(new IppPrintContextBuilder().WithClient(this.client).WithDocumentFormats(IppDocumentFormats.Standard).Build(), []);
	}

	[Fact]
	public async Task GivenPrinterAcceptingPdfWhenPdfSubmittedThenSendsItOverIppKeepingTheWindowsPrinterId()
	{
		// Arrange
		this.AddPrinter("application/pdf", "image/urf");
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		PrintOptions options = new PrintOptions
		{
			Copies        = 2,
			VendorOptions = new Dictionary<string, string> { ["ipp:print-quality"] = "5" }
		};

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert — ipp: keys reach a routed job; neither the rasteriser nor GDI is used
		Assert.Equal("ipp:77", job.Id);
		Assert.Equal("windows:Office", job.PrinterId);
		Assert.Equal(WindowsPrinterIppRoutingTests.PrinterUri, this.client.LastPrintJobRequest?.OperationAttributes?.PrinterUri?.Value);
		Assert.Equal(2, this.client.LastPrintJobRequest?.JobTemplateAttributes?.Copies?.Value);
		Assert.Equal("print-quality", Assert.Single(this.client.LastVendorAttributes?.Job ?? []).Name);
		Assert.Null(this.rasterizer.LastDocument);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenPrinterTakingJpegWhenJpegSubmittedThenSendsItOverIppAsItIs()
	{
		// Arrange
		this.AddPrinter("application/pdf", "image/jpeg");
		WindowsPrinter printer = this.CreatePrinter();
		byte[] jpeg = WindowsPrinterImageTests.Jpeg(400, 300);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream(jpeg)), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:77", job.Id);
		Assert.Equal("image/jpeg", this.client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Equal(jpeg, Assert.Single(this.client.ReceivedDocuments));
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenPrinterTakingPdfWhenPngSubmittedThenSendsItOverIppAsAPdf()
	{
		// Arrange — the printer's default paper, read for the image's page
		this.AddPrinter("application/pdf");
		this.client.SetAttributeList(WindowsPrinterIppRoutingTests.PrinterUri, [new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm")]);
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream png = new MemoryStream(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR4nGNgAAAAAgABSK+kcQAAAABJRU5ErkJggg=="));

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPng(png), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:77", job.Id);
		Assert.Equal("application/pdf", this.client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.StartsWith("%PDF-1.7", System.Text.Encoding.Latin1.GetString(Assert.Single(this.client.ReceivedDocuments), 0, 8), StringComparison.Ordinal);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenPrinterTakingPdfWhenTextSubmittedThenSendsItOverIppLaidOutAsAPdf()
	{
		// Arrange — a text-only driver too, which couldn't print it, though the printer behind it can
		this.AddPrinter("application/pdf");
		this.client.SetAttributeList(WindowsPrinterIppRoutingTests.PrinterUri, [new IppAttribute(Tag.Keyword, "media-default", "na_letter_8.5x11in")]);
		WindowsPrinter printer = this.CreatePrinter(driverName: "Generic / Text Only");

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromText("Hello"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:77", job.Id);
		Assert.Equal("application/pdf", this.client.LastPrintJobRequest?.OperationAttributes?.DocumentFormat?.Value.Value);
		Assert.Contains("/MediaBox [0 0 612 792]", System.Text.Encoding.Latin1.GetString(Assert.Single(this.client.ReceivedDocuments)), StringComparison.Ordinal);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenTextWithACharacterNoLayoutShowsWhenSentOverIppThenItIsRefusedWithoutTryingTheDriver()
	{
		// Arrange
		this.AddPrinter("application/pdf");
		this.client.SetAttributeList(WindowsPrinterIppRoutingTests.PrinterUri, [new IppAttribute(Tag.Keyword, "media-default", "iso_a4_210x297mm")]);
		WindowsPrinter printer = this.CreatePrinter();

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromText("日本"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the character's refusal, not one from the driver's path
		NotSupportedException exception = await Assert.ThrowsAnyAsync<NotSupportedException>(submit);
		Assert.Contains("U+65E5", exception.Message, StringComparison.Ordinal);
		Assert.Null(this.rasterizer.LastContent);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenATextOnlyDriverWhosePrinterTakesPdfWhenCapabilitiesAreReadThenTheyListWhatGoesOverIpp()
	{
		// Arrange — the driver takes raw data only; the printer behind it takes PDF
		this.AddPrinter("application/pdf");
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities(WindowsPrinterIppRoutingTests.PrinterName, new WindowsCapabilitiesRecord());
		WindowsPrinter printer = this.CreatePrinter(spool: spool, driverName: "Generic / Text Only");

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal([DocumentFormat.Pdf, DocumentFormat.Raw, DocumentFormat.Jpeg, DocumentFormat.Png, DocumentFormat.Text], capabilities.DocumentFormats);
	}

	[Fact]
	public async Task GivenTextOnlyDriverWhosePrinterTakesPdfWhenPdfSubmittedThenSendsItOverIpp()
	{
		// Arrange — the driver couldn't print the PDF, but the printer behind the queue can
		this.AddPrinter("application/pdf");
		WindowsPrinter printer = this.CreatePrinter(driverName: "Generic / Text Only");
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:77", job.Id);
		Assert.NotNull(this.client.LastPrintJobRequest);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenTextOnlyDriverWhosePrinterDoesNotTakePdfWhenPdfSubmittedThenRefusesIt()
	{
		// Arrange — the spooler would be next, and its driver prints text only
		this.AddPrinter("image/urf");
		WindowsPrinter printer = this.CreatePrinter(driverName: "Generic / Text Only");
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("Generic / Text Only", exception.Message, StringComparison.Ordinal);
		Assert.Null(this.client.LastPrintJobRequest);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenPrinterThatDoesNotTakePdfWhenPdfSubmittedThenPrintsThroughTheSpooler()
	{
		// Arrange
		this.AddPrinter("image/urf", "application/vnd.hp-pcl");
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("5", job.Id);
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenPrinterThatCantBeAskedWhenPdfSubmittedThenPrintsThroughTheSpoolerAndAsksAgainNextTime()
	{
		// Arrange
		this.client.AddUnreachable(WindowsPrinterIppRoutingTests.PrinterUri);
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream first = WindowsPrinterIppRoutingTests.CreatePdf();
		using MemoryStream second = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act — the printer answers by the second job
		IPrintJob firstJob = await printer.SubmitAsync(PrintContent.FromPdf(first), PrintOptions.Default, TestContext.Current.CancellationToken);
		this.client.AddPrinter(WindowsPrinterIppRoutingTests.PrinterUri, new PrinterAttributesBuilder().WithDocumentFormats("application/pdf").Build());
		WindowsPrinterIppRoutingTests.RemoveUnreachable(this.client);
		IPrintJob secondJob = await printer.SubmitAsync(PrintContent.FromPdf(second), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("5", firstJob.Id);
		Assert.Equal("ipp:77", secondJob.Id);
	}

	[Theory]
	[InlineData("output")]
	[InlineData("tray")]
	[InlineData("devmode")]
	public async Task GivenJobNeedingTheSpoolerWhenPdfSubmittedThenDoesNotAskThePrinter(string need)
	{
		// Arrange — a file to print to, a Windows tray number, a driver's DEVMODE (provided by InlineData above)
		this.AddPrinter("application/pdf");
		byte[] devMode = new byte[220];
		devMode[68] = 220;

		PrintOptions options = need switch
		{
			"output" => new PrintOptions { OutputFile = "out.pdf" },
			"tray"   => new PrintOptions { InputTray = new InputTray { Id = "257", Name = "Tray 1" } },
			_        => new PrintOptions { VendorOptions = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(devMode) } },
		};

		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities(WindowsPrinterIppRoutingTests.PrinterName, new WindowsCapabilitiesRecord { Bins = [new WindowsBinRecord { Id = 257, Name = "Tray 1" }] });
		WindowsPrinter printer = this.CreatePrinter(spool: spool);
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	[Theory]
	[InlineData(@"\\server\Office", PortName)]
	[InlineData(PrinterName, "WSD-a316ac12-318f-4e7e-ae4c-f523327f4f2d")]
	public async Task GivenQueueWithoutAKnownIppPrinterWhenPdfSubmittedThenPrintsThroughTheSpooler(string printerName, string portName)
	{
		// Arrange — a print server's queue, and a WSD queue nobody configured (provided by InlineData above)
		this.AddPrinter("application/pdf");
		WindowsPrinter printer = this.CreatePrinter(printerName, portName);
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenRouteOffWhenPdfSubmittedThenPrintsThroughTheSpooler()
	{
		// Arrange
		this.AddPrinter("application/pdf");
		WindowsPrinter printer = this.CreatePrinter(withRoute: false);
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	[Theory]
	[InlineData(HttpRequestError.ConnectionError)]
	[InlineData(HttpRequestError.NameResolutionError)]
	public async Task GivenPrinterGoneBeforeTheJobConnectsWhenPdfSubmittedThenSpoolerPrintsItFromTheStart(HttpRequestError error)
	{
		// Arrange — the printer said it takes PDF, then couldn't be reached for the job (error provided by InlineData above)
		this.AddPrinter("application/pdf");
		this.client.PrintJobFailureToThrow = new PrintException("Could not communicate.", new HttpRequestException(error, "Gone."));
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the fake read the document, so the spooler only gets all of it if it was rewound
		Assert.Equal("5", job.Id);
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Equal(0, document.Position);
	}

	[Fact]
	public async Task GivenPrinterRefusingTheJobWhenPdfSubmittedThenThrowsWithoutTryingTheSpooler()
	{
		// Arrange — client-error-attributes-or-values-not-supported, once connected
		this.AddPrinter("application/pdf");
		this.client.PrintJobFailureToThrow = new PrintException("Rejected.") { NativeErrorCode = 0x040B };
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the spooler could print it twice if the printer had started
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Equal(0x040B, exception.NativeErrorCode);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenConnectFailureWithStreamThatCantSeekWhenPdfSubmittedThenThrows()
	{
		// Arrange — the document can't be read again for the spooler
		this.AddPrinter("application/pdf");
		this.client.PrintJobFailureToThrow = new PrintException("Could not communicate.", new HttpRequestException(HttpRequestError.ConnectionError, "Gone."));
		WindowsPrinter printer = this.CreatePrinter();
		using Latiprimere.Testing.Fakes.ForwardOnlyStream document = new Latiprimere.Testing.Fakes.ForwardOnlyStream([0x25, 0x50, 0x44, 0x46]);

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenRawContentWhenSubmittedThenStaysOnTheSpooler()
	{
		// Arrange
		this.AddPrinter("application/pdf", "application/octet-stream");
		FakeWinSpool spool = new FakeWinSpool();
		spool.AddPrinter(new WindowsPrinterRecord { Name = WindowsPrinterIppRoutingTests.PrinterName });
		WindowsPrinter printer = this.CreatePrinter(spool: spool);
		using MemoryStream document = new MemoryStream([0x1B, 0x40]);

		// Act
		await printer.SubmitAsync(PrintContent.FromRaw(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(spool.LastSubmittedBytes);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenJobSentOverIppWhenGetJobThenAsksThePrinterForIt()
	{
		// Arrange
		this.AddPrinter("application/pdf");
		WindowsPrinter printer = this.CreatePrinter();
		using MemoryStream document = WindowsPrinterIppRoutingTests.CreatePdf();
		IPrintJob submitted = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);
		this.client.JobAttributes = new JobDescriptionAttributes { JobState = new IppValue<JobState>(JobState.Processing) };

		// Act
		IPrintJob? found = await printer.GetJobAsync(submitted.Id, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(found);
		Assert.Equal("ipp:77", found.Id);
		Assert.Equal("windows:Office", found.PrinterId);
		Assert.Equal(77, this.client.LastGetJobAttributesRequest?.OperationAttributes?.JobId?.Value);
		Assert.Equal(WindowsPrinterIppRoutingTests.PrinterUri, this.client.LastGetJobAttributesRequest?.OperationAttributes?.PrinterUri?.Value);
	}

	[Fact]
	public async Task GivenRouteOffWhenGetJobSentOverIppThenReturnsNull()
	{
		// Arrange
		this.AddPrinter("application/pdf");
		WindowsPrinter printer = this.CreatePrinter(withRoute: false);

		// Act
		IPrintJob? found = await printer.GetJobAsync("ipp:77", TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(found);
		Assert.Null(this.client.LastGetJobAttributesRequest);
	}

	[Fact]
	public async Task GivenPrinterUnreachableWhenGetJobSentOverIppThenThrowsRatherThanSayingItIsGone()
	{
		// Arrange — the job may still be on the printer, which is switched off
		this.AddPrinter("application/pdf");
		this.client.JobFailureToThrow = new PrintException("Could not communicate.", new HttpRequestException(HttpRequestError.ConnectionError, "Gone."));
		WindowsPrinter printer = this.CreatePrinter();

		// Act
		Func<Task> getJob = () => printer.GetJobAsync("ipp:77", TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(getJob);
		Assert.Same(this.client.JobFailureToThrow, exception);
	}

	[Fact]
	public async Task GivenQueueWithoutAnIppPrinterWhenGetJobSentOverIppThenReturnsNull()
	{
		// Arrange — a local port, with the route on: no job of this queue can have gone over IPP
		WindowsPrinter printer = this.CreatePrinter(printerName: "Label Printer", portName: "USB001");

		// Act
		IPrintJob? found = await printer.GetJobAsync("ipp:77", TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(found);
		Assert.Null(this.client.LastGetJobAttributesRequest);
	}

	/// <summary>
	/// Disposes the route and its fake client.
	/// </summary>
	public void Dispose()
	{
		this.route.Dispose();
	}

	/// <summary>
	/// Creates a small stand-in PDF.
	/// </summary>
	/// <returns>The stream, at its start.</returns>
	private static MemoryStream CreatePdf()
	{
		return new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D]);
	}

	/// <summary>
	/// Makes a printer that was unreachable answer, by clearing the fake's unreachable list.
	/// </summary>
	/// <param name="client">The fake client.</param>
	private static void RemoveUnreachable(FakeIppClient client)
	{
		client.ClearUnreachable();
	}

	/// <summary>
	/// Adds the queue's IPP printer to the fake client, accepting the given formats.
	/// </summary>
	/// <param name="formats">The MIME types it accepts.</param>
	private void AddPrinter(params string[] formats)
	{
		this.client.AddPrinter(WindowsPrinterIppRoutingTests.PrinterUri, new PrinterAttributesBuilder().WithDocumentFormats(formats).Build());
	}

	/// <summary>
	/// Creates the Windows printer under test.
	/// </summary>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="portName">The queue's port.</param>
	/// <param name="withRoute">Whether sending PDFs over IPP is on.</param>
	/// <param name="spool">The spooler, or <see langword="null"/> for a new fake.</param>
	/// <param name="driverName">The queue's driver, or <see langword="null"/> if not known.</param>
	/// <returns>The printer.</returns>
	private WindowsPrinter CreatePrinter
	(
		string        printerName = PrinterName,
		string        portName = PortName,
		bool          withRoute = true,
		FakeWinSpool? spool = null,
		string?       driverName = null
	)
	{
		WindowsPrintContextBuilder builder = new WindowsPrintContextBuilder()
			.WithSpool(spool ?? new FakeWinSpool())
			.WithGdi(this.gdi)
			.WithPdfRasterizer(this.rasterizer);

		if (withRoute)
		{
			builder.WithIppRoute(this.route);
		}

		PrinterInfo info = new PrinterInfo { Id = "windows:" + printerName, Name = printerName };

		return new WindowsPrinter(builder.Build(), printerName, driverName, portName, info);
	}
}
