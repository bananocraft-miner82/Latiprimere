namespace Latiprimere.Ipp.Raster;

/// <summary>
/// The pixel formats Latiprimere writes raster pages in (ADR 0034).
/// </summary>
internal enum RasterColorSpace
{
	/// <summary>One bit a pixel, 1 for ink: PWG raster's <c>black_1</c>. Apple raster has no 1-bit form.</summary>
	Black1 = 0,

	/// <summary>Eight bits a pixel, 0 for black and 255 for white: PWG raster's <c>sgray_8</c>, Apple raster's
	/// <c>W8</c>.</summary>
	Gray8 = 1,

	/// <summary>Eight bits for each of red, green and blue: PWG raster's <c>srgb_8</c>, Apple raster's
	/// <c>SRGB24</c>.</summary>
	Srgb8 = 2,
}
