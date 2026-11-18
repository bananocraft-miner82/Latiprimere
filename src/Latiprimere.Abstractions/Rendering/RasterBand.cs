namespace Latiprimere.Rendering;

/// <summary>
/// Describes one horizontal band of a page rendered at a given pixel size.
/// </summary>
/// <param name="PageWidth">The width in pixels the whole page is rendered at.</param>
/// <param name="PageHeight">The height in pixels the whole page is rendered at.</param>
/// <param name="Top">The first row of the page to produce, from 0.</param>
/// <param name="Height">The number of rows to produce.</param>
/// <param name="Stride">The number of bytes from the start of one row to the next; at least <paramref
/// name="PageWidth"/> × 4.</param>
public readonly record struct RasterBand(int PageWidth, int PageHeight, int Top, int Height, int Stride)
{
	/// <summary>
	/// The number of bytes in each pixel (B, G, R and an unused byte).
	/// </summary>
	public static readonly int BytesPerPixel = 4;

	/// <summary>
	/// Gets how far the page is turned as it is rendered; <see cref="RasterRotation.None"/> by default.
	/// <see cref="PageWidth"/> and <see cref="PageHeight"/> are the turned page's size.
	/// </summary>
	/// <remarks>
	/// An <see cref="IRasterDocument"/> that can't turn pages throws <see cref="NotSupportedException"/> for any other
	/// value, rather than rendering the page unturned.
	/// </remarks>
	public RasterRotation Rotation { get; init; }

	/// <summary>
	/// Gets the number of bytes the band's pixels occupy.
	/// </summary>
	public long ByteCount
	{
		get
		{
			return (long)this.Stride * this.Height;
		}
	}

	/// <summary>
	/// Creates a band with the tightest stride for the page width.
	/// </summary>
	/// <param name="pageWidth">The width in pixels the whole page is rendered at.</param>
	/// <param name="pageHeight">The height in pixels the whole page is rendered at.</param>
	/// <param name="top">The first row of the page to produce.</param>
	/// <param name="height">The number of rows to produce.</param>
	/// <returns>The band.</returns>
	public static RasterBand Create(int pageWidth, int pageHeight, int top, int height)
	{
		return new RasterBand(pageWidth, pageHeight, top, height, pageWidth * RasterBand.BytesPerPixel);
	}
}
