using System.Text;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using Xunit;

namespace Latiprimere.Windows.Tests;

/// <summary>
/// Plain text through the Windows spooler (ADR 0048): laid out as a PDF on the job's paper, then printed through the
/// driver as PDFs are.
/// </summary>
public sealed class WindowsPrinterTextTests
{
	[Theory]
	[InlineData(PageOrientation.PrinterDefault, PageOrientation.Portrait, "/MediaBox [0 0 595.2756 841.8898]")]
	[InlineData(PageOrientation.Landscape, PageOrientation.Landscape, "/MediaBox [0 0 841.8898 595.2756]")]
	public async Task GivenTextWhenSubmittedThenAPdfOnTheDriversPaperIsPrintedInItsOrientation(PageOrientation asked, PageOrientation printed, string mediaBox)
	{
		// Arrange — the driver's default is A4 (provided by InlineData above)
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterTextTests.Capabilities());
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		FakePdfRasterizer rasterizer = new FakePdfRasterizer();
		WindowsPrinter printer = WindowsPrinterTextTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(rasterizer));

		// Act
		await printer.SubmitAsync(PrintContent.FromText("Receipt"), new PrintOptions { Orientation = asked }, TestContext.Current.CancellationToken);

		// Assert
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(gdi.LastRequest);
		Assert.Equal(printed, request.Options.Orientation);
		Assert.Contains(mediaBox, Encoding.Latin1.GetString(rasterizer.LastContent!), StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenNoRasterizerWhenTextIsSubmittedThenNotSupportedNamingTextAndNothingIsPrinted()
	{
		// Arrange
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		WindowsPrinter printer = WindowsPrinterTextTests.CreatePrinter(new WindowsPrintContextBuilder().WithGdi(gdi));

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromText("Hello"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("images and text", exception.Message, StringComparison.Ordinal);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenACharacterCourierCantShowWhenTextIsSubmittedThenNotSupportedAndNothingIsPrinted()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterTextTests.Capabilities());
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		WindowsPrinter printer = WindowsPrinterTextTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer()));

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromText("日本"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAnyAsync<NotSupportedException>(submit);
		Assert.Contains("U+65E5", exception.Message, StringComparison.Ordinal);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenATextOnlyDriverWhenTextIsSubmittedThenRefusedAsAPdfWouldBeSuggestingRawData()
	{
		// Arrange — a text-only driver takes text as raw data, not as pictures of pages
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterTextTests.Capabilities());
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		WindowsPrinter printer = WindowsPrinterTextTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer()), "Generic / Text Only");

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromText("Hello"), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("raw data", exception.Message, StringComparison.Ordinal);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenATextOnlyDriverWhenADamagedImageIsSubmittedThenItIsRefusedForTheDriverBeforeTheImageIsRead()
	{
		// Arrange — an image that couldn't be read, so reading it would throw PrintException
		FakePdfRasterizer rasterizer = new FakePdfRasterizer();
		WindowsPrinter printer = WindowsPrinterTextTests.CreatePrinter(new WindowsPrintContextBuilder().WithPdfRasterizer(rasterizer), "Generic / Text Only");

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream([1, 2, 3])), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("Generic / Text Only", exception.Message, StringComparison.Ordinal);
		Assert.Null(rasterizer.LastContent);
	}

	/// <summary>
	/// The office printer's papers, A4 the default.
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
	/// Creates the printer under test, the queue "Office".
	/// </summary>
	/// <param name="contextBuilder">The context.</param>
	/// <param name="driverName">Its driver, or <see langword="null"/>.</param>
	/// <returns>The printer.</returns>
	private static WindowsPrinter CreatePrinter(WindowsPrintContextBuilder contextBuilder, string? driverName = null)
	{
		WindowsPrintContext context = contextBuilder.Build();

		if (context.Spool is FakeWinSpool fakeSpool)
		{
			fakeSpool.AddPrinter(new WindowsPrinterRecord { Name = "Office" });
		}

		return new WindowsPrinter(context, "Office", driverName, portName: null, new PrinterInfo { Id = "windows:Office", Name = "Office" });
	}
}
