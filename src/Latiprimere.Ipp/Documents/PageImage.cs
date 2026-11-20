namespace Latiprimere.Ipp.Documents;

/// <summary>
/// An image read from a JPEG or PNG, ready to be placed on a page: its size as stored, the resolution and
/// orientation it gives, and its samples as a PDF image.
/// </summary>
internal sealed record PageImage
{
	/// <summary>Gets the width as stored, in pixels.</summary>
	public required int Width { get; init; }

	/// <summary>Gets the height as stored, in pixels.</summary>
	public required int Height { get; init; }

	/// <summary>Gets the horizontal resolution as stored, in dots per inch, or <see langword="null"/> if not given.</summary>
	public double? DpiX { get; init; }

	/// <summary>Gets the vertical resolution as stored, in dots per inch, or <see langword="null"/> if not given.</summary>
	public double? DpiY { get; init; }

	/// <summary>Gets how the stored image is turned or mirrored for showing, 1 to 8 as EXIF numbers them.</summary>
	public int Orientation { get; init; } = 1;

	/// <summary>Gets the samples, as an image the PDF writer draws.</summary>
	public required PdfImage Image { get; init; }
}
