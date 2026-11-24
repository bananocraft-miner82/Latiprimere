using Latiprimere.Rendering;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// A page as PDFium rendered it: four bytes a pixel, blue, green, red and alpha.
/// </summary>
/// <param name="Pixels">The pixels, top row first.</param>
/// <param name="Stride">The bytes in each row.</param>
internal sealed record RenderedPage(byte[] Pixels, int Stride)
{
	/// <summary>
	/// Gets a pixel's colour.
	/// </summary>
	/// <param name="x">Its column.</param>
	/// <param name="y">Its row, from the top.</param>
	/// <returns>Its red, green and blue.</returns>
	public (int R, int G, int B) GetRgb(int x, int y)
	{
		int index = (y * this.Stride) + (x * RasterBand.GetBytesPerPixel(RasterPixelFormat.Bgra32));

		return (this.Pixels[index + 2], this.Pixels[index + 1], this.Pixels[index]);
	}

	/// <summary>
	/// Counts the dark pixels in a rectangle.
	/// </summary>
	/// <param name="left">Its left column.</param>
	/// <param name="top">Its top row.</param>
	/// <param name="width">Its width.</param>
	/// <param name="height">Its height.</param>
	/// <returns>How many pixels have every component below 128.</returns>
	public int CountDark(int left, int top, int width, int height)
	{
		int count = 0;

		for (int y = top; y < top + height; y++)
		{
			for (int x = left; x < left + width; x++)
			{
				(int r, int g, int b) = this.GetRgb(x, y);
				count += r < 128 && g < 128 && b < 128 ? 1 : 0;
			}
		}

		return count;
	}
}
