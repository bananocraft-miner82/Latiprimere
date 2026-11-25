using System.Text;
using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Tests.Fakes;
using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// Documents written page by page, then opened and rendered by PDFium, so PDF's own reader checks what the writer
/// wrote, pixel by pixel.
/// </summary>
public sealed class PdfDocumentWriterTests
{
	private const double Width = 400;  // The test page's width, in points
	private const double Height = 300; // Its height, in points

	[Fact]
	public void GivenGreyImageCompressedWithFlateWhenRenderedThenItsHalvesAreBlackAndWhite()
	{
		// Arrange — 40 × 30 samples, black on the left half, white on the right, filling the page
		PdfImage image = new PdfImage
		{
			Width      = 40,
			Height     = 30,
			ColorSpace = PdfColorSpace.Gray,
			Encoding   = PdfStreamEncoding.Flate,
			WriteData  = stream => PdfDocumentWriterTests.WriteHalves(stream, 40, 30, [0], [255])
		};

		// Act
		RenderedPage page = PdfDocumentWriterTests.RenderOnePage(writer => writer.DrawImage(image, 0, 0, PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height));

		// Assert
		Assert.Equal((0, 0, 0), page.GetRgb(100, 150));
		Assert.Equal((255, 255, 255), page.GetRgb(300, 150));
	}

	[Fact]
	public void GivenColourImageRunLengthEncodedWhenRenderedThenItsQuadrantsHaveTheirColours()
	{
		// Arrange — red, green, blue and white quadrants, the top first, as images store their rows
		PdfImage image = new PdfImage
		{
			Width      = 20,
			Height     = 20,
			ColorSpace = PdfColorSpace.Rgb,
			Encoding   = PdfStreamEncoding.RunLength,
			WriteData  = stream => PdfDocumentWriterTests.WriteQuadrants(stream, 20, 20)
		};

		// Act
		RenderedPage page = PdfDocumentWriterTests.RenderOnePage(writer => writer.DrawImage(image, 0, 0, PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height));

		// Assert
		Assert.Equal((255, 0, 0), page.GetRgb(100, 75));
		Assert.Equal((0, 255, 0), page.GetRgb(300, 75));
		Assert.Equal((0, 0, 255), page.GetRgb(100, 225));
		Assert.Equal((255, 255, 255), page.GetRgb(300, 225));
	}

	[Fact]
	public void GivenJpegPassedThroughWhenRenderedThenItsQuadrantsHaveTheirColours()
	{
		// Arrange — the JPEG's own bytes, named DCTDecode, decoded by the reader, not by Latiprimere
		byte[] jpeg = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", "quadrants-64x48.jpg"));

		PdfImage image = new PdfImage
		{
			Width      = 64,
			Height     = 48,
			ColorSpace = PdfColorSpace.Rgb,
			Encoding   = PdfStreamEncoding.None,
			Filter     = "DCTDecode",
			WriteData  = stream => stream.Write(jpeg)
		};

		// Act
		RenderedPage page = PdfDocumentWriterTests.RenderOnePage(writer => writer.DrawImage(image, 0, 0, PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height));

		// Assert — near each colour, as JPEG is lossy
		PdfDocumentWriterTests.AssertNear((255, 0, 0), page.GetRgb(100, 75));
		PdfDocumentWriterTests.AssertNear((0, 255, 0), page.GetRgb(300, 75));
		PdfDocumentWriterTests.AssertNear((0, 0, 255), page.GetRgb(100, 225));
		PdfDocumentWriterTests.AssertNear((255, 255, 255), page.GetRgb(300, 225));
	}

	[Fact]
	public void GivenImageDrawnInPartOfThePageWhenRenderedThenTheRestIsWhite()
	{
		// Arrange — a black square in the bottom left quarter: PDF's origin is the bottom left
		PdfImage image = new PdfImage
		{
			Width      = 1,
			Height     = 1,
			ColorSpace = PdfColorSpace.Gray,
			WriteData  = stream => stream.WriteByte(0)
		};

		// Act
		RenderedPage page = PdfDocumentWriterTests.RenderOnePage(writer => writer.DrawImage(image, 0, 0, 200, 150));

		// Assert — rows count from the top
		Assert.Equal((0, 0, 0), page.GetRgb(100, 225));
		Assert.Equal((255, 255, 255), page.GetRgb(100, 75));
		Assert.Equal((255, 255, 255), page.GetRgb(300, 225));
	}

	[Fact]
	public void GivenTextWithDelimitersAndWindows1252CharactersWhenRenderedThenItInksWhereItIsSet()
	{
		// Arrange — parentheses and a backslash, which need escaping, and € and curly quotes, which need WinAnsiEncoding

		// Act
		RenderedPage page = PdfDocumentWriterTests.RenderOnePage(writer => writer.DrawText("(Tea) \\ €2 “ok”", 20, 250, 24));

		// Assert — ink along the line, near the top, and none at the bottom
		Assert.True(page.CountDark(0, 30, 400, 60) > 100, "The text isn't there.");
		Assert.Equal(0, page.CountDark(0, 150, 400, 150));
	}

	[Fact]
	public void GivenSeveralPagesOfDifferentSizesWhenWrittenThenEachHasItsSize()
	{
		// Arrange — A4 portrait, A4 landscape and a label, each with a line of text
		using MemoryStream output = new MemoryStream();
		(double Width, double Height)[] sizes = [(595.2756, 841.8898), (841.8898, 595.2756), (288, 144)];

		using (PdfDocumentWriter writer = new PdfDocumentWriter(output))
		{
			// Act
			foreach ((double width, double height) in sizes)
			{
				writer.BeginPage(width, height);
				writer.DrawText("Page", 10, 10, 12);
				writer.EndPage();
			}

			writer.Complete();
			Assert.Equal(3, writer.PageCount);
		}

		// Assert
		output.Position = 0;
		using IRasterDocument document = new PdfiumRasterizer().Open(output);
		Assert.Equal(3, document.PageCount);

		for (int index = 0; index < sizes.Length; index++)
		{
			PageSize size = document.GetPageSize(index);
			Assert.Equal(sizes[index].Width, size.WidthPoints, 0.01);
			Assert.Equal(sizes[index].Height, size.HeightPoints, 0.01);
		}
	}

	[Fact]
	public void GivenLargeImageWrittenInRowsWhenWrittenToStreamThatCantSeekThenItIsntHeldWhole()
	{
		// Arrange — 2,000 × 2,000 grey samples, 4 MB, written a row at a time to a stream that can't seek
		RecordingStream recorded = new RecordingStream();
		using WriteOnlyStream output = new WriteOnlyStream(recorded);
		byte[] row = new byte[2000];

		PdfImage image = new PdfImage
		{
			Width      = 2000,
			Height     = 2000,
			ColorSpace = PdfColorSpace.Gray,
			WriteData  = stream =>
			{
				for (int y = 0; y < 2000; y++)
				{
					Array.Fill(row, (byte)(y % 256));
					stream.Write(row);
				}
			}
		};

		// Act
		using (PdfDocumentWriter writer = new PdfDocumentWriter(output))
		{
			writer.BeginPage(PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);
			writer.DrawImage(image, 0, 0, PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);
			writer.EndPage();
			writer.Complete();
		}

		// Assert — it went out in many writes, none of them the whole image, and PDFium reads it
		Assert.True(recorded.WriteSizes.Count > 10, $"{recorded.WriteSizes.Count} writes");
		Assert.True(recorded.WriteSizes.Max() < 4_000_000, $"The largest write was {recorded.WriteSizes.Max()} bytes");

		recorded.Position = 0;
		using IRasterDocument document = new PdfiumRasterizer().Open(recorded);
		Assert.Equal(1, document.PageCount);
	}

	[Fact]
	public void GivenTextWithCharacterCourierCantShowWhenDrawnThenThrowsArgumentExceptionNamingIt()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();
		using PdfDocumentWriter writer = new PdfDocumentWriter(output);
		writer.BeginPage(PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);

		// Act
		Action draw = () => writer.DrawText("Café Ā", 10, 10, 12);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(draw);
		Assert.Contains("U+0100", exception.Message, StringComparison.Ordinal);
		Assert.Contains("5", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenImageWithFilterAndEncodingWhenDrawnThenThrowsArgumentException()
	{
		// Arrange — data said to be a JPEG already, and to be compressed again
		using MemoryStream output = new MemoryStream();
		using PdfDocumentWriter writer = new PdfDocumentWriter(output);
		writer.BeginPage(PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);

		PdfImage image = new PdfImage
		{
			Width      = 1,
			Height     = 1,
			ColorSpace = PdfColorSpace.Gray,
			Encoding   = PdfStreamEncoding.Flate,
			Filter     = "DCTDecode",
			WriteData  = stream => stream.WriteByte(0)
		};

		// Act
		Action draw = () => writer.DrawImage(image, 0, 0, 10, 10);

		// Assert
		Assert.Throws<ArgumentException>(draw);
	}

	[Fact]
	public void GivenCallsOutOfOrderWhenMadeThenThrowInvalidOperationException()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();
		using PdfDocumentWriter writer = new PdfDocumentWriter(output);

		// Act and assert — drawing or ending without a page, completing without one, starting two, completing mid-page
		Assert.Throws<InvalidOperationException>(() => writer.DrawText("a", 0, 0, 10));
		Assert.Throws<InvalidOperationException>(() => writer.EndPage());
		Assert.Throws<InvalidOperationException>(() => writer.Complete());

		writer.BeginPage(10, 10);
		Assert.Throws<InvalidOperationException>(() => writer.BeginPage(10, 10));
		Assert.Throws<InvalidOperationException>(() => writer.Complete());
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(double.NaN)]
	public void GivenPageSizeThatIsntPositiveWhenBegunThenThrowsArgumentOutOfRangeException(double size)
	{
		// Arrange (provided by InlineData above)
		using MemoryStream output = new MemoryStream();
		using PdfDocumentWriter writer = new PdfDocumentWriter(output);

		// Act
		Action begin = () => writer.BeginPage(size, 100);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(begin);
	}

	[Fact]
	public void GivenDocumentWhenWrittenThenFileIsAsciiOutsideItsStreamsAndEndsWithTheTrailer()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();

		// Act
		using (PdfDocumentWriter writer = new PdfDocumentWriter(output))
		{
			writer.BeginPage(PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);
			writer.DrawText("Hello", 10, 10, 12);
			writer.EndPage();
			writer.Complete();
		}

		// Assert — Courier is named, not embedded
		string file = Encoding.Latin1.GetString(output.ToArray());
		Assert.Contains("/BaseFont /Courier /Encoding /WinAnsiEncoding", file, StringComparison.Ordinal);
		Assert.DoesNotContain("/FontFile", file, StringComparison.Ordinal);
		Assert.Contains("/Producer (Latiprimere)", file, StringComparison.Ordinal);
		Assert.EndsWith("%%EOF\n", file, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenRectangleFilledInRedWhenRenderedThenItIsRedAndTheRestWhite()
	{
		// Arrange — 100 × 50 points from (50, 100), from the bottom: rows 150 to 200 from the top
		Action<PdfDocumentWriter> draw = writer => writer.FillRectangle(50, 100, 100, 50, 1, 0, 0);

		// Act
		RenderedPage page = PdfDocumentWriterTests.RenderOnePage(draw);

		// Assert
		Assert.Equal((255, 0, 0), page.GetRgb(100, 175));
		Assert.Equal((255, 255, 255), page.GetRgb(100, 120));
		Assert.Equal((255, 255, 255), page.GetRgb(200, 175));
	}

	[Fact]
	public void GivenRectangleFilledInGreyWhenWrittenThenItIsInDeviceGray()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();

		// Act
		using (PdfDocumentWriter writer = new PdfDocumentWriter(output))
		{
			writer.BeginPage(PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);
			writer.FillRectangle(10, 20, 30, 40, 0.5, 0.5, 0.5);
			writer.FillRectangle(10, 20, 30, 40, 0, 1, 1);
			writer.EndPage();
			writer.Complete();
		}

		// Assert — a grey with black alone, a colour in RGB
		string content = Assert.Single(PdfTextLines.ReadContents(output.ToArray()));
		Assert.Contains("q 0.5 g 10 20 30 40 re f Q", content, StringComparison.Ordinal);
		Assert.Contains("q 0 1 1 rg 10 20 30 40 re f Q", content, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenRectangleOutlinedWhenRenderedThenItsEdgeIsDarkAndItsInsideWhite()
	{
		// Arrange — a 2-point line, so it covers whole pixels
		Action<PdfDocumentWriter> draw = writer => writer.StrokeRectangle(50, 50, 200, 100, 2);

		// Act
		RenderedPage page = PdfDocumentWriterTests.RenderOnePage(draw);

		// Assert — the left edge at x 50, the inside, and outside
		Assert.True(page.CountDark(49, 160, 2, 80) > 0);
		Assert.Equal((255, 255, 255), page.GetRgb(150, 200));
		Assert.Equal(0, page.CountDark(0, 0, 40, (int)PdfDocumentWriterTests.Height));
	}

	[Fact]
	public void GivenLineDrawnWhenRenderedThenItIsDarkAlongItsLength()
	{
		// Arrange — across the page at 150 points up, row 150 from the top
		Action<PdfDocumentWriter> draw = writer => writer.DrawLine(20, 150, 380, 150, 2);

		// Act
		RenderedPage page = PdfDocumentWriterTests.RenderOnePage(draw);

		// Assert
		Assert.True(page.CountDark(20, 149, 360, 2) > 300);
		Assert.Equal(0, page.CountDark(0, 0, (int)PdfDocumentWriterTests.Width, 140));
	}

	[Theory]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	[InlineData(double.NaN)]
	public void GivenColourComponentOutsideZeroToOneWhenFilledThenThrowsArgumentOutOfRange(double red)
	{
		// Arrange
		using MemoryStream output = new MemoryStream();
		using PdfDocumentWriter writer = new PdfDocumentWriter(output);
		writer.BeginPage(PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);

		// Act
		Action fill = () => writer.FillRectangle(0, 0, 10, 10, red, 0, 0);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(fill);
	}

	[Fact]
	public void GivenLineOfNoWidthWhenDrawnThenThrowsArgumentOutOfRange()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();
		using PdfDocumentWriter writer = new PdfDocumentWriter(output);
		writer.BeginPage(PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);

		// Act
		Action draw = () => writer.DrawLine(0, 0, 10, 10, 0);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(draw);
	}

	/// <summary>
	/// Writes a one-page document of the test page's size, drawing on it as the test says, and renders it at 72 dpi,
	/// a pixel a point.
	/// </summary>
	/// <param name="draw">Draws on the page.</param>
	/// <returns>The rendered page.</returns>
	private static RenderedPage RenderOnePage(Action<PdfDocumentWriter> draw)
	{
		using MemoryStream output = new MemoryStream();

		using (PdfDocumentWriter writer = new PdfDocumentWriter(output))
		{
			writer.BeginPage(PdfDocumentWriterTests.Width, PdfDocumentWriterTests.Height);
			draw(writer);
			writer.EndPage();
			writer.Complete();
		}

		output.Position = 0;
		using IRasterDocument document = new PdfiumRasterizer().Open(output);
		Assert.Equal(1, document.PageCount);

		RasterBand band = RasterBand.Create((int)PdfDocumentWriterTests.Width, (int)PdfDocumentWriterTests.Height, 0, (int)PdfDocumentWriterTests.Height);
		byte[] pixels = new byte[band.ByteCount];
		document.RenderBand(0, band, pixels);

		return new RenderedPage(pixels, band.Stride);
	}

	/// <summary>
	/// Writes rows whose left half is one sample and right half another.
	/// </summary>
	/// <param name="stream">Where the samples go.</param>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	/// <param name="left">The left half's pixel.</param>
	/// <param name="right">The right half's pixel.</param>
	private static void WriteHalves(Stream stream, int width, int height, byte[] left, byte[] right)
	{
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				stream.Write(x < width / 2 ? left : right);
			}
		}
	}

	/// <summary>
	/// Writes RGB rows of four quadrants: red and green above, blue and white below.
	/// </summary>
	/// <param name="stream">Where the samples go.</param>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	private static void WriteQuadrants(Stream stream, int width, int height)
	{
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				byte[] pixel = (x < width / 2, y < height / 2) switch
				{
					(true, true)   => [255, 0, 0],
					(false, true)  => [0, 255, 0],
					(true, false)  => [0, 0, 255],
					(false, false) => [255, 255, 255],
				};

				stream.Write(pixel);
			}
		}
	}

	/// <summary>
	/// Checks a colour is within 40 of each component of another, as a JPEG's colours are.
	/// </summary>
	/// <param name="expected">The colour expected.</param>
	/// <param name="actual">The colour rendered.</param>
	private static void AssertNear((int R, int G, int B) expected, (int R, int G, int B) actual)
	{
		Assert.True
		(
			Math.Abs(expected.R - actual.R) <= 40 && Math.Abs(expected.G - actual.G) <= 40 && Math.Abs(expected.B - actual.B) <= 40,
			$"Expected about {expected}, but got {actual}."
		);
	}
}
