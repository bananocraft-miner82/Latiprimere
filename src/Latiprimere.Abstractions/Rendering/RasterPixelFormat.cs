namespace Latiprimere.Rendering;

/// <summary>
/// How a rendered band's pixels are laid out in memory.
/// </summary>
/// <remarks>
/// Values may be added in a later version, such as a format for a kind of printer not yet covered. An
/// <see cref="IRasterDocument"/> throws <see cref="NotSupportedException"/> for a format it can't render, and code
/// that reads pixels should do the same for a format it doesn't know, rather than guess its layout.
/// </remarks>
public enum RasterPixelFormat
{
	/// <summary>
	/// 32 bits a pixel, in B, G, R, unused byte order: the layout of a top-down 32-bit Windows device-independent
	/// bitmap.
	/// </summary>
	Bgra32 = 0,

	/// <summary>
	/// 8 bits a pixel of grey, 0 black and 255 white, for monochrome printers: a quarter of the memory of
	/// <see cref="Bgra32"/>.
	/// </summary>
	Gray8 = 1,
}
