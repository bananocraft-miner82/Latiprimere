using Latiprimere.Rendering;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// Turns rendered pixels into a raster colour space's bytes: colour from B, G, R, unused pixels, and grey and 1-bit
/// black from 8-bit grey ones, which PDFium renders directly in a quarter of the memory (ADR 0054).
/// </summary>
internal static class RasterPixels
{
	/// <summary>Bits in a byte.</summary>
	private const int BitsPerByte = 8;

	/// <summary>The value of white in 8-bit grey and colour.</summary>
	private const byte White = 0xFF;

	/// <summary>The value of no ink in 1-bit black: a byte of eight white pixels.</summary>
	private const byte NoInk = 0x00;

	/// <summary>The side of the ordered dither matrix.</summary>
	private const int DitherSize = 8;

	/// <summary>
	/// An 8×8 Bayer matrix: each cell's place, 0 to 63, in the order pixels turn black as grey darkens, which spreads
	/// mid-greys into an even pattern instead of a hard threshold.
	/// </summary>
	private static readonly byte[] DitherOrder =
	[
		0, 32, 8, 40, 2, 34, 10, 42,
		48, 16, 56, 24, 50, 18, 58, 26,
		12, 44, 4, 36, 14, 46, 6, 38,
		60, 28, 52, 20, 62, 30, 54, 22,
		3, 35, 11, 43, 1, 33, 9, 41,
		51, 19, 59, 27, 49, 17, 57, 25,
		15, 47, 7, 39, 13, 45, 5, 37,
		63, 31, 55, 23, 61, 29, 53, 21,
	];

	/// <summary>
	/// Gets the pixel format a colour space is rendered in: grey for grey and 1-bit black, colour otherwise.
	/// </summary>
	/// <param name="colorSpace">The colour space.</param>
	/// <returns>The pixel format.</returns>
	internal static RasterPixelFormat RenderFormat(RasterColorSpace colorSpace)
	{
		return colorSpace == RasterColorSpace.Srgb8 ? RasterPixelFormat.Bgra32 : RasterPixelFormat.Gray8;
	}

	/// <summary>
	/// Gets the bits a pixel takes.
	/// </summary>
	/// <param name="colorSpace">The colour space.</param>
	/// <returns>1, 8 or 24.</returns>
	internal static int BitsPerPixel(RasterColorSpace colorSpace)
	{
		return colorSpace switch
		{
			RasterColorSpace.Black1 => 1,
			RasterColorSpace.Gray8 => 8,
			_ => 24,
		};
	}

	/// <summary>
	/// Gets the bytes in the unit the line compression works in: a byte of eight 1-bit pixels, a grey pixel, or a
	/// colour pixel.
	/// </summary>
	/// <param name="colorSpace">The colour space.</param>
	/// <returns>1 or 3.</returns>
	internal static int BytesPerUnit(RasterColorSpace colorSpace)
	{
		return colorSpace == RasterColorSpace.Srgb8 ? 3 : 1;
	}

	/// <summary>
	/// Gets the bytes in a line of pixels.
	/// </summary>
	/// <param name="colorSpace">The colour space.</param>
	/// <param name="width">The pixels in the line.</param>
	/// <returns>The bytes; a 1-bit line is padded to a whole byte.</returns>
	internal static int BytesPerLine(RasterColorSpace colorSpace, int width)
	{
		return colorSpace switch
		{
			RasterColorSpace.Black1 => (width + RasterPixels.BitsPerByte - 1) / RasterPixels.BitsPerByte,
			RasterColorSpace.Gray8 => width,
			_ => width * 3,
		};
	}

	/// <summary>
	/// Fills a line with white.
	/// </summary>
	/// <param name="colorSpace">The colour space.</param>
	/// <param name="line">The line's bytes.</param>
	internal static void FillWhite(RasterColorSpace colorSpace, Span<byte> line)
	{
		line.Fill(colorSpace == RasterColorSpace.Black1 ? RasterPixels.NoInk : RasterPixels.White);
	}

	/// <summary>
	/// Reverses a line of rendered pixels, left to right, for a back side printed mirrored.
	/// </summary>
	/// <param name="pixels">The line.</param>
	/// <param name="width">The pixels in it.</param>
	/// <param name="pixelFormat">How the line's pixels are laid out.</param>
	internal static void Mirror(Span<byte> pixels, int width, RasterPixelFormat pixelFormat)
	{
		if (pixelFormat == RasterPixelFormat.Gray8)
		{
			pixels[..width].Reverse();

			return;
		}

		Span<uint> words = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(pixels[..(width * RasterBand.GetBytesPerPixel(pixelFormat))]);
		words.Reverse();
	}

	/// <summary>
	/// Converts a line of rendered pixels.
	/// </summary>
	/// <param name="pixels">The line, in the colour space's <see cref="RenderFormat"/>: four bytes a pixel, B, G, R and
	/// unused, for colour; a byte of grey a pixel otherwise.</param>
	/// <param name="width">The pixels in it.</param>
	/// <param name="colorSpace">The colour space to convert to.</param>
	/// <param name="row">The line's row on the page, which places the dither pattern for 1-bit black.</param>
	/// <param name="output">The converted line, <see cref="BytesPerLine"/> long.</param>
	internal static void Convert(ReadOnlySpan<byte> pixels, int width, RasterColorSpace colorSpace, int row, Span<byte> output)
	{
		switch (colorSpace)
		{
			case RasterColorSpace.Srgb8:
				for (int x = 0, source = 0, target = 0; x < width; x++, source += RasterBand.GetBytesPerPixel(RasterPixelFormat.Bgra32), target += 3)
				{
					output[target] = pixels[source + 2];
					output[target + 1] = pixels[source + 1];
					output[target + 2] = pixels[source];
				}

				break;

			case RasterColorSpace.Gray8:
				pixels[..width].CopyTo(output);

				break;

			default:
				RasterPixels.ConvertToBlack(pixels, width, row, output);

				break;
		}
	}

	/// <summary>
	/// Converts a line to 1-bit black, dithered, the leftmost pixel in each byte's highest bit.
	/// </summary>
	/// <param name="pixels">The line, a byte of grey a pixel.</param>
	/// <param name="width">The pixels in it.</param>
	/// <param name="row">The line's row, which picks the dither matrix's row.</param>
	/// <param name="output">The converted line.</param>
	private static void ConvertToBlack(ReadOnlySpan<byte> pixels, int width, int row, Span<byte> output)
	{
		output[..RasterPixels.BytesPerLine(RasterColorSpace.Black1, width)].Clear();
		int matrixRow = (row % RasterPixels.DitherSize) * RasterPixels.DitherSize;

		for (int x = 0; x < width; x++)
		{
			// Ink where the grey is darker than the cell's threshold: 0 is always ink, 255 never
			int threshold = (RasterPixels.DitherOrder[matrixRow + (x % RasterPixels.DitherSize)] * 4) + 2;

			if (pixels[x] < threshold)
			{
				output[x / RasterPixels.BitsPerByte] |= (byte)(0x80 >> (x % RasterPixels.BitsPerByte));
			}
		}
	}
}
