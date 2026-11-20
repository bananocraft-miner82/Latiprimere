using System.Text;
using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Raster;
using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Testing.Raster;
using Xunit;

namespace Latiprimere.Ipp.Tests.Raster;

/// <summary>
/// Real PDFs encoded as PCLm: every line of its strips, decoded as a printer decodes them, must be the line PWG raster
/// gets for the same document; and PDFium, rendering the PCLm as a PDF, must draw them where they belong.
/// </summary>
public sealed class PclmRoundTripTests
{
	private static readonly MediaSize A4 = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimeters = 210, HeightMillimeters = 297 };

	[Theory]
	[InlineData(TestDocuments.ThreePages, (int)RasterColorSpace.Gray8, 100, 16, (int)PdfStreamEncoding.Flate, DuplexMode.OneSided)]
	[InlineData(TestDocuments.ThreePages, (int)RasterColorSpace.Gray8, 150, 7, (int)PdfStreamEncoding.RunLength, DuplexMode.LongEdge)]
	[InlineData(TestDocuments.MixedOnePage, (int)RasterColorSpace.Srgb8, 150, 32, (int)PdfStreamEncoding.RunLength, DuplexMode.OneSided)]
	[InlineData(TestDocuments.MixedOnePage, (int)RasterColorSpace.Srgb8, 100, 1000, (int)PdfStreamEncoding.Flate, DuplexMode.OneSided)]
	public void GivenPdfWhenEncodedAsPclmThenItsStripsAreThePwgRasterLines
	(
		string documentName,
		int colorSpaceValue,
		int dpi,
		int stripHeight,
		int compressionValue,
		DuplexMode duplex
	)
	{
		// Arrange — odd strips that don't divide the page, strips taller than it, and rotated back sides (provided
		// by InlineData above)
		RasterColorSpace colorSpace = (RasterColorSpace)colorSpaceValue;
		PdfStreamEncoding compression = (PdfStreamEncoding)compressionValue;
		RasterSettings pwg = new RasterSettings
		{
			Format     = RasterFormat.PwgRaster,
			ColorSpace = colorSpace,
			Dpi        = dpi,
			Media      = PclmRoundTripTests.A4,
			Duplex     = duplex,
			BackSide   = RasterBackSide.Rotated
		};

		RasterSettings pclm = pwg with { Format = RasterFormat.Pclm, PclmStripHeight = stripHeight, PclmCompression = compression };

		// Act
		RasterDocument expected = RasterDocumentReader.Read(PclmRoundTripTests.Encode(documentName, pwg));
		byte[] encoded = PclmRoundTripTests.Encode(documentName, pclm);

		// Assert — each page is its strips in turn, every one as wide as the page and the last no taller than the rest
		IReadOnlyList<PclmStrip> strips = PclmStrip.ReadAll(encoded);
		int stripIndex = 0;
		int bytesPerPixel = colorSpace == RasterColorSpace.Srgb8 ? 3 : 1;

		foreach (RasterPage page in expected.Pages)
		{
			Assert.InRange(page.GetInk(), 0.01, 0.9);
			int y = 0;

			while (y < page.Height)
			{
				PclmStrip strip = strips[stripIndex++];
				Assert.Equal((page.Width, bytesPerPixel), (strip.Width, strip.BytesPerPixel));
				Assert.Equal(Math.Min(stripHeight, page.Height - y), strip.Height);

				for (int line = 0; line < strip.Height; line++)
				{
					Assert.True(page.Lines[y + line].AsSpan().SequenceEqual(strip.Data.AsSpan(line * page.Width * bytesPerPixel, page.Width * bytesPerPixel)));
				}

				y += strip.Height;
			}
		}

		Assert.Equal(strips.Count, stripIndex);

		// PDFium smooths images it scales, so its pixels are off by up to 2.6 on average; strips 32 lines out of place
		// would be off by 5 to 19
		using IRasterDocument rendered = new PdfiumRasterizer().Open(new MemoryStream(encoded));
		Assert.Equal(expected.Pages.Count, rendered.PageCount);

		for (int pageIndex = 0; pageIndex < rendered.PageCount; pageIndex++)
		{
			RasterPage page = expected.Pages[pageIndex];
			Assert.Equal(page.Width * 72.0 / dpi, rendered.GetPageSize(pageIndex).WidthPoints, 3);
			Assert.Equal(page.Height * 72.0 / dpi, rendered.GetPageSize(pageIndex).HeightPoints, 3);

			RasterBand band = RasterBand.Create(page.Width, page.Height, 0, page.Height);
			byte[] pixels = new byte[band.ByteCount];
			rendered.RenderBand(pageIndex, band, pixels);

			Assert.InRange(PclmRoundTripTests.MeanDifference(page, pixels, band.Stride, colorSpace), 0, 3);
		}
	}

	[Fact]
	public void GivenPclmWhenEncodedThenItIsMarkedAndEachPageIsItsStrips()
	{
		// Arrange — A4 at 100 dpi is 1169 lines: 36 strips of 32 and one of 17
		RasterSettings settings = new RasterSettings
		{
			Format          = RasterFormat.Pclm,
			ColorSpace      = RasterColorSpace.Gray8,
			Dpi             = 100,
			Media           = PclmRoundTripTests.A4,
			PclmStripHeight = 32,
			PclmCompression = PdfStreamEncoding.Flate
		};

		// Act
		byte[] encoded = PclmRoundTripTests.Encode(TestDocuments.ThreePages, settings);

		// Assert
		string text = Encoding.Latin1.GetString(encoded);
		Assert.StartsWith("%PDF-1.7\n%PCLm 1.0\n", text, StringComparison.Ordinal);
		Assert.Equal(3 * 37, PclmRoundTripTests.Count(text, "/Subtype /Image"));
		Assert.Equal(3 * 36, PclmRoundTripTests.Count(text, "/Height 32 "));
		Assert.Equal(3, PclmRoundTripTests.Count(text, "/Height 17 "));
		Assert.Equal(3 * 37, PclmRoundTripTests.Count(text, "/ColorSpace /DeviceGray"));
		Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenOneBitWhenEncodedAsPclmThenThrowsArgumentException()
	{
		// Arrange
		RasterSettings settings = new RasterSettings
		{
			Format     = RasterFormat.Pclm,
			ColorSpace = RasterColorSpace.Black1,
			Dpi        = 100,
			Media      = PclmRoundTripTests.A4
		};

		// Act
		Action encode = () => PclmRoundTripTests.Encode(TestDocuments.ThreePages, settings);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(encode);
		Assert.Contains("PCLm", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Renders a test document with PDFium and encodes it.
	/// </summary>
	/// <param name="documentName">The document.</param>
	/// <param name="settings">How to encode it.</param>
	/// <returns>The encoded document.</returns>
	private static byte[] Encode(string documentName, RasterSettings settings)
	{
		using FileStream file = TestDocuments.Open(documentName);
		using IRasterDocument document = new PdfiumRasterizer().Open(file);
		using MemoryStream output = new MemoryStream();

		RasterDocumentEncoder.Encode(document, settings, output, TestContext.Current.CancellationToken);

		return output.ToArray();
	}

	/// <summary>
	/// Measures how far PDFium's rendering is from a decoded PWG raster page, on average.
	/// </summary>
	/// <param name="page">The PWG raster page.</param>
	/// <param name="pixels">PDFium's rendering of the PCLm page: blue, green, red and alpha.</param>
	/// <param name="stride">The bytes in each rendered row.</param>
	/// <param name="colorSpace">The colour space both were encoded in.</param>
	/// <returns>The mean difference of each sample, from 0 to 255.</returns>
	private static double MeanDifference(RasterPage page, byte[] pixels, int stride, RasterColorSpace colorSpace)
	{
		long differences = 0;

		for (int y = 0; y < page.Height; y++)
		{
			byte[] line = page.Lines[y];

			for (int x = 0; x < page.Width; x++)
			{
				int source = (y * stride) + (x * RasterBand.BytesPerPixel);
				(int r, int g, int b) = (pixels[source + 2], pixels[source + 1], pixels[source]);

				// DeviceGray renders as equal red, green and blue
				differences += colorSpace == RasterColorSpace.Srgb8
					? Math.Abs(line[x * 3] - r) + Math.Abs(line[(x * 3) + 1] - g) + Math.Abs(line[(x * 3) + 2] - b)
					: 3 * Math.Abs(line[x] - r);
			}
		}

		return differences / (3.0 * page.Width * page.Height);
	}

	/// <summary>
	/// Counts a text's occurrences.
	/// </summary>
	/// <param name="text">Where to look.</param>
	/// <param name="value">What to count.</param>
	/// <returns>How often it occurs.</returns>
	private static int Count(string text, string value)
	{
		int count = 0;
		int index = text.IndexOf(value, StringComparison.Ordinal);

		while (index >= 0)
		{
			count++;
			index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
		}

		return count;
	}
}
