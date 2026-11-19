using System.Text;
using Latiprimere.Testing.Fakes;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using Xunit;

namespace Latiprimere.Windows.Tests;

public sealed class WindowsPrinterTests
{
	[Fact]
	public async Task GivenRawContentWhenSubmitThenSendsBytesUnderJobNameAndReturnsJob()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool { NextJobId = 31 };
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		byte[] data = new byte[] { 0x1B, 0x40, 0x41, 0x0A };
		using MemoryStream document = new MemoryStream(data);

		// Act
		IPrintJob job = await printer.SubmitAsync
		(
			PrintContent.FromRaw(document),
			new PrintOptions { JobName = "Label 7" },
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.Equal(data, spool.LastSubmittedBytes);
		Assert.Equal("Label 7", spool.LastSubmittedDocumentName);
		Assert.Equal("Office", spool.LastSubmittedPrinterName);
		Assert.Null(spool.LastSubmittedOutputFile);
		Assert.Equal("31", job.Id);
		Assert.Equal("windows:Office", job.PrinterId);
	}

	[Fact]
	public async Task GivenCopiesWhenRawSubmittedThenSpoolerSendsThatManyWithTheCallersToken()
	{
		// Arrange — RAW data bypasses the driver, so the spooler wrapper must send it once per copy
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		using MemoryStream document = new MemoryStream(new byte[] { 0x1B, 0x40 });
		using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		// Act
		await printer.SubmitAsync(PrintContent.FromRaw(document), new PrintOptions { Copies = 3 }, cancellation.Token);

		// Assert
		Assert.Equal(3, spool.LastSubmittedCopies);
		Assert.Equal(cancellation.Token, spool.LastSubmittedCancellationToken);
	}

	[Fact]
	public async Task GivenCopiesFromStreamThatCantSeekWhenRawSubmittedThenRefusesWithoutQueueingAnything()
	{
		// Arrange — each copy is sent again from the start, which a forward-only stream can't do
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		using ForwardOnlyStream document = new ForwardOnlyStream(new byte[] { 0x1B, 0x40 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromRaw(document),
			new PrintOptions { Copies = 2 },
			TestContext.Current.CancellationToken
		);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("seek", exception.Message, StringComparison.Ordinal);
		Assert.Null(spool.LastSubmittedBytes);
	}

	[Fact]
	public async Task GivenOneCopyFromStreamThatCantSeekWhenRawSubmittedThenSendsIt()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		byte[] data = new byte[] { 0x1B, 0x40 };
		using ForwardOnlyStream document = new ForwardOnlyStream(data);

		// Act
		await printer.SubmitAsync(PrintContent.FromRaw(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(data, spool.LastSubmittedBytes);
		Assert.Equal(1, spool.LastSubmittedCopies);
	}

	[Theory]
	[InlineData("Microsoft Print To PDF")]
	[InlineData("microsoft print to pdf")]
	public async Task GivenDriverThatDropsRawDataWhenRawSubmittedThenRefusesWithoutQueueingAnything(string driverName)
	{
		// Arrange — driverName is Microsoft Print to PDF's driver, in any case (provided by InlineData above)
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool), driverName);
		using MemoryStream document = new MemoryStream(new byte[] { 0x1B, 0x40 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromRaw(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("can't print raw data", exception.Message, StringComparison.Ordinal);
		Assert.Contains(driverName, exception.Message, StringComparison.Ordinal);
		Assert.Null(spool.LastSubmittedBytes);
	}

	[Theory]
	[InlineData("Generic / Text Only")]
	[InlineData("generic / text only")]
	public async Task GivenTextOnlyDriverWhenPdfSubmittedThenRefusesBeforeRenderingAnything(string driverName)
	{
		// Arrange — driverName is the Generic / Text Only driver, in any case (provided by InlineData above)
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		FakePdfRasterizer rasterizer = new FakePdfRasterizer();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(new FakeWinSpool()).WithGdi(gdi).WithPdfRasterizer(rasterizer),
			driverName
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — explained with the driver's name; the document is never opened, and nothing reaches GDI
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("can't print PDFs", exception.Message, StringComparison.Ordinal);
		Assert.Contains(driverName, exception.Message, StringComparison.Ordinal);
		Assert.Null(rasterizer.LastDocument);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenTextOnlyDriverWhenRawSubmittedThenSendsIt()
	{
		// Arrange — raw data, such as plain text, is what a text-only driver is for
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool), "Generic / Text Only");
		byte[] data = "Plain text for a text-only printer\r\n"u8.ToArray();
		using MemoryStream document = new MemoryStream(data);

		// Act
		await printer.SubmitAsync(PrintContent.FromRaw(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(data, spool.LastSubmittedBytes);
	}

	[Fact]
	public async Task GivenDriverThatPassesRawDataThroughWhenRawSubmittedThenSendsIt()
	{
		// Arrange — the XPS Document Writer, also a v4 driver, writes raw data to its output unchanged
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(spool),
			"Microsoft XPS Document Writer v4"
		);

		byte[] data = new byte[] { 0x1B, 0x40 };
		using MemoryStream document = new MemoryStream(data);

		// Act
		await printer.SubmitAsync(PrintContent.FromRaw(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(data, spool.LastSubmittedBytes);
	}

	[Fact]
	public async Task GivenNoJobNameWhenSubmitThenUsesDefaultDocumentName()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		using MemoryStream document = new MemoryStream(new byte[] { 0x41 });

		// Act
		await printer.SubmitAsync
		(
			PrintContent.FromRaw(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.False(string.IsNullOrWhiteSpace(spool.LastSubmittedDocumentName));
	}

	[Fact]
	public async Task GivenRelativeOutputFileWhenSubmitRawThenPassesFullPath()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		using MemoryStream document = new MemoryStream(new byte[] { 0x41 });
		PrintOptions options = new PrintOptions { OutputFile = "label.prn" };

		// Act
		await printer.SubmitAsync(PrintContent.FromRaw(document), options, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(Path.GetFullPath("label.prn"), spool.LastSubmittedOutputFile);
	}

	[Fact]
	public async Task GivenPdfWithoutRasterizerWhenSubmitThenThrowsNotSupportedExceptionWithoutSubmitting()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		FakeGdiPrinter gdi = new FakeGdiPrinter();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi)
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Null(spool.LastSubmittedBytes);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenPdfWithRasterizerWhenSubmitThenPrintsThroughGdiAndDisposesDocument()
	{
		// Arrange
		FakeGdiPrinter gdi = new FakeGdiPrinter { NextJobId = 44 };
		FakePdfRasterizer rasterizer = new FakePdfRasterizer { PageCount = 3 };

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithGdi(gdi).WithPdfRasterizer(rasterizer)
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		PrintOptions options = new PrintOptions { JobName = "Invoice", Copies = 2, OutputFile = "invoice.pdf" };

		// Act
		IPrintJob job = await printer.SubmitAsync
		(
			PrintContent.FromPdf(document),
			options,
			TestContext.Current.CancellationToken
		);

		// Assert
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(gdi.LastRequest);
		Assert.Equal("Office", request.PrinterName);
		Assert.Equal("Invoice", request.DocumentName);
		Assert.Equal(Path.GetFullPath("invoice.pdf"), request.OutputFile);
		Assert.Same(options, request.Options);
		Assert.Null(request.PaperId);
		Assert.Null(request.BinId);
		Assert.Equal(3, gdi.LastPageCount);
		Assert.True(rasterizer.LastDocument?.IsDisposed);
		Assert.Equal("44", job.Id);
	}

	[Fact]
	public async Task GivenSizeAndTrayTheDriverListsWhenPdfSubmittedThenPassesTheirNumbersToGdi()
	{
		// Arrange — the size has another display name than the driver's, as when read from another printer
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterTests.OfficeCapabilities());
		FakeGdiPrinter gdi = new FakeGdiPrinter();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer())
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			MediaSize = new MediaSize
			{
				Name              = "iso_a4_210x297mm",
				DisplayName       = "A4 (another driver)",
				WidthMillimeters  = 210,
				HeightMillimeters = 297
			},
			InputTray = new InputTray { Id = "257", Name = "Tray 1" }
		};

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(gdi.LastRequest);
		Assert.Equal((short)9, request.PaperId);
		Assert.Equal((short)257, request.BinId);
	}

	[Fact]
	public async Task GivenSizeTheDriverDoesNotListWhenPdfSubmittedThenRefusesBeforeOpeningTheDocument()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterTests.OfficeCapabilities());
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		FakePdfRasterizer rasterizer = new FakePdfRasterizer();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(rasterizer)
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			MediaSize = new MediaSize
			{
				Name              = "iso_a3_297x420mm",
				DisplayName       = "A3",
				WidthMillimeters  = 297,
				HeightMillimeters = 420
			}
		};

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("'Office'", exception.Message, StringComparison.Ordinal);
		Assert.Contains("A3 (iso_a3_297x420mm)", exception.Message, StringComparison.Ordinal);
		Assert.Null(rasterizer.LastDocument);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenTrayTheDriverDoesNotListWhenPdfSubmittedThenRefusesWithoutPrinting()
	{
		// Arrange — an IPP keyword, as from a printer's IPP capabilities, means nothing to the Windows driver
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterTests.OfficeCapabilities());
		FakeGdiPrinter gdi = new FakeGdiPrinter();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer())
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		PrintOptions options = new PrintOptions { InputTray = new InputTray { Id = "by-pass-tray", Name = "Bypass tray" } };

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("'Bypass tray' (ID by-pass-tray)", exception.Message, StringComparison.Ordinal);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenSizeChosenForPrinterThatHasGoneWhenPdfSubmittedThenThrowsPrintException()
	{
		// Arrange — the fake spooler has no capabilities for the printer
		FakeGdiPrinter gdi = new FakeGdiPrinter();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(new FakeWinSpool()).WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer())
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		PrintOptions options = new PrintOptions { InputTray = new InputTray { Id = "257", Name = "Tray 1" } };

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenSavedDevModeWhenPdfSubmittedThenPassesItToGdi()
	{
		// Arrange — a minimal DEVMODE: dmSize 220 at offset 68, no driver data
		byte[] saved = new byte[220];
		saved[68] = 220;
		FakeGdiPrinter gdi = new FakeGdiPrinter();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer())
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			VendorOptions = new Dictionary<string, string>
			{
				["windows:devmode"]   = Convert.ToBase64String(saved),
				["ipp:print-quality"] = "5"
			}
		};

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(gdi.LastRequest);
		Assert.Equal(saved, request.SavedDevMode);
	}

	[Fact]
	public async Task GivenPrintTicketWhenPdfSubmittedThenPassesItToGdiAsUtf8()
	{
		// Arrange — a Print Ticket that declares UTF-16, as a .NET string written out would
		const string ticket = """
			<?xml version="1.0" encoding="utf-16"?>
			<psf:PrintTicket xmlns:psf="http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework" version="1" />
			""";

		FakeGdiPrinter gdi = new FakeGdiPrinter();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer())
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		PrintOptions options = new PrintOptions { VendorOptions = new Dictionary<string, string> { ["windows:print-ticket"] = ticket } };

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(gdi.LastRequest);
		Assert.Null(request.SavedDevMode);
		byte[] sentTicket = Assert.Single(request.PrintTickets);
		string sent = Encoding.UTF8.GetString(sentTicket);
		Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", sent, StringComparison.Ordinal);
		Assert.Contains("psf:PrintTicket", sent, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenPrintTicketThatIsNotXmlWhenPdfSubmittedThenRefusesWithoutRendering()
	{
		// Arrange
		FakeGdiPrinter gdi = new FakeGdiPrinter();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer())
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		PrintOptions options = new PrintOptions { VendorOptions = new Dictionary<string, string> { ["windows:print-ticket"] = "duplex please" } };

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<ArgumentException>(submit);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenUnsupportedWindowsKeyWhenRawSubmittedThenRefusesWithoutQueueingAnything()
	{
		// Arrange — raw jobs don't use a DEVMODE, but an unsupported key is still refused rather than ignored
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		using MemoryStream document = new MemoryStream(new byte[] { 0x1B, 0x40 });
		PrintOptions options = new PrintOptions { VendorOptions = new Dictionary<string, string> { ["windows:paper"] = "A4" } };

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(document), options, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Null(spool.LastSubmittedBytes);
	}

	[Fact]
	public async Task GivenNullOptionsWhenSubmitThenThrowsArgumentNullException()
	{
		// Arrange
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder());
		using MemoryStream document = new MemoryStream();
		PrintOptions? options = null;

		// Act
		Func<Task> submit = () => printer.SubmitAsync
		(
			PrintContent.FromRaw(document),
			options!, // Deliberately null to test the guard
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<ArgumentNullException>(submit);
	}

	[Fact]
	public async Task GivenDriverCapabilitiesWhenGetCapabilitiesThenMapsThem()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", new WindowsCapabilitiesRecord { CanDuplex = true, MaxCopies = 5 });
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(5, capabilities.MaxCopies);
		Assert.Contains(DuplexMode.LongEdge, capabilities.DuplexModes);
	}

	[Fact]
	public async Task GivenPrinterThatHasGoneWhenGetCapabilitiesThenThrowsPrintException()
	{
		// Arrange — the fake spooler has no capabilities for the printer
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(new FakeWinSpool())
		);

		// Act
		Func<Task> getCapabilities = () => printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(getCapabilities);
	}

	[Fact]
	public async Task GivenDriverWithPrintCapabilitiesWhenGetCapabilitiesThenListsItsFeaturesAsVendorOptions()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();

		spool.SetCapabilities
		(
			"Office",
			WindowsPrinterTests.OfficeCapabilities() with
			{
				PrintCapabilities = Encoding.UTF8.GetBytes(Latiprimere.Windows.Tests.PrintTickets.PrintCapabilitiesDocumentTests.Capabilities)
			}
		);

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Contains(capabilities.VendorOptions, option => option.Key == "windows:feature:psk:PageOutputQuality");
		Assert.DoesNotContain(capabilities.VendorOptions, option => option.Key == "windows:feature:psk:PageMediaSize");
	}

	[Fact]
	public async Task GivenDriverWithMalformedPrintCapabilitiesWhenGetCapabilitiesThenListsNoVendorOptions()
	{
		// Arrange — the typed capabilities don't depend on it
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterTests.OfficeCapabilities() with { PrintCapabilities = Encoding.UTF8.GetBytes("<not") });
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Empty(capabilities.VendorOptions);
		Assert.NotEmpty(capabilities.MediaSizes);
	}

	[Fact]
	public async Task GivenFeatureKeysAndPrintTicketWhenPdfSubmittedThenGdiGetsBothTicketsInOrder()
	{
		// Arrange — the features are checked against the capabilities for the ticket's choices
		FakeWinSpool spool = new FakeWinSpool
		{
			PrintCapabilities = Encoding.UTF8.GetBytes(Latiprimere.Windows.Tests.PrintTickets.PrintCapabilitiesDocumentTests.Capabilities)
		};

		FakeGdiPrinter gdi = new FakeGdiPrinter();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer())
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			VendorOptions = new Dictionary<string, string>
			{
				["windows:print-ticket"]                 = Latiprimere.Windows.Tests.Spooler.WindowsVendorOptionsTests.CreatePrintTicket("psk:ISOA5"),
				["windows:feature:psk:PageOutputQuality"] = "psk:High"
			}
		};

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(gdi.LastRequest);
		Assert.Equal(2, request.PrintTickets.Count);
		Assert.Contains("psk:ISOA5", Encoding.UTF8.GetString(request.PrintTickets[0]), StringComparison.Ordinal);
		Assert.Contains("psk:High", Encoding.UTF8.GetString(request.PrintTickets[1]), StringComparison.Ordinal);
		byte[] constraint = Assert.Single(spool.LastPrintCapabilitiesTickets!);
		Assert.Equal(request.PrintTickets[0], constraint);
	}

	[Fact]
	public async Task GivenFeatureKeysAndSavedDevModeWhenPdfSubmittedThenChecksThemForTheSavedDevMode()
	{
		// Arrange — the options a saved DEVMODE rules in or out are the ones the job prints with
		FakeWinSpool spool = new FakeWinSpool
		{
			PrintCapabilities = Encoding.UTF8.GetBytes(Latiprimere.Windows.Tests.PrintTickets.PrintCapabilitiesDocumentTests.Capabilities)
		};

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(new FakeGdiPrinter()).WithPdfRasterizer(new FakePdfRasterizer())
		);

		byte[] saved = new byte[220];
		saved[68] = 220;
		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintOptions options = new PrintOptions
		{
			VendorOptions = new Dictionary<string, string>
			{
				["windows:devmode"]                       = Convert.ToBase64String(saved),
				["windows:feature:psk:PageOutputQuality"] = "psk:High"
			}
		};

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(saved, spool.LastPrintCapabilitiesDevMode);
	}

	[Fact]
	public async Task GivenDriverWithoutPrintTicketSupportWhenGetCapabilitiesThenListsNoVendorOptionsAndLogsWhy()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		PrintException failure = new PrintException("No provider.");
		spool.SetCapabilities("Office", WindowsPrinterTests.OfficeCapabilities() with { PrintSchemaFailure = failure });
		Latiprimere.Testing.Fakes.RecordingLogger logger = new Latiprimere.Testing.Fakes.RecordingLogger();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithLogger(logger));

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Empty(capabilities.VendorOptions);
		Assert.NotEmpty(capabilities.MediaSizes);
		Assert.Same(failure, Assert.Single(logger.Entries).Exception);
	}

	[Fact]
	public async Task GivenDriverWithoutPrintTicketSupportWhenGetCapabilitiesTwiceThenTriesAndLogsOnlyOnce()
	{
		// Arrange — every read would otherwise open a Print Ticket session, fail and log
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterTests.OfficeCapabilities() with { PrintSchemaFailure = new PrintException("No provider.") });
		Latiprimere.Testing.Fakes.RecordingLogger logger = new Latiprimere.Testing.Fakes.RecordingLogger();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithLogger(logger));

		// Act
		await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);
		PrinterCapabilities second = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(1, spool.PrintSchemaRequests);
		Assert.Single(logger.Entries);
		Assert.NotEmpty(second.MediaSizes);
	}

	[Fact]
	public async Task GivenDefaultTicketThatIsNotXmlWhenGetCapabilitiesThenListsTheOptionsWithoutDefaultsAndLogsIt()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();

		spool.SetCapabilities
		(
			"Office",
			WindowsPrinterTests.OfficeCapabilities() with
			{
				PrintCapabilities  = Encoding.UTF8.GetBytes(Latiprimere.Windows.Tests.PrintTickets.PrintCapabilitiesDocumentTests.Capabilities),
				DefaultPrintTicket = Encoding.UTF8.GetBytes("<not")
			}
		);

		Latiprimere.Testing.Fakes.RecordingLogger logger = new Latiprimere.Testing.Fakes.RecordingLogger();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithLogger(logger));

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		VendorOption quality = Assert.Single(capabilities.VendorOptions, option => option.Key == "windows:feature:psk:PageOutputQuality");
		Assert.Null(quality.DefaultValue);
		Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, Assert.Single(logger.Entries).Level);
	}

	[Fact]
	public async Task GivenFeatureThePrinterLacksWhenPdfSubmittedThenRefusesWithoutRendering()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool
		{
			PrintCapabilities = Encoding.UTF8.GetBytes(Latiprimere.Windows.Tests.PrintTickets.PrintCapabilitiesDocumentTests.Capabilities)
		};

		FakeGdiPrinter gdi = new FakeGdiPrinter();
		FakePdfRasterizer rasterizer = new FakePdfRasterizer();

		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter
		(
			new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(rasterizer)
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
		PrintOptions options = new PrintOptions { VendorOptions = new Dictionary<string, string> { ["windows:feature:psk:JobStapleAllDocuments"] = "psk:StapleTopLeft" } };

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<ArgumentException>(submit);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenFeatureKeyWhenRawSubmittedThenIgnoresItAsRawJobsIgnoreTheDriver()
	{
		// Arrange — raw data goes to the printer unchanged, as with windows:devmode
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		using MemoryStream document = new MemoryStream(new byte[] { 0x1B, 0x40 });
		PrintOptions options = new PrintOptions { VendorOptions = new Dictionary<string, string> { ["windows:feature:psk:PageOutputQuality"] = "psk:High" } };

		// Act
		await printer.SubmitAsync(PrintContent.FromRaw(document), options, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(spool.LastSubmittedBytes);
		Assert.Null(spool.LastPrintCapabilitiesTickets);
	}

	[Fact]
	public async Task GivenWindowsPrinterWhenGetPrintCapabilitiesWithTicketThenReturnsTheDriversDocumentForIt()
	{
		// Arrange — what the catalog returns is an IWindowsPrinter
		FakeWinSpool spool = new FakeWinSpool { PrintCapabilities = Encoding.UTF8.GetBytes("﻿<psf:PrintCapabilities />") };
		IPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));
		IWindowsPrinter windowsPrinter = Assert.IsAssignableFrom<IWindowsPrinter>(printer);

		// Act
		string document = await windowsPrinter.GetPrintCapabilitiesAsync
		(
			Latiprimere.Windows.Tests.Spooler.WindowsVendorOptionsTests.CreatePrintTicket("psk:ISOA5"),
			TestContext.Current.CancellationToken
		);

		// Assert — without the byte order mark
		Assert.Equal("<psf:PrintCapabilities />", document);
		Assert.Equal("Office", windowsPrinter.QueueName);
		Assert.Contains("psk:ISOA5", Encoding.UTF8.GetString(Assert.Single(spool.LastPrintCapabilitiesTickets!)), StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenTicketThatIsNotXmlWhenGetPrintCapabilitiesThenThrowsArgumentExceptionWithoutAskingTheDriver()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));

		// Act
		Func<Task> read = () => printer.GetPrintCapabilitiesAsync("A5 please", TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<ArgumentException>(read);
		Assert.Null(spool.LastPrintCapabilitiesTickets);
	}

	[Fact]
	public async Task GivenDriverWithoutPrintTicketSupportWhenGetDefaultPrintTicketThenThrowsItsPrintException()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool { PrintTicketFailure = new PrintException("No provider.") };
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));

		// Act
		Func<Task> read = () => printer.GetDefaultPrintTicketAsync(TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(read);
		Assert.Equal("No provider.", exception.Message);
	}

	/// <summary>
	/// Capabilities with Letter (DMPAPER 1) and A4 (9), an automatic source (7) and a driver's own tray (257).
	/// </summary>
	/// <returns>The capabilities.</returns>
	private static WindowsCapabilitiesRecord OfficeCapabilities()
	{
		return new WindowsCapabilitiesRecord
		{
			Papers = new[]
			{
				new WindowsPaperRecord { Id = 1, Name = "Letter", WidthTenthsOfMillimetre = 2159, HeightTenthsOfMillimetre = 2794 },
				new WindowsPaperRecord { Id = 9, Name = "A4", WidthTenthsOfMillimetre = 2100, HeightTenthsOfMillimetre = 2970 },
			},
			Bins = new[]
			{
				new WindowsBinRecord { Id = 7, Name = "Automatically Select" },
				new WindowsBinRecord { Id = 257, Name = "Tray 1" },
			}
		};
	}

	[Fact]
	public async Task GivenJobTheSpoolerListsWhenGetJobThenReturnsIt()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetJob(12, new WindowsJobRecord { JobId = 12, Status = JobStatusFlags.Paused });
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));

		// Act
		IPrintJob? job = await printer.GetJobAsync("12", TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(job);
		Assert.Equal("12", job.Id);
		Assert.Equal("windows:Office", job.PrinterId);
		PrintJobStatus status = await job.GetStatusAsync(TestContext.Current.CancellationToken);
		Assert.Equal(PrintJobState.Held, status.State);
	}

	[Theory]
	[InlineData("12")]
	[InlineData("abc")]
	[InlineData("0")]
	[InlineData("-12")]
	[InlineData("ipp:12")]
	public async Task GivenJobTheSpoolerDoesNotListWhenGetJobThenReturnsNull(string jobId)
	{
		// Arrange — jobId has left the queue, or is no spooler job ID (provided by InlineData above); no IPP route
		FakeWinSpool spool = new FakeWinSpool();
		WindowsPrinter printer = WindowsPrinterTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool));

		// Act
		IPrintJob? job = await printer.GetJobAsync(jobId, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(job);
	}

	/// <summary>
	/// Creates a printer named Office, adding it to the context's spooler when that is a fake.
	/// </summary>
	/// <param name="contextBuilder">The context to use.</param>
	/// <param name="driverName">The printer's driver name, or <see langword="null"/> if not known.</param>
	/// <returns>The printer.</returns>
	private static WindowsPrinter CreatePrinter(WindowsPrintContextBuilder contextBuilder, string? driverName = null)
	{
		WindowsPrintContext context = contextBuilder.Build();

		if (context.Spool is FakeWinSpool fakeSpool)
		{
			fakeSpool.AddPrinter(new WindowsPrinterRecord { Name = "Office" });
		}

		PrinterInfo info = new PrinterInfo { Id = "windows:Office", Name = "Office" };

		return new WindowsPrinter(context, "Office", driverName, portName: null, info);
	}
}
