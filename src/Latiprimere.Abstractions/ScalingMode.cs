namespace Latiprimere;

/// <summary>
/// Enumerates the ways an image is sized on the paper, as IPP's <c>print-scaling</c> has them.
/// </summary>
/// <remarks>
/// They apply to images, from <see cref="PrintContent.FromJpeg"/> and <see cref="PrintContent.FromPng"/>. PDF pages
/// keep their own size, shrunk to fit paper they're too big for.
/// </remarks>
public enum ScalingMode
{
	/// <summary>
	/// Use the printer's default: an image larger than the paper is shrunk to fit it, and a smaller one is printed at
	/// its own size, as IPP's <c>auto</c> does.
	/// </summary>
	PrinterDefault = 0,

	/// <summary>
	/// Scale the image up or down to fit within the paper's printable area, keeping its proportions.
	/// </summary>
	Fit = 1,

	/// <summary>
	/// Scale the image up or down to cover the whole paper, keeping its proportions and cutting off what overhangs.
	/// </summary>
	Fill = 2,

	/// <summary>
	/// Print the image at its own size, from the resolution it gives, centred, cutting off what doesn't fit.
	/// </summary>
	None = 3,
}
