namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Places an image on a page of the job's paper, as IPP's <c>print-scaling</c> does (PWG 5100.13, 6.2.3): turned
/// as its EXIF orientation says, on a landscape page when it is wider than tall, and sized as
/// <see cref="ScalingMode"/> says, centred.
/// </summary>
internal static class ImagePageLayout
{
	/// <summary>The resolution an image that gives none is printed at, as Windows and web browsers show it.</summary>
	internal const double DefaultDpi = 96;

	/// <summary>
	/// The margin, in points, kept clear by every scaling but <see cref="ScalingMode.Fill"/>: 5 mm, which most
	/// printers' unprintable edges fit within.
	/// </summary>
	internal const double Margin = 5 * 72 / 25.4;

	private const double PointsPerInch = 72;
	private const double MillimetresPerInch = 25.4;

	/// <summary>
	/// Places an image.
	/// </summary>
	/// <param name="image">The image.</param>
	/// <param name="paper">The paper.</param>
	/// <param name="orientation">The job's orientation: <see cref="PageOrientation.PrinterDefault"/> follows the
	/// image's shape.</param>
	/// <param name="scaling">How to size it.</param>
	/// <returns>The page and where the image goes on it.</returns>
	public static ImagePlacement Place(PageImage image, MediaSize paper, PageOrientation orientation, ScalingMode scaling)
	{
		ArgumentNullException.ThrowIfNull(image);
		ArgumentNullException.ThrowIfNull(paper);

		// Its size at its resolution; a resolution given only one way is used both ways
		double? givenX = ImagePageLayout.Usable(image.DpiX);
		double? givenY = ImagePageLayout.Usable(image.DpiY);
		double dpiX = givenX ?? givenY ?? ImagePageLayout.DefaultDpi;
		double dpiY = givenY ?? givenX ?? ImagePageLayout.DefaultDpi;
		double storedWidth = image.Width * ImagePageLayout.PointsPerInch / dpiX;
		double storedHeight = image.Height * ImagePageLayout.PointsPerInch / dpiY;

		// Orientations 5 to 8 swap its width and height
		bool transposed = image.Orientation is >= 5 and <= 8;
		double width = transposed ? storedHeight : storedWidth;
		double height = transposed ? storedWidth : storedHeight;

		double shortSide = Math.Min(paper.WidthMillimeters, paper.HeightMillimeters) * ImagePageLayout.PointsPerInch / ImagePageLayout.MillimetresPerInch;
		double longSide = Math.Max(paper.WidthMillimeters, paper.HeightMillimeters) * ImagePageLayout.PointsPerInch / ImagePageLayout.MillimetresPerInch;

		bool landscape = orientation switch
		{
			PageOrientation.Landscape => true,
			PageOrientation.Portrait  => false,
			_                         => width > height,
		};

		double pageWidth = landscape ? longSide : shortSide;
		double pageHeight = landscape ? shortSide : longSide;

		// The margin never takes more than a tenth of the paper, however small it is
		double margin = Math.Min(ImagePageLayout.Margin, Math.Min(pageWidth, pageHeight) / 10);
		double fit = Math.Min((pageWidth - (2 * margin)) / width, (pageHeight - (2 * margin)) / height);

		double scale = scaling switch
		{
			ScalingMode.Fit  => fit,
			ScalingMode.Fill => Math.Max(pageWidth / width, pageHeight / height),
			ScalingMode.None => 1,
			_                => Math.Min(1, fit),
		};

		double drawnWidth = width * scale;
		double drawnHeight = height * scale;
		double x = (pageWidth - drawnWidth) / 2;
		double y = (pageHeight - drawnHeight) / 2;

		return new ImagePlacement(pageWidth, pageHeight, ImagePageLayout.Orient(image.Orientation, x, y, drawnWidth, drawnHeight));
	}

	/// <summary>
	/// Builds the matrix that draws the stored image into a rectangle as it is shown. EXIF's orientation says where
	/// the stored first row and first column go (EXIF 2.32, 4.6.5): 2 mirrors it, 3 turns it half round, 4 flips it,
	/// 5 mirrors it across its diagonal, 6 turns it a quarter clockwise, 7 across its other diagonal, and 8 a quarter
	/// anticlockwise.
	/// </summary>
	/// <param name="orientation">The orientation, 1 to 8; any other value is taken as 1.</param>
	/// <param name="x">The rectangle's left, in points.</param>
	/// <param name="y">Its bottom.</param>
	/// <param name="width">Its width, as shown.</param>
	/// <param name="height">Its height, as shown.</param>
	/// <returns>The matrix, mapping the unit square, its top edge the stored first row, to the rectangle.</returns>
	internal static PdfMatrix Orient(int orientation, double x, double y, double width, double height)
	{
		// In the unit square u runs along the stored rows and v up from the stored last row to the first
		return orientation switch
		{
			2 => new PdfMatrix(-width, 0, 0, height, x + width, y),
			3 => new PdfMatrix(-width, 0, 0, -height, x + width, y + height),
			4 => new PdfMatrix(width, 0, 0, -height, x, y + height),
			5 => new PdfMatrix(0, -height, -width, 0, x + width, y + height),
			6 => new PdfMatrix(0, -height, width, 0, x, y + height),
			7 => new PdfMatrix(0, height, width, 0, x, y),
			8 => new PdfMatrix(0, height, -width, 0, x + width, y),
			_ => new PdfMatrix(width, 0, 0, height, x, y),
		};
	}

	/// <summary>
	/// Keeps a resolution that can size an image: between 1 and 100,000 dots per inch.
	/// </summary>
	/// <param name="dpi">The resolution given.</param>
	/// <returns>It, or <see langword="null"/> when it is missing or absurd.</returns>
	private static double? Usable(double? dpi)
	{
		return dpi is >= 1 and <= 100_000 ? dpi : null;
	}
}
