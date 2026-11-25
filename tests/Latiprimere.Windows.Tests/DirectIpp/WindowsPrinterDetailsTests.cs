using System.Text;
using Latiprimere.Ipp;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Windows.Tests.DirectIpp;

/// <summary>
/// A Windows queue's details and identifying it, which only its IPP printer can give, and its test page, printed as any
/// PDF is (ADR 0059).
/// </summary>
public sealed class WindowsPrinterDetailsTests : IDisposable
{
	private const string PrinterName = "Office";                                          // The queue's name
	private const string PortName = "http://printer.local:631/ipp/print";                  // A queue added by URL
	private static readonly Uri PrinterUri = new Uri("ipp://printer.local:631/ipp/print"); // Its IPP printer

	private readonly FakeIppClient client = new FakeIppClient { PrintJobId = 77 }; // The printer, over IPP
	private readonly FakeGdiPrinter gdi = new FakeGdiPrinter { NextJobId = 5 };   // The spooler's PDF path
	private readonly FakePdfRasterizer rasterizer = new FakePdfRasterizer();     // Opened only on the spooler path
	private readonly WindowsIppRoute route;                                        // The queue's route to its printer

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsPrinterDetailsTests"/> class.
	/// </summary>
	public WindowsPrinterDetailsTests()
	{
		this.route = new WindowsIppRoute(new IppPrintContextBuilder().WithClient(this.client).WithDocumentFormats(IppDocumentFormats.Standard).Build(), []);
	}

	[Fact]
	public async Task GivenTheIppRouteWhenTheQueuesDetailsAreReadThenItsPrintersAreGivenWithTheWindowsId()
	{
		// Arrange
		this.AddPrinter([new IppAttribute(Tag.Uri, "printer-more-info", "http://printer.local/"), new IppAttribute(Tag.Keyword, "identify-actions-supported", "sound")]);
		WindowsPrinter printer = this.CreatePrinter(withRoute: true);

		// Act
		PrinterDetails details = await printer.GetDetailsAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("windows:Office", details.PrinterId);
		Assert.Equal(new Uri("http://printer.local/"), details.WebPage);
		Assert.Equal([IdentifyAction.Sound], details.IdentifyActions);
	}

	[Fact]
	public async Task GivenNoIppRouteWhenTheQueuesDetailsAreReadThenTheyHaveOnlyItsId()
	{
		// Arrange
		this.AddPrinter([new IppAttribute(Tag.Uri, "printer-more-info", "http://printer.local/")]);
		WindowsPrinter printer = this.CreatePrinter(withRoute: false);

		// Act
		PrinterDetails details = await printer.GetDetailsAsync(TestContext.Current.CancellationToken);

		// Assert — the spooler has none, and the printer isn't asked
		Assert.Equal("windows:Office", details.PrinterId);
		Assert.Null(details.WebPage);
		Assert.Equal(0, this.client.AttributeListRequestCount);
	}

	[Fact]
	public async Task GivenTheQueuesIppPrinterCannotBeReachedWhenItsDetailsAreReadThenTheyHaveOnlyItsId()
	{
		// Arrange
		this.client.AddUnreachable(WindowsPrinterDetailsTests.PrinterUri);
		this.AddPrinter([]);
		WindowsPrinter printer = this.CreatePrinter(withRoute: true);

		// Act
		PrinterDetails details = await printer.GetDetailsAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("windows:Office", details.PrinterId);
		Assert.Empty(details.IdentifyActions);
	}

	[Fact]
	public async Task GivenTheIppRouteWhenTheQueueIsIdentifiedThenItsPrinterIsAsked()
	{
		// Arrange
		this.AddPrinter([]);
		WindowsPrinter printer = this.CreatePrinter(withRoute: true);

		// Act
		await printer.IdentifyAsync("Office", TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(WindowsPrinterDetailsTests.PrinterUri, Assert.Single(this.client.IdentifyPrinterRequests).OperationAttributes?.PrinterUri?.Value);
	}

	[Fact]
	public async Task GivenNoIppRouteWhenTheQueueIsIdentifiedThenThrowsNotSupportedSayingHowItCouldBe()
	{
		// Arrange
		WindowsPrinter printer = this.CreatePrinter(withRoute: false);

		// Act
		Func<Task> identify = () => printer.IdentifyAsync(null, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(identify);
		Assert.Contains("PrintPdfOverIpp", exception.Message, StringComparison.Ordinal);
		Assert.Empty(this.client.IdentifyPrinterRequests);
	}

	[Fact]
	public async Task GivenNoIppRouteWhenATestPageIsPrintedThenAPdfOnTheDriversPaperGoesThroughTheDriver()
	{
		// Arrange — the driver's default is A4
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities(WindowsPrinterDetailsTests.PrinterName, WindowsPrinterDetailsTests.Capabilities());
		spool.AddPrinter(new WindowsPrinterRecord { Name = WindowsPrinterDetailsTests.PrinterName });
		WindowsPrinter printer = this.CreatePrinter(withRoute: false, spool);

		// Act
		IPrintJob job = await printer.PrintTestPageAsync(PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("5", job.Id);
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(this.gdi.LastRequest);
		Assert.Equal("Latiprimere test page", request.DocumentName);
		Assert.Contains("/MediaBox [0 0 595.2756 841.8898]", Encoding.Latin1.GetString(this.rasterizer.LastContent!), StringComparison.Ordinal);
		Assert.Empty(this.client.ReceivedDocuments);
	}

	[Fact]
	public async Task GivenTheIppRouteAndAPrinterTakingPdfWhenATestPageIsPrintedThenItGoesOverIpp()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities(WindowsPrinterDetailsTests.PrinterName, WindowsPrinterDetailsTests.Capabilities());
		spool.AddPrinter(new WindowsPrinterRecord { Name = WindowsPrinterDetailsTests.PrinterName });
		this.AddPrinter([]);
		WindowsPrinter printer = this.CreatePrinter(withRoute: true, spool);

		// Act
		IPrintJob job = await printer.PrintTestPageAsync(PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:77", job.Id);
		Assert.Equal("Latiprimere test page", this.client.LastPrintJobRequest?.OperationAttributes?.JobName?.Value);
		Assert.StartsWith("%PDF-1.7", Encoding.Latin1.GetString(Assert.Single(this.client.ReceivedDocuments), 0, 8), StringComparison.Ordinal);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public void GivenAQueueWithADriverAndPortWhenDescribedThenEachIsNamed()
	{
		// Arrange
		WindowsPrinter printer = new WindowsPrinter(new WindowsPrintContextBuilder().Build(), "Office", "HP Universal", "WSD-1234", new PrinterInfo { Id = "windows:Office", Name = "Office" });

		// Act
		string description = printer.DescribeQueue();

		// Assert
		Assert.Equal("queue Office, driver HP Universal, port WSD-1234", description);
	}

	/// <summary>
	/// Disposes the route and its fake client.
	/// </summary>
	public void Dispose()
	{
		this.route.Dispose();
		this.client.Dispose();
	}

	/// <summary>
	/// Gives the driver's papers: Letter and A4, A4 by default.
	/// </summary>
	/// <returns>The capabilities.</returns>
	private static WindowsCapabilitiesRecord Capabilities()
	{
		return new WindowsCapabilitiesRecord
		{
			Papers =
			[
				new WindowsPaperRecord { Id = 1, Name = "Letter", WidthTenthsOfMillimetre = 2159, HeightTenthsOfMillimetre = 2794 },
				new WindowsPaperRecord { Id = 9, Name = "A4", WidthTenthsOfMillimetre = 2100, HeightTenthsOfMillimetre = 2970 },
			],
			DefaultPaperId = 9
		};
	}

	/// <summary>
	/// Adds the queue's IPP printer, taking PDF, with the raw attributes it reports.
	/// </summary>
	/// <param name="attributes">The raw attributes.</param>
	private void AddPrinter(IReadOnlyList<IppAttribute> attributes)
	{
		this.client.AddPrinter(WindowsPrinterDetailsTests.PrinterUri, new PrinterAttributesBuilder().WithDocumentFormats("application/pdf").Build());
		this.client.SetAttributeList(WindowsPrinterDetailsTests.PrinterUri, attributes);
	}

	/// <summary>
	/// Creates the Windows printer under test.
	/// </summary>
	/// <param name="withRoute">Whether sending PDFs over IPP is on.</param>
	/// <param name="spool">The spooler, or <see langword="null"/> for a new fake.</param>
	/// <returns>The printer.</returns>
	private WindowsPrinter CreatePrinter(bool withRoute, FakeWinSpool? spool = null)
	{
		WindowsPrintContextBuilder builder = new WindowsPrintContextBuilder()
			.WithSpool(spool ?? new FakeWinSpool())
			.WithGdi(this.gdi)
			.WithPdfRasterizer(this.rasterizer);

		if (withRoute)
		{
			builder.WithIppRoute(this.route);
		}

		PrinterInfo info = new PrinterInfo { Id = "windows:" + WindowsPrinterDetailsTests.PrinterName, Name = WindowsPrinterDetailsTests.PrinterName };

		return new WindowsPrinter(builder.Build(), WindowsPrinterDetailsTests.PrinterName, driverName: null, WindowsPrinterDetailsTests.PortName, info);
	}
}
