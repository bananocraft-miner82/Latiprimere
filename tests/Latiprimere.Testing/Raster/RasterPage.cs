namespace Latiprimere.Testing.Raster;

/// <summary>
/// A page of a raster document, with what its header says and its lines decompressed.
/// </summary>
public sealed record RasterPage
{
	/// <summary>Gets the width in pixels.</summary>
	public required int Width { get; init; }

	/// <summary>Gets the height in lines.</summary>
	public required int Height { get; init; }

	/// <summary>Gets the bits in a pixel: 1, 8 or 24.</summary>
	public required int BitsPerPixel { get; init; }

	/// <summary>Gets the bits in a colour.</summary>
	public int BitsPerColor { get; init; }

	/// <summary>Gets the bytes in a line.</summary>
	public required int BytesPerLine { get; init; }

	/// <summary>Gets the resolution across.</summary>
	public int DpiX { get; init; }

	/// <summary>Gets the resolution down.</summary>
	public int DpiY { get; init; }

	/// <summary>Gets the colour space's number: PWG's (3 black, 18 sGray, 19 sRGB) or Apple's (0 sGray, 1 sRGB).</summary>
	public int ColorSpace { get; init; }

	/// <summary>Gets the number of colours a pixel has.</summary>
	public int NumColors { get; init; }

	/// <summary>Gets PWG raster's media class, which must be <c>PwgRaster</c>; empty for Apple raster.</summary>
	public string MediaClass { get; init; } = string.Empty;

	/// <summary>Gets PWG raster's paper name, such as <c>iso_a4_210x297mm</c>; empty for Apple raster.</summary>
	public string PageSizeName { get; init; } = string.Empty;

	/// <summary>Gets PWG raster's paper size in points.</summary>
	public (int Width, int Height) PageSizePoints { get; init; }

	/// <summary>Gets whether the page is two-sided.</summary>
	public bool Duplex { get; init; }

	/// <summary>Gets whether a two-sided page turns on the short edge.</summary>
	public bool Tumble { get; init; }

	/// <summary>Gets Apple raster's duplex byte: 1 one-sided, 2 short-edge, 3 long-edge.</summary>
	public int AppleDuplexMode { get; init; }

	/// <summary>Gets the document's page count from PWG raster's header.</summary>
	public int TotalPages { get; init; }

	/// <summary>Gets PWG raster's cross-feed transform: 1, or -1 for mirrored left to right.</summary>
	public int CrossFeedTransform { get; init; }

	/// <summary>Gets PWG raster's feed transform: 1, or -1 for lines bottom to top.</summary>
	public int FeedTransform { get; init; }

	/// <summary>Gets PWG raster's image box: left, top, right, bottom, in pixels.</summary>
	public (int Left, int Top, int Right, int Bottom) ImageBox { get; init; }

	/// <summary>Gets the print quality the header carries.</summary>
	public int PrintQuality { get; init; }

	/// <summary>Gets the header's bytes as written.</summary>
	public required byte[] RawHeader { get; init; }

	/// <summary>Gets the lines, top to bottom as sent, each <see cref="BytesPerLine"/> long.</summary>
	public required byte[][] Lines { get; init; }

	/// <summary>
	/// Gets a pixel's grey, 0 for black to 255 for white, whatever the colour space.
	/// </summary>
	/// <param name="x">The pixel's column.</param>
	/// <param name="y">The pixel's line.</param>
	/// <returns>The grey; for colour, the same luma weights Latiprimere uses.</returns>
	public int GetGrey(int x, int y)
	{
		byte[] line = this.Lines[y];

		return this.BitsPerPixel switch
		{
			1 => (line[x / 8] & (0x80 >> (x % 8))) != 0 ? 0 : 255,
			8 => line[x],
			_ => ((77 * line[x * 3]) + (150 * line[(x * 3) + 1]) + (29 * line[(x * 3) + 2]) + 128) >> 8,
		};
	}

	/// <summary>
	/// Gets the share of the page that isn't white.
	/// </summary>
	/// <returns>0 for a blank page, 1 for one covered.</returns>
	public double GetInk()
	{
		long inked = 0;

		for (int y = 0; y < this.Height; y++)
		{
			for (int x = 0; x < this.Width; x++)
			{
				inked += this.GetGrey(x, y) < 250 ? 1 : 0;
			}
		}

		return (double)inked / ((long)this.Width * this.Height);
	}
}
