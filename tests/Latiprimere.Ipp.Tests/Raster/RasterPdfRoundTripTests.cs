using Latiprimere.Ipp.Raster;
using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Testing.Raster;
using Xunit;

namespace Latiprimere.Ipp.Tests.Raster;

/// <summary>
/// Real PDFs, rendered by PDFium, encoded and read back: every pixel must be the page as PDFium renders it directly.
/// </summary>
public sealed class RasterPdfRoundTripTests
{
	private static readonly MediaSize A4 = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimeters = 210, HeightMillimeters = 297 };

	[Theory]
	[InlineData(TestDocuments.ThreePages, RasterFormat.PwgRaster, RasterColorSpace.Gray8, 150)]
	[InlineData(TestDocuments.ThreePages, RasterFormat.AppleRaster, RasterColorSpace.Gray8, 100)]
	[InlineData(TestDocuments.MixedOnePage, RasterFormat.PwgRaster, RasterColorSpace.Srgb8, 100)]
	[InlineData(TestDocuments.MixedOnePage, RasterFormat.AppleRaster, RasterColorSpace.Srgb8, 150)]
	public void GivenPdfWhenEncodedThenEveryPixelIsThePageAsPdfiumRendersIt(string documentName, int format, int colorSpace, int dpi)
	{
		// Arrange (provided by InlineData above)
		RasterSettings settings = new RasterSettings
		{
			Format     = (RasterFormat)format,
			ColorSpace = (RasterColorSpace)colorSpace,
			Dpi        = dpi,
			Media      = RasterPdfRoundTripTests.A4
		};

		using FileStream file = TestDocuments.Open(documentName);
		using IRasterDocument document = new PdfiumRasterizer().Open(file);
		using MemoryStream output = new MemoryStream();

		// Act
		RasterDocumentEncoder.Encode(document, settings, output, TestContext.Current.CancellationToken);
		RasterDocument raster = RasterDocumentReader.Read(output.ToArray());

		// Assert
		Assert.Equal(document.PageCount, raster.Pages.Count);
		int mediaWidth = RasterDocumentEncoder.ToPixels(210, dpi);
		int mediaHeight = RasterDocumentEncoder.ToPixels(297, dpi);

		for (int pageIndex = 0; pageIndex < document.PageCount; pageIndex++)
		{
			RasterPage page = raster.Pages[pageIndex];
			Assert.Equal((mediaWidth, mediaHeight), (page.Width, page.Height));
			Assert.InRange(page.GetInk(), 0.01, 0.9);

			// The page rendered directly, in one band, at the size and turn the encoder chose
			(RasterRotation rotation, int x, int y, int width, int height) = RasterDocumentEncoder.Layout
			(
				document.GetPageSize(pageIndex), PageOrientation.PrinterDefault, mediaWidth, mediaHeight, dpi
			);

			// In the format the encoder renders the colour space in: grey for grey (ADR 0054)
			RasterPixelFormat pixelFormat = RasterPixels.RenderFormat((RasterColorSpace)colorSpace);
			RasterBand band = RasterBand.Create(width, height, 0, height, pixelFormat) with { Rotation = rotation };
			byte[] expected = new byte[band.ByteCount];
			document.RenderBand(pageIndex, band, expected);
			RasterPdfRoundTripTests.AssertSamePixels(page, expected, x, y, width, height, (RasterColorSpace)colorSpace);
		}
	}

	/// <summary>
	/// Checks every pixel of a decoded page: the rendered pixels inside the content box, white outside.
	/// </summary>
	/// <param name="page">The decoded page.</param>
	/// <param name="rendered">The content as rendered: four bytes a pixel for colour, one of grey otherwise.</param>
	/// <param name="left">The content's left.</param>
	/// <param name="top">Its top.</param>
	/// <param name="width">Its width.</param>
	/// <param name="height">Its height.</param>
	/// <param name="colorSpace">The colour space it was encoded in.</param>
	private static void AssertSamePixels(RasterPage page, byte[] rendered, int left, int top, int width, int height, RasterColorSpace colorSpace)
	{
		int differences = 0;

		for (int y = 0; y < page.Height; y++)
		{
			byte[] line = page.Lines[y];

			for (int x = 0; x < page.Width; x++)
			{
				bool inside = x >= left && x < left + width && y >= top && y < top + height;
				int pixel = inside ? ((y - top) * width) + (x - left) : -1;
				bool same;

				if (colorSpace == RasterColorSpace.Srgb8)
				{
					int source = pixel * RasterBand.GetBytesPerPixel(RasterPixelFormat.Bgra32);
					(int r, int g, int b) = inside ? (rendered[source + 2], rendered[source + 1], rendered[source]) : (255, 255, 255);
					same = line[x * 3] == r && line[(x * 3) + 1] == g && line[(x * 3) + 2] == b;
				}
				else
				{
					same = line[x] == (inside ? rendered[pixel] : 255);
				}

				differences += same ? 0 : 1;
			}
		}

		Assert.Equal(0, differences);
	}
}
