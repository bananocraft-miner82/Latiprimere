using System.Buffers.Binary;
using Latiprimere.Ipp.Raster;
using Latiprimere.Rendering;
using Latiprimere.Testing.Raster;
using Xunit;

namespace Latiprimere.Ipp.Tests.Raster;

public sealed class RasterDocumentEncoderTests
{
	/// <summary>A4, portrait.</summary>
	private static readonly MediaSize A4 = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimeters = 210, HeightMillimeters = 297 };

	/// <summary>An A4 page in points.</summary>
	private static readonly PageSize PortraitA4 = new PageSize(595.28, 841.89);

	/// <summary>The same, turned: a landscape page.</summary>
	private static readonly PageSize LandscapeA4 = new PageSize(841.89, 595.28);

	[Theory]
	[InlineData(210, 297, 600, 4960, 7015)]
	[InlineData(210, 297, 300, 2480, 3507)]
	[InlineData(215.9, 279.4, 600, 5100, 6600)]
	[InlineData(148, 210, 300, 1748, 2480)]
	public void GivenPaperWhenConvertedToPixelsThenRoundsDownFromHundredthsOfAMillimetreAsLibcupsDoes(double width, double height, int dpi, int expectedWidth, int expectedHeight)
	{
		// Arrange (provided by InlineData above)

		// Act
		(int pixelsWide, int pixelsHigh) = (RasterDocumentEncoder.ToPixels(width, dpi), RasterDocumentEncoder.ToPixels(height, dpi));

		// Assert
		Assert.Equal((expectedWidth, expectedHeight), (pixelsWide, pixelsHigh));
	}

	[Fact]
	public void GivenPortraitPageWhenEncodedAsPwgGreyThenHeaderAndPixelsAreThePage()
	{
		// Arrange — at 72 dpi a point is a pixel, so A4 is 595 × 841, rounded down as libcups does
		QuadrantDocument document = new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4);
		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Gray8, dpi: 72);

		// Act
		RasterDocument raster = RasterDocumentEncoderTests.EncodeAndRead(document, settings);

		// Assert
		RasterPage page = Assert.Single(raster.Pages);
		Assert.Equal("image/pwg-raster", raster.Format);
		Assert.Equal("PwgRaster", page.MediaClass);
		Assert.Equal("iso_a4_210x297mm", page.PageSizeName);
		Assert.Equal((595, 841), page.PageSizePoints);
		Assert.Equal((595, 841), (page.Width, page.Height));
		Assert.Equal((72, 72), (page.DpiX, page.DpiY));
		Assert.Equal((8, 8, 595, 18, 1), (page.BitsPerPixel, page.BitsPerColor, page.BytesPerLine, page.ColorSpace, page.NumColors));
		Assert.Equal(1, page.TotalPages);
		Assert.Equal((1, 1), (page.CrossFeedTransform, page.FeedTransform));
		Assert.False(page.Duplex);

		Assert.Equal(QuadrantDocument.Black, page.GetGrey(20, 20));
		Assert.Equal(QuadrantDocument.Red, page.GetGrey(575, 20));
		Assert.Equal(QuadrantDocument.Green, page.GetGrey(20, 820));
		Assert.Equal(QuadrantDocument.White, page.GetGrey(575, 820));
	}

	[Fact]
	public void GivenLandscapePageOnPortraitPaperWhenEncodedThenItIsTurnedAQuarterAnticlockwise()
	{
		// Arrange — landscape is printed with the page's top to the left
		QuadrantDocument document = new QuadrantDocument(RasterDocumentEncoderTests.LandscapeA4);
		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Gray8, dpi: 72);

		// Act
		RasterPage page = Assert.Single(RasterDocumentEncoderTests.EncodeAndRead(document, settings).Pages);

		// Assert — the page's top-left (black) is now bottom left, its top-right (red) top left
		Assert.All(document.Bands, band => Assert.Equal(RasterRotation.Counterclockwise90, band.Rotation));
		Assert.Equal((595, 841), (page.Width, page.Height));
		Assert.Equal(QuadrantDocument.Red, page.GetGrey(20, 20));
		Assert.Equal(QuadrantDocument.Black, page.GetGrey(20, 820));
		Assert.Equal(QuadrantDocument.White, page.GetGrey(575, 20));
		Assert.Equal(QuadrantDocument.Green, page.GetGrey(575, 820));
	}

	[Theory]
	[InlineData(PageOrientation.Portrait, RasterRotation.None)]
	[InlineData(PageOrientation.Landscape, RasterRotation.Counterclockwise90)]
	[InlineData(PageOrientation.PrinterDefault, RasterRotation.None)]
	public void GivenOrientationAskedForWhenPortraitPageEncodedThenItDecidesTheTurn(PageOrientation orientation, RasterRotation expected)
	{
		// Arrange — a portrait page asked to print landscape is turned and shrunk to fit across (provided by InlineData above)
		QuadrantDocument document = new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4);
		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Gray8, dpi: 72) with { Orientation = orientation };

		// Act
		RasterPage page = Assert.Single(RasterDocumentEncoderTests.EncodeAndRead(document, settings).Pages);

		// Assert
		Assert.All(document.Bands, band => Assert.Equal(expected, band.Rotation));

		if (expected == RasterRotation.Counterclockwise90)
		{
			// 842 × 595 turned is 595 wide and 421 high: centred, white above and below
			Assert.Equal((0, 210, 595, 631), page.ImageBox);
			Assert.Equal(QuadrantDocument.White, page.GetGrey(300, 100));
		}
	}

	[Fact]
	public void GivenSmallPageWhenEncodedThenItIsCentredAtItsOwnSizeOnWhite()
	{
		// Arrange — a 400-point square fits, so it isn't enlarged
		QuadrantDocument document = new QuadrantDocument(new PageSize(400, 400));
		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Gray8, dpi: 72);

		// Act
		RasterPage page = Assert.Single(RasterDocumentEncoderTests.EncodeAndRead(document, settings).Pages);

		// Assert — (595 − 400) / 2 = 97 and (841 − 400) / 2 = 220
		Assert.Equal((97, 220, 497, 620), page.ImageBox);
		Assert.Equal(QuadrantDocument.White, page.GetGrey(50, 50));
		Assert.Equal(QuadrantDocument.Black, page.GetGrey(100, 225));
		Assert.Equal(QuadrantDocument.White, page.GetGrey(300, 700));
	}

	[Theory]
	[InlineData(DuplexMode.LongEdge, (int)RasterBackSide.Flipped, 1, -1, QuadrantDocument.Green)]
	[InlineData(DuplexMode.ShortEdge, (int)RasterBackSide.Flipped, -1, 1, QuadrantDocument.Red)]
	[InlineData(DuplexMode.LongEdge, (int)RasterBackSide.Rotated, -1, -1, QuadrantDocument.White)]
	[InlineData(DuplexMode.ShortEdge, (int)RasterBackSide.Rotated, 1, 1, QuadrantDocument.Black)]
	[InlineData(DuplexMode.ShortEdge, (int)RasterBackSide.ManualTumble, -1, -1, QuadrantDocument.White)]
	[InlineData(DuplexMode.LongEdge, (int)RasterBackSide.ManualTumble, 1, 1, QuadrantDocument.Black)]
	[InlineData(DuplexMode.LongEdge, (int)RasterBackSide.Normal, 1, 1, QuadrantDocument.Black)]
	public void GivenTwoSidedJobWhenEncodedThenBackSidesAreTurnedAsThePrinterAsks
	(
		DuplexMode     duplex,
		int            backSide,
		int            crossFeed,
		int            feed,
		int            topLeftGrey
	)
	{
		// Arrange — libcups' table for pwg-raster-document-sheet-back (provided by InlineData above)
		QuadrantDocument document = new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4, RasterDocumentEncoderTests.PortraitA4);

		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Gray8, dpi: 72) with
		{
			Duplex   = duplex,
			BackSide = (RasterBackSide)backSide
		};

		// Act
		RasterDocument raster = RasterDocumentEncoderTests.EncodeAndRead(document, settings);

		// Assert — the front is never turned; the back's header says what was done, and the pixels show it
		Assert.Equal(2, raster.Pages.Count);
		Assert.All(raster.Pages, page => Assert.True(page.Duplex));
		Assert.All(raster.Pages, page => Assert.Equal(duplex == DuplexMode.ShortEdge, page.Tumble));
		Assert.Equal((1, 1), (raster.Pages[0].CrossFeedTransform, raster.Pages[0].FeedTransform));
		Assert.Equal(QuadrantDocument.Black, raster.Pages[0].GetGrey(20, 20));
		Assert.Equal((crossFeed, feed), (raster.Pages[1].CrossFeedTransform, raster.Pages[1].FeedTransform));
		Assert.Equal(topLeftGrey, raster.Pages[1].GetGrey(20, 20));
	}

	[Fact]
	public void GivenSmallBandLimitWhenEncodedThenTheDocumentIsTheSameAsInOneBand()
	{
		// Arrange — bands of a few lines, mirrored back side included, must join up exactly
		QuadrantDocument oneBand = new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4, RasterDocumentEncoderTests.PortraitA4);
		QuadrantDocument manyBands = new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4, RasterDocumentEncoderTests.PortraitA4);

		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Gray8, dpi: 72) with
		{
			Duplex   = DuplexMode.LongEdge,
			BackSide = RasterBackSide.Rotated
		};

		// Act
		byte[] whole = RasterDocumentEncoderTests.Encode(oneBand, settings);
		byte[] banded = RasterDocumentEncoderTests.Encode(manyBands, settings with { MaxBandBytes = 595 * 4 * 7 });

		// Assert
		Assert.Equal(2, oneBand.Bands.Count);
		Assert.True(manyBands.Bands.Count > 200, $"{manyBands.Bands.Count} bands");
		Assert.All(manyBands.Bands, band => Assert.True(band.Height <= 7));
		Assert.Equal(whole, banded);
	}

	[Fact]
	public void GivenBlackAndWhiteWhenEncodedThenGreysAreDitheredIntoOneBitPixels()
	{
		// Arrange
		QuadrantDocument document = new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4);
		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Black1, dpi: 72);

		// Act
		RasterPage page = Assert.Single(RasterDocumentEncoderTests.EncodeAndRead(document, settings).Pages);

		// Assert — black is all ink, white none, and red (grey 77, about 70 % ink) a pattern near that
		Assert.Equal((1, 1, 75, 3), (page.BitsPerPixel, page.BitsPerColor, page.BytesPerLine, page.ColorSpace));
		Assert.Equal(0, RasterDocumentEncoderTests.InkIn(page, 0, 0, 290, 400));
		Assert.Equal(255, RasterDocumentEncoderTests.InkIn(page, 300, 430, 590, 840));
		double redInk = 1 - (RasterDocumentEncoderTests.InkIn(page, 300, 0, 590, 400) / 255.0);
		Assert.InRange(redInk, 0.66, 0.74);
	}

	[Fact]
	public void GivenColourWhenEncodedAsAppleRasterThenHeaderAndPixelsAreSrgb()
	{
		// Arrange
		QuadrantDocument document = new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4, RasterDocumentEncoderTests.PortraitA4);

		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.AppleRaster, RasterColorSpace.Srgb8, dpi: 72) with
		{
			Duplex       = DuplexMode.LongEdge,
			PrintQuality = 5
		};

		// Act
		byte[] bytes = RasterDocumentEncoderTests.Encode(document, settings);
		RasterDocument raster = RasterDocumentReader.Read(bytes);

		// Assert — UNIRAST, two pages, then 32-byte headers: 24 bits, sRGB, long-edge, high quality
		Assert.Equal("UNIRAST\0"u8.ToArray(), bytes[..8]);
		Assert.Equal(2u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
		Assert.Equal("image/urf", raster.Format);
		RasterPage page = raster.Pages[0];
		Assert.Equal(new byte[] { 24, 1, 3, 5 }, page.RawHeader[..4]);
		Assert.Equal((595, 841, 72), (page.Width, page.Height, page.DpiX));
		Assert.Equal(new byte[] { 255, 0, 0 }, page.Lines[20][(575 * 3)..((575 * 3) + 3)]);
		Assert.Equal(new byte[] { 0, 255, 0 }, page.Lines[820][(20 * 3)..((20 * 3) + 3)]);
	}

	[Fact]
	public void GivenOneBitAppleRasterWhenEncodedThenThrowsArgumentException()
	{
		// Arrange — Apple raster has no 1-bit form
		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.AppleRaster, RasterColorSpace.Black1, dpi: 72);

		// Act
		Action encode = () => RasterDocumentEncoderTests.Encode(new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4), settings);

		// Assert
		Assert.Throws<ArgumentException>(encode);
	}

	[Fact]
	public void GivenBlankPageAt600DpiWhenEncodedThenRepeatedLinesKeepItSmall()
	{
		// Arrange — 4,961 × 7,016 white pixels, 34 MB unencoded
		BlankDocument document = new BlankDocument(RasterDocumentEncoderTests.PortraitA4);
		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Gray8, dpi: 600);

		// Act
		byte[] bytes = RasterDocumentEncoderTests.Encode(document, settings);

		// Assert — 28 repeat groups of 256 lines, each a few run bytes
		Assert.InRange(bytes.Length, 1796, 4096);
		RasterPage page = Assert.Single(RasterDocumentReader.Read(bytes).Pages);
		Assert.Equal((4960, 7015), (page.Width, page.Height));
		Assert.Equal(0.0, page.GetInk());
	}

	[Fact]
	public void GivenCancelledTokenWhenEncodedThenThrowsOperationCanceledException()
	{
		// Arrange
		using CancellationTokenSource cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		RasterSettings settings = RasterDocumentEncoderTests.Settings(RasterFormat.PwgRaster, RasterColorSpace.Gray8, dpi: 72);

		// Act
		Action encode = () => RasterDocumentEncoder.Encode(new QuadrantDocument(RasterDocumentEncoderTests.PortraitA4), settings, Stream.Null, cancellation.Token);

		// Assert
		Assert.Throws<OperationCanceledException>(encode);
	}

	/// <summary>
	/// Makes settings for A4.
	/// </summary>
	/// <param name="format">The format.</param>
	/// <param name="colorSpace">The colour space.</param>
	/// <param name="dpi">The resolution.</param>
	/// <returns>The settings.</returns>
	private static RasterSettings Settings(RasterFormat format, RasterColorSpace colorSpace, int dpi)
	{
		return new RasterSettings { Format = format, ColorSpace = colorSpace, Dpi = dpi, Media = RasterDocumentEncoderTests.A4 };
	}

	/// <summary>
	/// Encodes a document.
	/// </summary>
	/// <param name="document">The document.</param>
	/// <param name="settings">The settings.</param>
	/// <returns>The raster document's bytes.</returns>
	private static byte[] Encode(IRasterDocument document, RasterSettings settings)
	{
		using MemoryStream output = new MemoryStream();
		RasterDocumentEncoder.Encode(document, settings, output, TestContext.Current.CancellationToken);

		return output.ToArray();
	}

	/// <summary>
	/// Encodes a document and reads it back.
	/// </summary>
	/// <param name="document">The document.</param>
	/// <param name="settings">The settings.</param>
	/// <returns>What a printer would read.</returns>
	private static RasterDocument EncodeAndRead(IRasterDocument document, RasterSettings settings)
	{
		return RasterDocumentReader.Read(RasterDocumentEncoderTests.Encode(document, settings));
	}

	/// <summary>
	/// Averages the grey over an area.
	/// </summary>
	/// <param name="page">The page.</param>
	/// <param name="left">The area's left.</param>
	/// <param name="top">Its top.</param>
	/// <param name="right">Its right, exclusive.</param>
	/// <param name="bottom">Its bottom, exclusive.</param>
	/// <returns>The average grey, 0 to 255.</returns>
	private static double InkIn(RasterPage page, int left, int top, int right, int bottom)
	{
		long total = 0;

		for (int y = top; y < bottom; y++)
		{
			for (int x = left; x < right; x++)
			{
				total += page.GetGrey(x, y);
			}
		}

		return (double)total / ((right - left) * (bottom - top));
	}

	/// <summary>
	/// A document of white pages.
	/// </summary>
	private sealed class BlankDocument : IRasterDocument
	{
		private readonly PageSize page;  // Every page's size

		/// <summary>
		/// Initialises a new instance of the <see cref="BlankDocument"/> class.
		/// </summary>
		/// <param name="page">The page size.</param>
		internal BlankDocument(PageSize page)
		{
			this.page = page;
		}

		/// <inheritdoc/>
		public int PageCount
		{
			get
			{
				return 1;
			}
		}

		/// <inheritdoc/>
		public PageSize GetPageSize(int pageIndex)
		{
			return this.page;
		}

		/// <inheritdoc/>
		public void RenderBand(int pageIndex, RasterBand band, Span<byte> pixels)
		{
			pixels[..(int)band.ByteCount].Fill(byte.MaxValue);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
		}
	}
}
