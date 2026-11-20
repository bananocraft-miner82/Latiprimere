using Latiprimere.Ipp.Documents;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

public sealed class ImagePageLayoutTests
{
	private const double A4Width = 595.2756;  // 210 mm in points
	private const double A4Height = 841.8898; // 297 mm in points

	private static readonly MediaSize A4 = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimeters = 210, HeightMillimeters = 297 };

	[Theory]
	[InlineData(ScalingMode.PrinterDefault, 480, 360)]
	[InlineData(ScalingMode.None, 480, 360)]
	[InlineData(ScalingMode.Fit, 755.905512, 566.929134)]
	[InlineData(ScalingMode.Fill, 841.8898, 631.4173)]
	public void GivenASmallLandscapeImageWhenPlacedThenALandscapePageAndTheScalingsSize(ScalingMode scaling, double width, double height)
	{
		// Arrange — 640 × 480 at 96 dpi is 480 × 360 points; Fit fills the height within 5 mm margins, Fill covers the
		// page's width (provided by InlineData above)
		PageImage image = ImagePageLayoutTests.Image(640, 480, 96, 96);

		// Act
		ImagePlacement placement = ImagePageLayout.Place(image, ImagePageLayoutTests.A4, PageOrientation.PrinterDefault, scaling);

		// Assert — centred on a landscape A4 page
		Assert.True(placement.IsLandscape);
		Assert.Equal(ImagePageLayoutTests.A4Height, placement.PageWidth, 3);
		Assert.Equal(ImagePageLayoutTests.A4Width, placement.PageHeight, 3);
		ImagePageLayoutTests.AssertRectangle(placement, (placement.PageWidth - width) / 2, (placement.PageHeight - height) / 2, width, height);
	}

	[Fact]
	public void GivenALargeImageWhenPlacedByDefaultThenItIsShrunkToFitWithinTheMargins()
	{
		// Arrange — 4000 × 3000 at 72 dpi is 4000 × 3000 points
		PageImage image = ImagePageLayoutTests.Image(4000, 3000, 72, 72);

		// Act
		ImagePlacement placement = ImagePageLayout.Place(image, ImagePageLayoutTests.A4, PageOrientation.PrinterDefault, ScalingMode.PrinterDefault);

		// Assert — the height is the limit: 595.28 less two 5 mm margins
		double height = ImagePageLayoutTests.A4Width - (2 * ImagePageLayout.Margin);
		double width = height * 4 / 3;
		ImagePageLayoutTests.AssertRectangle(placement, (placement.PageWidth - width) / 2, ImagePageLayout.Margin, width, height);
	}

	[Theory]
	[InlineData(PageOrientation.Portrait, false)]
	[InlineData(PageOrientation.Landscape, true)]
	public void GivenAnOrientationWhenPlacedThenThePageFollowsItNotTheImage(PageOrientation orientation, bool landscape)
	{
		// Arrange — a landscape image, and a portrait one, each way (provided by InlineData above)
		PageImage wide = ImagePageLayoutTests.Image(300, 100, 96, 96);
		PageImage tall = ImagePageLayoutTests.Image(100, 300, 96, 96);

		// Act
		ImagePlacement wideOnPage = ImagePageLayout.Place(wide, ImagePageLayoutTests.A4, orientation, ScalingMode.Fit);
		ImagePlacement tallOnPage = ImagePageLayout.Place(tall, ImagePageLayoutTests.A4, orientation, ScalingMode.Fit);

		// Assert
		Assert.Equal((landscape, landscape), (wideOnPage.IsLandscape, tallOnPage.IsLandscape));
	}

	[Theory]
	[InlineData(null, null, 72.0, 72.0)]
	[InlineData(0.0, 0.0, 72.0, 72.0)]
	[InlineData(192.0, null, 36.0, 36.0)]
	[InlineData(null, 48.0, 144.0, 144.0)]
	[InlineData(96.0, 48.0, 72.0, 144.0)]
	public void GivenResolutionsMissingOrGivenOneWayWhenPlacedThen96DpiOrTheOneGiven(double? dpiX, double? dpiY, double width, double height)
	{
		// Arrange — 96 × 96 pixels (provided by InlineData above)
		PageImage image = ImagePageLayoutTests.Image(96, 96, dpiX, dpiY);

		// Act
		ImagePlacement placement = ImagePageLayout.Place(image, ImagePageLayoutTests.A4, PageOrientation.Portrait, ScalingMode.None);

		// Assert
		ImagePageLayoutTests.AssertRectangle(placement, (placement.PageWidth - width) / 2, (placement.PageHeight - height) / 2, width, height);
	}

	[Theory]
	[InlineData(5)]
	[InlineData(6)]
	[InlineData(7)]
	[InlineData(8)]
	public void GivenAnOrientationThatTurnsAQuarterWhenPlacedThenItsWidthAndHeightSwap(int orientation)
	{
		// Arrange — stored landscape, shown portrait (provided by InlineData above)
		PageImage image = ImagePageLayoutTests.Image(400, 200, 96, 96) with { Orientation = orientation };

		// Act
		ImagePlacement placement = ImagePageLayout.Place(image, ImagePageLayoutTests.A4, PageOrientation.PrinterDefault, ScalingMode.None);

		// Assert — a portrait page, the image 150 points wide and 300 high
		Assert.False(placement.IsLandscape);
		ImagePageLayoutTests.AssertRectangle(placement, (placement.PageWidth - 150) / 2, (placement.PageHeight - 300) / 2, 150, 300);
	}

	[Theory]
	[InlineData(1, "top-left", "top-right")]
	[InlineData(2, "top-right", "top-left")]
	[InlineData(3, "bottom-right", "bottom-left")]
	[InlineData(4, "bottom-left", "bottom-right")]
	[InlineData(5, "top-left", "bottom-left")]
	[InlineData(6, "top-right", "bottom-right")]
	[InlineData(7, "bottom-right", "top-right")]
	[InlineData(8, "bottom-left", "top-left")]
	public void GivenAnExifOrientationWhenOrientedThenTheStoredFirstRowGoesWhereExifSays(int orientation, string firstPixel, string lastOfFirstRow)
	{
		// Arrange — a 10 × 10 rectangle at the origin; in the unit square the stored first row is v = 1 (provided by
		// InlineData above)

		// Act
		PdfMatrix matrix = ImagePageLayout.Orient(orientation, 0, 0, 10, 10);

		// Assert
		Assert.Equal(firstPixel, ImagePageLayoutTests.Corner(matrix, 0, 1));
		Assert.Equal(lastOfFirstRow, ImagePageLayoutTests.Corner(matrix, 1, 1));
	}

	/// <summary>
	/// Makes an image to place.
	/// </summary>
	/// <param name="width">Its width in pixels.</param>
	/// <param name="height">Its height.</param>
	/// <param name="dpiX">Its horizontal resolution, or <see langword="null"/>.</param>
	/// <param name="dpiY">Its vertical resolution, or <see langword="null"/>.</param>
	/// <returns>The image.</returns>
	private static PageImage Image(int width, int height, double? dpiX, double? dpiY)
	{
		return new PageImage
		{
			Width  = width,
			Height = height,
			DpiX   = dpiX,
			DpiY   = dpiY,
			Image  = new PdfImage { Width = width, Height = height, ColorSpace = PdfColorSpace.Gray, WriteData = stream => { } }
		};
	}

	/// <summary>
	/// Checks the rectangle a placement draws the image into, from where its matrix takes the unit square's corners.
	/// </summary>
	/// <param name="placement">The placement.</param>
	/// <param name="x">The expected left.</param>
	/// <param name="y">The expected bottom.</param>
	/// <param name="width">The expected width.</param>
	/// <param name="height">The expected height.</param>
	private static void AssertRectangle(ImagePlacement placement, double x, double y, double width, double height)
	{
		PdfMatrix m = placement.Matrix;
		double[] xs = [m.E, m.A + m.E, m.C + m.E, m.A + m.C + m.E];
		double[] ys = [m.F, m.B + m.F, m.D + m.F, m.B + m.D + m.F];

		Assert.Equal(x, xs.Min(), 3);
		Assert.Equal(y, ys.Min(), 3);
		Assert.Equal(width, xs.Max() - xs.Min(), 3);
		Assert.Equal(height, ys.Max() - ys.Min(), 3);
	}

	/// <summary>
	/// Names the corner of the 10 × 10 rectangle a point of the unit square goes to.
	/// </summary>
	/// <param name="matrix">The matrix.</param>
	/// <param name="u">The point's u.</param>
	/// <param name="v">Its v.</param>
	/// <returns>"top-left", "top-right", "bottom-left" or "bottom-right"; PDF's y runs up.</returns>
	private static string Corner(PdfMatrix matrix, double u, double v)
	{
		double x = (matrix.A * u) + (matrix.C * v) + matrix.E;
		double y = (matrix.B * u) + (matrix.D * v) + matrix.F;

		return (y > 5 ? "top" : "bottom") + "-" + (x > 5 ? "right" : "left");
	}
}
