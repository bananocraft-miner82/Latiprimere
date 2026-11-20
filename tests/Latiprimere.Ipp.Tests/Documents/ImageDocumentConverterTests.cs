using System.Text;
using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Tests.Fakes;
using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// Images converted to PDF and rendered by PDFium: each quadrant must show where its orientation puts it, in its
/// colour, with transparency over white.
/// </summary>
public sealed class ImageDocumentConverterTests
{
	// A 4 × 3 inch paper, so a 64 × 48 image at 16 dpi fills it exactly, a pixel a point at 72 dpi times 4.5
	private static readonly MediaSize Paper = new MediaSize { Name = "custom_4x3in", DisplayName = "4 × 3 in", WidthMillimeters = 76.2, HeightMillimeters = 101.6 };

	[Theory]
	[InlineData(1, "top-left", "top-right")]
	[InlineData(2, "top-right", "top-left")]
	[InlineData(3, "bottom-right", "bottom-left")]
	[InlineData(4, "bottom-left", "bottom-right")]
	[InlineData(5, "top-left", "bottom-left")]
	[InlineData(6, "top-right", "bottom-right")]
	[InlineData(7, "bottom-right", "top-right")]
	[InlineData(8, "bottom-left", "top-left")]
	public void GivenAJpegWithAnExifOrientationWhenConvertedThenPdfiumShowsItTurned(int orientation, string red, string green)
	{
		// Arrange — red stored top left, green top right; blue and white opposite them (provided by InlineData above)
		byte[] jpeg = ImageTestData.InsertSegment(ImageTestData.QuadrantsJpeg(), 0xE1, [.. "Exif\0\0"u8, .. ImageTestData.Exif(orientation)]);

		// Act
		(RenderedPage page, (double X, double Y, double Width, double Height) image) = ImageDocumentConverterTests.ConvertAndRender
		(
			PrintContent.FromJpeg(new MemoryStream(jpeg)),
			new PrintOptions { Scaling = ScalingMode.Fit }
		);

		// Assert — near each colour, as JPEG is lossy
		ImageDocumentConverterTests.AssertNear((255, 0, 0), ImageDocumentConverterTests.At(page, image, red));
		ImageDocumentConverterTests.AssertNear((0, 255, 0), ImageDocumentConverterTests.At(page, image, green));
		ImageDocumentConverterTests.AssertNear((0, 0, 255), ImageDocumentConverterTests.At(page, image, ImageDocumentConverterTests.Opposite(green)));
		ImageDocumentConverterTests.AssertNear((255, 255, 255), ImageDocumentConverterTests.At(page, image, ImageDocumentConverterTests.Opposite(red)));
	}

	[Theory]
	[InlineData(2, 8, false)]
	[InlineData(2, 16, true)]
	[InlineData(6, 8, true)]
	public void GivenAColourPngWhenConvertedThenPdfiumShowsItsQuadrantsExactly(int colorType, int bitDepth, bool interlaced)
	{
		// Arrange — passed through, decoded at 16 bits and interlaced, and decoded with an opaque alpha channel
		// (provided by InlineData above)
		int scale = bitDepth == 16 ? 257 : 1;
		byte[] png = PngTestEncoder.Encode
		(
			64,
			48,
			colorType,
			bitDepth,
			(x, y) => [.. ImageTestData.Quadrant(x, y).Select(value => value * scale), .. colorType == 6 ? new[] { 255 } : []],
			interlaced
		);

		// Act
		(RenderedPage page, (double X, double Y, double Width, double Height) image) = ImageDocumentConverterTests.ConvertAndRender
		(
			PrintContent.FromPng(new MemoryStream(png)),
			new PrintOptions { Scaling = ScalingMode.Fit }
		);

		// Assert
		Assert.Equal((255, 0, 0), ImageDocumentConverterTests.At(page, image, "top-left"));
		Assert.Equal((0, 255, 0), ImageDocumentConverterTests.At(page, image, "top-right"));
		Assert.Equal((0, 0, 255), ImageDocumentConverterTests.At(page, image, "bottom-left"));
		Assert.Equal((255, 255, 255), ImageDocumentConverterTests.At(page, image, "bottom-right"));
	}

	[Fact]
	public void GivenATransparentPngWhenConvertedThenItsClearPartsAreWhiteOnThePage()
	{
		// Arrange — the left half black and opaque, the right half black and clear
		byte[] png = PngTestEncoder.Encode(64, 48, 4, 8, (x, y) => [0, x < 32 ? 255 : 0]);

		// Act
		(RenderedPage page, (double X, double Y, double Width, double Height) image) = ImageDocumentConverterTests.ConvertAndRender
		(
			PrintContent.FromPng(new MemoryStream(png)),
			new PrintOptions { Scaling = ScalingMode.Fit }
		);

		// Assert
		Assert.Equal((0, 0, 0), ImageDocumentConverterTests.At(page, image, "top-left"));
		Assert.Equal((255, 255, 255), ImageDocumentConverterTests.At(page, image, "top-right"));
		Assert.Equal((0, 0, 0), ImageDocumentConverterTests.At(page, image, "bottom-left"));
	}

	[Fact]
	public void GivenAJpegWhenConvertedThenTheFileIsOnePdfPageHoldingTheJpegAsItIs()
	{
		// Arrange
		byte[] jpeg = ImageTestData.QuadrantsJpeg();

		// Act
		(MemoryStream pdf, ImagePlacement placement) = ImageDocumentConverter.ToPdf
		(
			PrintContent.FromJpeg(new MemoryStream(jpeg)),
			ImageDocumentConverterTests.Paper,
			PrintOptions.Default
		);

		// Assert — the JPEG's bytes appear whole
		using (pdf)
		{
			byte[] file = pdf.ToArray();
			Assert.StartsWith("%PDF-1.7", Encoding.Latin1.GetString(file, 0, 8), StringComparison.Ordinal);
			Assert.Contains("/Filter /DCTDecode", Encoding.Latin1.GetString(file), StringComparison.Ordinal);
			Assert.True(file.AsSpan().IndexOf(jpeg) > 0);
			Assert.True(placement.IsLandscape);
		}
	}

	[Fact]
	public void GivenAJpegFromAStreamThatCantSeekWhenConvertedThenItIsReadWhole()
	{
		// Arrange
		byte[] jpeg = ImageTestData.QuadrantsJpeg();

		// Act
		(MemoryStream pdf, ImagePlacement placement) = ImageDocumentConverter.ToPdf
		(
			PrintContent.FromJpeg(new NonSeekableStream(jpeg)),
			ImageDocumentConverterTests.Paper,
			PrintOptions.Default
		);

		// Assert
		using (pdf)
		{
			Assert.True(pdf.ToArray().AsSpan().IndexOf(jpeg) > 0);
			Assert.True(placement.IsLandscape);
		}
	}

	[Fact]
	public void GivenAJpegFromPartWayThroughAStreamWhenConvertedThenOnlyTheRestIsRead()
	{
		// Arrange — two bytes before the JPEG, already read past
		byte[] jpeg = ImageTestData.QuadrantsJpeg();
		MemoryStream stream = new MemoryStream([0x00, 0x01, .. jpeg]) { Position = 2 };

		// Act
		(MemoryStream pdf, _) = ImageDocumentConverter.ToPdf(PrintContent.FromJpeg(stream), ImageDocumentConverterTests.Paper, PrintOptions.Default);

		// Assert
		using (pdf)
		{
			Assert.True(pdf.ToArray().AsSpan().IndexOf(jpeg) > 0);
		}
	}

	[Theory]
	[InlineData(DocumentFormat.Pdf)]
	[InlineData(DocumentFormat.Raw)]
	public void GivenADocumentThatIsntAnImageWhenConvertedThenThrowsArgumentException(DocumentFormat format)
	{
		// Arrange (provided by InlineData above)
		PrintContent content = format == DocumentFormat.Pdf ? PrintContent.FromPdf(new MemoryStream()) : PrintContent.FromRaw(new MemoryStream());

		// Act
		Action convert = () => ImageDocumentConverter.ToPdf(content, ImageDocumentConverterTests.Paper, PrintOptions.Default);

		// Assert
		Assert.Throws<ArgumentException>(convert);
	}

	/// <summary>
	/// Converts an image on the 4 × 3 inch paper and renders the page at 72 dpi.
	/// </summary>
	/// <param name="content">The image.</param>
	/// <param name="options">The job's options.</param>
	/// <returns>The rendered page and the image's rectangle on it, in pixels from the top left.</returns>
	private static (RenderedPage Page, (double X, double Y, double Width, double Height) Image) ConvertAndRender(PrintContent content, PrintOptions options)
	{
		(MemoryStream pdf, ImagePlacement placement) = ImageDocumentConverter.ToPdf(content, ImageDocumentConverterTests.Paper, options);

		using (pdf)
		{
			using IRasterDocument document = new PdfiumRasterizer().Open(pdf);
			Assert.Equal(1, document.PageCount);

			int width = (int)Math.Round(placement.PageWidth);
			int height = (int)Math.Round(placement.PageHeight);
			RasterBand band = RasterBand.Create(width, height, 0, height);
			byte[] pixels = new byte[band.ByteCount];
			document.RenderBand(0, band, pixels);

			PdfMatrix m = placement.Matrix;
			double[] xs = [m.E, m.A + m.E, m.C + m.E, m.A + m.C + m.E];
			double[] ys = [m.F, m.B + m.F, m.D + m.F, m.B + m.D + m.F];

			return (new RenderedPage(pixels, band.Stride), (xs.Min(), placement.PageHeight - ys.Max(), xs.Max() - xs.Min(), ys.Max() - ys.Min()));
		}
	}

	/// <summary>
	/// Reads the colour at the middle of one quarter of the image.
	/// </summary>
	/// <param name="page">The rendered page.</param>
	/// <param name="image">The image's rectangle, from the top left.</param>
	/// <param name="corner">Which quarter: "top-left" and so on.</param>
	/// <returns>The colour.</returns>
	private static (int R, int G, int B) At(RenderedPage page, (double X, double Y, double Width, double Height) image, string corner)
	{
		double x = image.X + (image.Width * (corner.EndsWith("left", StringComparison.Ordinal) ? 0.25 : 0.75));
		double y = image.Y + (image.Height * (corner.StartsWith("top", StringComparison.Ordinal) ? 0.25 : 0.75));

		return page.GetRgb((int)x, (int)y);
	}

	/// <summary>
	/// Names the opposite corner.
	/// </summary>
	/// <param name="corner">A corner.</param>
	/// <returns>The one diagonally across.</returns>
	private static string Opposite(string corner)
	{
		return corner switch
		{
			"top-left"    => "bottom-right",
			"top-right"   => "bottom-left",
			"bottom-left" => "top-right",
			_             => "top-left",
		};
	}

	/// <summary>
	/// Checks a colour is within a JPEG's error of another.
	/// </summary>
	/// <param name="expected">The colour expected.</param>
	/// <param name="actual">The colour found.</param>
	private static void AssertNear((int R, int G, int B) expected, (int R, int G, int B) actual)
	{
		Assert.True
		(
			Math.Abs(expected.R - actual.R) <= 40 && Math.Abs(expected.G - actual.G) <= 40 && Math.Abs(expected.B - actual.B) <= 40,
			$"expected near {expected}, found {actual}"
		);
	}
}
