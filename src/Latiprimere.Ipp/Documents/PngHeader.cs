namespace Latiprimere.Ipp.Documents;

/// <summary>
/// A PNG's chunks as read: its header, palette, transparency, resolution and orientation, and where its image data
/// is in the file.
/// </summary>
internal sealed record PngHeader
{
	/// <summary>Gets the whole file, which the image data is read from.</summary>
	public required byte[] Data { get; init; }

	/// <summary>Gets the width in pixels.</summary>
	public required int Width { get; init; }

	/// <summary>Gets the height in pixels.</summary>
	public required int Height { get; init; }

	/// <summary>Gets the bits in each sample, or each palette index: 1, 2, 4, 8 or 16.</summary>
	public required int BitDepth { get; init; }

	/// <summary>
	/// Gets the colour type: 0 grey, 2 colour, 3 palette, 4 grey with alpha, 6 colour with alpha.
	/// </summary>
	public required int ColorType { get; init; }

	/// <summary>Gets a value indicating whether the image data is interlaced with Adam7.</summary>
	public required bool Interlaced { get; init; }

	/// <summary>Gets the palette's red, green and blue bytes, or <see langword="null"/> without one.</summary>
	public byte[]? Palette { get; init; }

	/// <summary>
	/// Gets the <c>tRNS</c> chunk's data: an alpha for each palette entry, or the one grey or colour that is fully
	/// transparent; or <see langword="null"/> without one.
	/// </summary>
	public byte[]? Transparency { get; init; }

	/// <summary>Gets the horizontal resolution in dots per inch, or <see langword="null"/> if not given.</summary>
	public double? DpiX { get; init; }

	/// <summary>Gets the vertical resolution in dots per inch, or <see langword="null"/> if not given.</summary>
	public double? DpiY { get; init; }

	/// <summary>Gets how the image is turned or mirrored for showing, from an <c>eXIf</c> chunk, 1 to 8.</summary>
	public int Orientation { get; init; } = 1;

	/// <summary>Gets each <c>IDAT</c> chunk's data, as its offset and length in <see cref="Data"/>, in order.</summary>
	public required IReadOnlyList<(int Offset, int Length)> ImageData { get; init; }

	/// <summary>Gets the samples in each pixel: 1 for grey or a palette index, 2, 3 or 4.</summary>
	public int Channels
	{
		get
		{
			return this.ColorType switch
			{
				2 => 3,
				4 => 2,
				6 => 4,
				_ => 1,
			};
		}
	}

	/// <summary>Gets a value indicating whether the image is shown in grey, as colour types 0 and 4 are.</summary>
	public bool IsGrey
	{
		get
		{
			return this.ColorType is 0 or 4;
		}
	}
}
