using Latiprimere.Rendering;

namespace Latiprimere.Windows.Gdi;

/// <summary>
/// Turns rendered pixels grey, so a monochrome job prints without colour whatever the driver does with the DEVMODE's
/// colour setting.
/// </summary>
/// <remarks>
/// Microsoft Print to PDF ignores <c>dmColor</c> and keeps colour (seen 2026-11-17, end-to-end harness); other drivers
/// may too. Since Latiprimere renders every page itself, it greys the pixels before the driver sees them.
/// </remarks>
internal static class GreyscaleConverter
{
	/// <summary>
	/// Replaces each pixel with its luma, by the ITU-R BT.601 weights the eye's sensitivity suggests: 0.299 red,
	/// 0.587 green, 0.114 blue.
	/// </summary>
	/// <param name="pixels">Pixels in the rasteriser's layout: blue, green, red and an unused byte each, which is left
	/// as it is. A length that isn't a whole number of pixels leaves the last bytes unchanged.</param>
	internal static void ToGrey(Span<byte> pixels)
	{
		int end = pixels.Length - (pixels.Length % RasterBand.BytesPerPixel);

		for (int offset = 0; offset < end; offset += RasterBand.BytesPerPixel)
		{
			// Weights out of 256: 77 + 150 + 29; adding 128 rounds to the nearest
			int blue = pixels[offset];
			int green = pixels[offset + 1];
			int red = pixels[offset + 2];
			byte luma = (byte)(((77 * red) + (150 * green) + (29 * blue) + 128) >> 8);

			pixels[offset] = luma;
			pixels[offset + 1] = luma;
			pixels[offset + 2] = luma;
		}
	}
}
