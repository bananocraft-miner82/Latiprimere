using Latiprimere.Rendering.Pdfium.Interop;
using Latiprimere.Testing.Fixtures;
using Xunit;

namespace Latiprimere.Rendering.Pdfium.Tests;

public sealed class PdfiumDocumentTests
{
	/// <summary>Width of an A4 page rendered at 72 DPI (one pixel per point).</summary>
	private const int PageWidth = 595;

	/// <summary>Height of an A4 page rendered at 72 DPI.</summary>
	private const int PageHeight = 842;

	[Fact]
	public void GivenWholePageBandWhenRenderBandThenDrawsContentInPlaceOnWhite()
	{
		// Arrange
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();

		RasterBand band = RasterBand.Create
		(
			PdfiumDocumentTests.PageWidth,
			PdfiumDocumentTests.PageHeight,
			0,
			PdfiumDocumentTests.PageHeight
		);

		byte[] pixels = new byte[band.ByteCount];

		// Act
		document.RenderBand(0, band, pixels);

		// Assert — the corner is white; inside the blue rectangle (x 72-222, y 462-542 from the top) is
		// RGB(51,102,204)
		Assert.Equal((255, 255, 255), PdfiumDocumentTests.ReadPixel(pixels, band, 5, 5));
		(int red, int green, int blue) = PdfiumDocumentTests.ReadPixel(pixels, band, 150, 500);
		Assert.InRange(red, 45, 57);
		Assert.InRange(green, 96, 108);
		Assert.InRange(blue, 198, 210);

		// Assert — checks near the right edge catch a wrong pixel format, which shifts content left and leaves
		// the end of each row unwritten: the rule (x 72-523, y 592) ends where it should, and the margin is white
		Assert.Equal((0, 0, 0), PdfiumDocumentTests.ReadPixel(pixels, band, 515, 592));
		Assert.Equal((255, 255, 255), PdfiumDocumentTests.ReadPixel(pixels, band, 535, 592));
		Assert.Equal((255, 255, 255), PdfiumDocumentTests.ReadPixel(pixels, band, 590, 400));
	}

	[Fact]
	public void GivenPageRenderedInBandsWhenComparedWithWholePageThenPixelsAreIdentical()
	{
		// Arrange — the whole page once, then the same page as four uneven bands
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();

		RasterBand wholePage = RasterBand.Create
		(
			PdfiumDocumentTests.PageWidth,
			PdfiumDocumentTests.PageHeight,
			0,
			PdfiumDocumentTests.PageHeight
		);

		byte[] expected = new byte[wholePage.ByteCount];
		document.RenderBand(0, wholePage, expected);
		int[] bandTops = new int[] { 0, 200, 455, 700, PdfiumDocumentTests.PageHeight };
		byte[] banded = new byte[wholePage.ByteCount];

		// Act
		for (int bandIndex = 0; bandIndex < bandTops.Length - 1; bandIndex++)
		{
			RasterBand band = RasterBand.Create
			(
				PdfiumDocumentTests.PageWidth,
				PdfiumDocumentTests.PageHeight,
				bandTops[bandIndex],
				bandTops[bandIndex + 1] - bandTops[bandIndex]
			);

			document.RenderBand(0, band, banded.AsSpan((int)(bandTops[bandIndex] * (long)wholePage.Stride)));
		}

		// Assert
		Assert.True(expected.AsSpan().SequenceEqual(banded), "Banded rendering differs from whole-page rendering.");
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(1)]
	public void GivenPageIndexOutOfRangeWhenGetPageSizeThenThrowsArgumentOutOfRangeException(int pageIndex)
	{
		// Arrange — a page index before the first or after the last page (provided by InlineData above)
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();

		// Act
		Action getPageSize = () => document.GetPageSize(pageIndex);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(getPageSize);
	}

	[Theory]
	[InlineData(0, 842, 800, 100)]   // runs past the bottom of the page
	[InlineData(0, 842, -1, 10)]     // starts above the page
	[InlineData(0, 842, 0, 0)]       // no rows
	[InlineData(-5, 842, 0, 10)]     // negative page width
	public void GivenBandOutsidePageWhenRenderBandThenThrowsArgumentOutOfRangeException
	(
		int pageWidthAdjustment,
		int pageHeight,
		int top,
		int height
	)
	{
		// Arrange — band geometry that doesn't fit the page (provided by InlineData above)
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();
		int pageWidth = pageWidthAdjustment < 0 ? pageWidthAdjustment : PdfiumDocumentTests.PageWidth;
		RasterBand band = new RasterBand(pageWidth, pageHeight, top, height, PdfiumDocumentTests.PageWidth * 4);
		byte[] pixels = new byte[PdfiumDocumentTests.PageWidth * 4 * 100];

		// Act
		Action render = () => document.RenderBand(0, band, pixels);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(render);
	}

	[Fact]
	public void GivenStrideNarrowerThanPageWhenRenderBandThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();

		RasterBand band = new RasterBand
		(
			PdfiumDocumentTests.PageWidth,
			PdfiumDocumentTests.PageHeight,
			0,
			10,
			PdfiumDocumentTests.PageWidth
		);

		byte[] pixels = new byte[PdfiumDocumentTests.PageWidth * 4 * 10];

		// Act
		Action render = () => document.RenderBand(0, band, pixels);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(render);
	}

	[Fact]
	public void GivenPageSoWideItsRowBytesOverflowAnIntWhenRenderBandThenThrowsArgumentOutOfRangeException()
	{
		// Arrange — 600 million pixels across need 2.4 billion bytes a row, past int.MaxValue, so a stride of 4 bytes
		// must be refused rather than handed to PDFium
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();
		RasterBand band = new RasterBand(600_000_000, 1, 0, 1, 4);
		byte[] pixels = new byte[band.ByteCount];

		// Act
		Action render = () => document.RenderBand(0, band, pixels);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(render);
	}

	[Fact]
	public void GivenBufferTooSmallWhenRenderBandThenThrowsArgumentException()
	{
		// Arrange
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();
		RasterBand band = RasterBand.Create(PdfiumDocumentTests.PageWidth, PdfiumDocumentTests.PageHeight, 0, 10);
		byte[] pixels = new byte[band.ByteCount - 1];

		// Act
		Action render = () => document.RenderBand(0, band, pixels);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(render);
		Assert.Equal("pixels", exception.ParamName);
	}

	[Fact]
	public void GivenDisposedDocumentWhenRenderBandThenThrowsObjectDisposedException()
	{
		// Arrange
		IRasterDocument document = PdfiumDocumentTests.OpenOnePage();
		document.Dispose();
		RasterBand band = RasterBand.Create(PdfiumDocumentTests.PageWidth, PdfiumDocumentTests.PageHeight, 0, 1);

		// Act
		Action render = () => document.RenderBand(0, band, new byte[band.ByteCount]);

		// Assert
		Assert.Throws<ObjectDisposedException>(render);
	}

	[Fact]
	public void GivenDocumentDisposedTwiceWhenDisposedThenDoesNotThrow()
	{
		// Arrange
		IRasterDocument document = PdfiumDocumentTests.OpenOnePage();
		document.Dispose();

		// Act
		Exception? exception = Record.Exception(document.Dispose);

		// Assert
		Assert.Null(exception);
	}

	[Fact]
	public void GivenPdfiumLockFreeWhenReleasedWithoutWaitingThenClosesTheDocumentAtOnce()
	{
		// Arrange
		PdfiumDocument document = (PdfiumDocument)PdfiumDocumentTests.OpenOnePage();
		RasterBand band = RasterBand.Create(PdfiumDocumentTests.PageWidth, PdfiumDocumentTests.PageHeight, 0, 1);

		// Act — holding the lock, which is reentrant, so tests rendering in parallel can't take it in between
		bool isReleased;

		lock (PdfiumLibrary.Lock)
		{
			isReleased = document.ReleaseWithoutWaiting();
		}

		// Assert — closed, and a later Dispose has nothing left to free
		Assert.True(isReleased);
		Assert.Throws<ObjectDisposedException>(() => document.RenderBand(0, band, new byte[band.ByteCount]));
		Assert.Null(Record.Exception(document.Dispose));
	}

	[Fact]
	public void GivenPdfiumLockHeldElsewhereWhenReleasedWithoutWaitingThenReturnsAtOnceAndTheNextCallClosesIt()
	{
		// Arrange — another thread holds the lock, as one rendering a large page would
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;
		PdfiumDocument document = (PdfiumDocument)PdfiumDocumentTests.OpenOnePage();
		using ManualResetEventSlim isHeld = new ManualResetEventSlim();
		using ManualResetEventSlim mayRelease = new ManualResetEventSlim();

		Thread holder = new Thread(() =>
		{
			lock (PdfiumLibrary.Lock)
			{
				isHeld.Set();
				mayRelease.Wait(TimeSpan.FromSeconds(30), cancellationToken);
			}
		});

		holder.Start();
		isHeld.Wait(TimeSpan.FromSeconds(30), cancellationToken);
		bool isReleased;
		int pendingWhileHeld;

		try
		{
			// Act
			isReleased = document.ReleaseWithoutWaiting();
			pendingWhileHeld = PdfiumLibrary.PendingReleaseCount;
		}
		finally
		{
			mayRelease.Set();
			holder.Join();
		}

		// The next PDFium call, from any document, closes what was left
		using (IRasterDocument next = PdfiumDocumentTests.OpenOnePage())
		{
			_ = next.GetPageSize(0);
		}

		// Assert — it didn't wait, it was left for later, and the next call closed it
		Assert.False(isReleased);
		Assert.True(pendingWhileHeld >= 1);
		Assert.Equal(0, PdfiumLibrary.PendingReleaseCount);
		Assert.Null(Record.Exception(document.Dispose));
	}

	[Fact]
	public void GivenGreyBandWhenRenderBandThenItIsTheColourRenderingInGrey()
	{
		// Arrange
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();
		RasterBand colourBand = RasterBand.Create(PdfiumDocumentTests.PageWidth, PdfiumDocumentTests.PageHeight, 0, PdfiumDocumentTests.PageHeight);
		RasterBand greyBand = colourBand with { Stride = PdfiumDocumentTests.PageWidth, PixelFormat = RasterPixelFormat.Gray8 };
		byte[] colour = new byte[colourBand.ByteCount];
		byte[] grey = new byte[greyBand.ByteCount];

		// Act
		document.RenderBand(0, colourBand, colour);
		document.RenderBand(0, greyBand, grey);

		// Assert: a quarter of the bytes; solid colour the grey of its luma, as RasterPixels weighs it; and the page
		// as a whole alike, though PDFium smooths the edges of text and scales the image a little differently in grey
		Assert.Equal(colourBand.ByteCount / 4, greyBand.ByteCount);
		long total = 0;

		for (int pixel = 0; pixel < grey.Length; pixel++)
		{
			int offset = pixel * 4;
			int luma = ((77 * colour[offset + 2]) + (150 * colour[offset + 1]) + (29 * colour[offset]) + 128) >> 8;
			total += Math.Abs(luma - grey[pixel]);
		}

		Assert.InRange(total / (double)grey.Length, 0, 4.0);
		Assert.InRange(grey[(500 * PdfiumDocumentTests.PageWidth) + 150], 94, 102); // The blue rectangle, RGB(51,102,204)
		Assert.Equal(255, grey[(5 * PdfiumDocumentTests.PageWidth) + 5]);
		Assert.Equal(0, grey[(592 * PdfiumDocumentTests.PageWidth) + 515]);
	}

	[Fact]
	public void GivenSmoothingOffWhenBlackLineArtRenderedInGreyThenThereIsNoGrey()
	{
		// Arrange: black text, a diagonal line and a circle, whose edges are grey when smoothed
		using FileStream content = TestDocuments.Open(TestDocuments.LineArt);
		using IRasterDocument document = new PdfiumRasterizer().Open(content);
		RasterBand band = RasterBand.Create(PdfiumDocumentTests.PageWidth, PdfiumDocumentTests.PageHeight, 0, PdfiumDocumentTests.PageHeight, RasterPixelFormat.Gray8);
		RasterRenderOptions sharp = new RasterRenderOptions { SmoothText = false, SmoothLines = false, SmoothImages = false };
		byte[] smoothed = new byte[band.ByteCount];
		byte[] unsmoothed = new byte[band.ByteCount];

		// Act
		document.RenderBand(0, band, smoothed);
		document.RenderBand(0, band, unsmoothed, sharp);

		// Assert
		Assert.True(PdfiumDocumentTests.CountGrey(smoothed) > 500, "smoothed edges are grey");
		Assert.Equal(0, PdfiumDocumentTests.CountGrey(unsmoothed));
		Assert.True(unsmoothed.Count(value => value == 0) > 10_000, "the circle and text are black");
	}

	[Fact]
	public void GivenFormatThisVersionDoesNotKnowWhenRenderBandThenThrowsNotSupportedException()
	{
		// Arrange
		using IRasterDocument document = PdfiumDocumentTests.OpenOnePage();
		RasterBand band = RasterBand.Create(10, 10, 0, 10) with { PixelFormat = (RasterPixelFormat)99 };

		// Act
		Action render = () => document.RenderBand(0, band, new byte[1000]);

		// Assert
		Assert.Throws<NotSupportedException>(render);
	}

	/// <summary>
	/// Counts the pixels that are neither black nor white.
	/// </summary>
	/// <param name="grey">Pixels, a byte of grey each.</param>
	/// <returns>The count.</returns>
	private static int CountGrey(byte[] grey)
	{
		return grey.Count(value => value is not 0 and not 255);
	}

	/// <summary>
	/// Opens the one-page mixed-content test document.
	/// </summary>
	/// <returns>The document. The caller disposes it.</returns>
	private static IRasterDocument OpenOnePage()
	{
		using FileStream content = TestDocuments.Open(TestDocuments.MixedOnePage);

		return new PdfiumRasterizer().Open(content);
	}

	/// <summary>
	/// Reads one pixel from BGRx data.
	/// </summary>
	/// <param name="pixels">The pixel data.</param>
	/// <param name="band">The band the data holds.</param>
	/// <param name="x">The column.</param>
	/// <param name="y">The row within the band.</param>
	/// <returns>The red, green and blue values.</returns>
	private static (int Red, int Green, int Blue) ReadPixel(byte[] pixels, RasterBand band, int x, int y)
	{
		int offset = (y * band.Stride) + (x * RasterBand.GetBytesPerPixel(RasterPixelFormat.Bgra32));

		return (pixels[offset + 2], pixels[offset + 1], pixels[offset]);
	}
}
