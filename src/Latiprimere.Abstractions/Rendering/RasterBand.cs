using Latiprimere.Resources;

namespace Latiprimere.Rendering;

/// <summary>
/// Describes one horizontal band of a page rendered at a given pixel size.
/// </summary>
/// <param name="PageWidth">The width in pixels the whole page is rendered at.</param>
/// <param name="PageHeight">The height in pixels the whole page is rendered at.</param>
/// <param name="Top">The first row of the page to produce, from 0.</param>
/// <param name="Height">The number of rows to produce.</param>
/// <param name="Stride">The number of bytes from the start of one row to the next; at least <paramref
/// name="PageWidth"/> × <see cref="BytesPerPixel"/>.</param>
public readonly record struct RasterBand(int PageWidth, int PageHeight, int Top, int Height, int Stride)
{
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
	/// Gets how the band's pixels are laid out; <see cref="RasterPixelFormat.Bgra32"/> by default.
	/// </summary>
	/// <remarks>
	/// An <see cref="IRasterDocument"/> that can't render a format throws <see cref="NotSupportedException"/> for it,
	/// rather than rendering another.
	/// </remarks>
	public RasterPixelFormat PixelFormat { get; init; }

	/// <summary>
	/// Gets the number of bytes in each pixel of the band's <see cref="PixelFormat"/>.
	/// </summary>
	/// <exception cref="NotSupportedException">The pixel format isn't one this version knows.</exception>
	public int BytesPerPixel
	{
		get
		{
			return RasterBand.GetBytesPerPixel(this.PixelFormat);
		}
	}

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
	/// Gets the number of bytes in each pixel of a format.
	/// </summary>
	/// <param name="pixelFormat">The pixel format.</param>
	/// <returns>4 for <see cref="RasterPixelFormat.Bgra32"/>, 1 for <see cref="RasterPixelFormat.Gray8"/>.</returns>
	/// <exception cref="NotSupportedException">The pixel format isn't one this version knows.</exception>
	public static int GetBytesPerPixel(RasterPixelFormat pixelFormat)
	{
		return pixelFormat switch
		{
			RasterPixelFormat.Bgra32 => 4,
			RasterPixelFormat.Gray8  => 1,
			_                        => throw new NotSupportedException(ExceptionMessages.FormatPixelFormatNotSupported(pixelFormat)),
		};
	}

	/// <summary>
	/// Creates a band of 32-bit pixels (<see cref="RasterPixelFormat.Bgra32"/>) with the tightest stride for the page
	/// width.
	/// </summary>
	/// <param name="pageWidth">The width in pixels the whole page is rendered at.</param>
	/// <param name="pageHeight">The height in pixels the whole page is rendered at.</param>
	/// <param name="top">The first row of the page to produce.</param>
	/// <param name="height">The number of rows to produce.</param>
	/// <returns>The band.</returns>
	public static RasterBand Create(int pageWidth, int pageHeight, int top, int height)
	{
		return RasterBand.Create(pageWidth, pageHeight, top, height, RasterPixelFormat.Bgra32);
	}

	/// <summary>
	/// Creates a band with the tightest stride for the page width in the given pixel format.
	/// </summary>
	/// <param name="pageWidth">The width in pixels the whole page is rendered at.</param>
	/// <param name="pageHeight">The height in pixels the whole page is rendered at.</param>
	/// <param name="top">The first row of the page to produce.</param>
	/// <param name="height">The number of rows to produce.</param>
	/// <param name="pixelFormat">How the pixels are laid out.</param>
	/// <returns>The band.</returns>
	/// <exception cref="NotSupportedException">The pixel format isn't one this version knows.</exception>
	public static RasterBand Create(int pageWidth, int pageHeight, int top, int height, RasterPixelFormat pixelFormat)
	{
		int stride = pageWidth * RasterBand.GetBytesPerPixel(pixelFormat);

		return new RasterBand(pageWidth, pageHeight, top, height, stride) { PixelFormat = pixelFormat };
	}
}
