namespace Latiprimere.Ipp.Documents;

/// <summary>
/// What an image's EXIF data says about how to show it.
/// </summary>
/// <param name="Orientation">How the stored image is turned or mirrored for showing, 1 to 8 as EXIF numbers them;
/// 1, as stored, when not given.</param>
/// <param name="DpiX">Its horizontal resolution in dots per inch, or <see langword="null"/> when not given.</param>
/// <param name="DpiY">Its vertical resolution in dots per inch, or <see langword="null"/> when not given.</param>
internal sealed record ExifInfo(int Orientation, double? DpiX, double? DpiY)
{
	/// <summary>Gets what an image without EXIF data has: shown as stored, with no resolution.</summary>
	public static ExifInfo None { get; } = new ExifInfo(1, null, null);
}
