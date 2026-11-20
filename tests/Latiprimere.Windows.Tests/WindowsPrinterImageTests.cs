using System.Text;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using Xunit;

namespace Latiprimere.Windows.Tests;

/// <summary>
/// JPEG and PNG images through the Windows spooler (ADR 0047): placed on a page of the job's paper as a PDF, then
/// printed through the driver as PDFs are.
/// </summary>
public sealed class WindowsPrinterImageTests
{
	[Fact]
	public async Task GivenAWideJpegWhenSubmittedThenALandscapePdfOnTheDriversDefaultPaperIsPrintedLandscape()
	{
		// Arrange — the driver's default is A4
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterImageTests.Capabilities(defaultPaperId: 9));
		FakeGdiPrinter gdi = new FakeGdiPrinter { NextJobId = 8 };
		FakePdfRasterizer rasterizer = new FakePdfRasterizer { PageCount = 1 };
		WindowsPrinter printer = WindowsPrinterImageTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(rasterizer));

		// Act
		IPrintJob job = await printer.SubmitAsync
		(
			PrintContent.FromJpeg(new MemoryStream(WindowsPrinterImageTests.Jpeg(width: 400, height: 300))),
			new PrintOptions { JobName = "Photo", Scaling = ScalingMode.Fill },
			TestContext.Current.CancellationToken
		);

		// Assert — the driver turns its paper to match the page; the image is laid out already
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(gdi.LastRequest);
		Assert.Equal(("Photo", PageOrientation.Landscape, ScalingMode.PrinterDefault), (request.DocumentName, request.Options.Orientation, request.Options.Scaling));
		Assert.Null(request.PaperId);
		Assert.Equal("8", job.Id);

		string pdf = Encoding.Latin1.GetString(rasterizer.LastContent!);
		Assert.StartsWith("%PDF-1.7", pdf, StringComparison.Ordinal);
		Assert.Contains("/MediaBox [0 0 841.8898 595.2756]", pdf, StringComparison.Ordinal);
		Assert.Contains("/Filter /DCTDecode", pdf, StringComparison.Ordinal);
		Assert.True(rasterizer.LastDocument?.IsDisposed);
	}

	[Fact]
	public async Task GivenAPaperAndOrientationChosenWhenAnImageIsSubmittedThenThePdfIsThatPaperThatWay()
	{
		// Arrange — US Letter, portrait, for a wide image
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterImageTests.Capabilities(defaultPaperId: 9));
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		FakePdfRasterizer rasterizer = new FakePdfRasterizer();
		WindowsPrinter printer = WindowsPrinterImageTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(rasterizer));

		PrintOptions options = new PrintOptions
		{
			MediaSize   = new MediaSize { Name = "na_letter_8.5x11in", DisplayName = "Letter", WidthMillimeters = 215.9, HeightMillimeters = 279.4 },
			Orientation = PageOrientation.Portrait
		};

		// Act
		await printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream(WindowsPrinterImageTests.Jpeg(400, 300))), options, TestContext.Current.CancellationToken);

		// Assert — the driver is still asked for Letter by number
		GdiPrintRequest request = Assert.IsType<GdiPrintRequest>(gdi.LastRequest);
		Assert.Equal(((short)1, PageOrientation.Portrait), (request.PaperId, request.Options.Orientation));
		Assert.Contains("/MediaBox [0 0 612 792]", Encoding.Latin1.GetString(rasterizer.LastContent!), StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenADriverWithoutADefaultPaperWhenAnImageIsSubmittedThenTheRegionsPaperIsUsed()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterImageTests.Capabilities(defaultPaperId: null));
		FakePdfRasterizer rasterizer = new FakePdfRasterizer();
		WindowsPrinter printer = WindowsPrinterImageTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithPdfRasterizer(rasterizer));

		// Act
		await printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream(WindowsPrinterImageTests.Jpeg(300, 400))), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — A4 or Letter, portrait for a tall image
		string expected = System.Globalization.RegionInfo.CurrentRegion.IsMetric ? "/MediaBox [0 0 595.2756 841.8898]" : "/MediaBox [0 0 612 792]";
		Assert.Contains(expected, Encoding.Latin1.GetString(rasterizer.LastContent!), StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenNoRasterizerWhenAnImageIsSubmittedThenNotSupportedNamingImagesAndNothingIsPrinted()
	{
		// Arrange
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		WindowsPrinter printer = WindowsPrinterImageTests.CreatePrinter(new WindowsPrintContextBuilder().WithGdi(gdi));

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromPng(new MemoryStream([1, 2, 3])), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("images", exception.Message, StringComparison.Ordinal);
		Assert.Null(gdi.LastRequest);
	}

	[Fact]
	public async Task GivenADamagedImageWhenSubmittedThenThrowsPrintExceptionAndNothingIsPrinted()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterImageTests.Capabilities(defaultPaperId: 9));
		FakeGdiPrinter gdi = new FakeGdiPrinter();
		WindowsPrinter printer = WindowsPrinterImageTests.CreatePrinter(new WindowsPrintContextBuilder().WithSpool(spool).WithGdi(gdi).WithPdfRasterizer(new FakePdfRasterizer()));

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromJpeg(new MemoryStream([0xFF, 0xD8, 0xFF])), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Contains("JPEG", exception.Message, StringComparison.Ordinal);
		Assert.Null(gdi.LastRequest);
	}

	[Theory]
	[InlineData(true, null, new[] { DocumentFormat.Pdf, DocumentFormat.Raw, DocumentFormat.Jpeg, DocumentFormat.Png, DocumentFormat.Text })]
	[InlineData(false, null, new[] { DocumentFormat.Raw })]
	[InlineData(true, "Generic / Text Only", new[] { DocumentFormat.Raw })]
	public async Task GivenARasterizerAndDriverWhenCapabilitiesAreReadThenTheFormatsFollow(bool withRasterizer, string? driverName, DocumentFormat[] expected)
	{
		// Arrange — PDFs, images and text need a rasteriser and a driver that prints more than text (provided by
		// InlineData above)
		FakeWinSpool spool = new FakeWinSpool();
		spool.SetCapabilities("Office", WindowsPrinterImageTests.Capabilities(defaultPaperId: 9));
		WindowsPrintContextBuilder builder = new WindowsPrintContextBuilder().WithSpool(spool);

		if (withRasterizer)
		{
			builder.WithPdfRasterizer(new FakePdfRasterizer());
		}

		WindowsPrinter printer = WindowsPrinterImageTests.CreatePrinter(builder, driverName);

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(expected, capabilities.DocumentFormats);
	}

	/// <summary>
	/// Builds a JPEG's headers, which is all the converter reads: start of image, a colour frame, a scan's start.
	/// </summary>
	/// <param name="width">Its width.</param>
	/// <param name="height">Its height.</param>
	/// <returns>The bytes.</returns>
	internal static byte[] Jpeg(int width, int height)
	{
		return
		[
			0xFF, 0xD8,
			0xFF, 0xC0, 0, 17, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0,
			0xFF, 0xDA, 0, 2,
		];
	}

	/// <summary>
	/// The office printer's papers, with a default or none.
	/// </summary>
	/// <param name="defaultPaperId">The default paper's DMPAPER number, or <see langword="null"/>.</param>
	/// <returns>The capabilities.</returns>
	private static WindowsCapabilitiesRecord Capabilities(short? defaultPaperId)
	{
		return new WindowsCapabilitiesRecord
		{
			Papers =
			[
				new WindowsPaperRecord { Id = 1, Name = "Letter", WidthTenthsOfMillimetre = 2159, HeightTenthsOfMillimetre = 2794 },
				new WindowsPaperRecord { Id = 9, Name = "A4", WidthTenthsOfMillimetre = 2100, HeightTenthsOfMillimetre = 2970 },
			],
			DefaultPaperId = defaultPaperId
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
