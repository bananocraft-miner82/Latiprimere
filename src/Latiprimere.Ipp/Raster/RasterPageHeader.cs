namespace Latiprimere.Ipp.Raster;

/// <summary>
/// What a raster page header says about the page that follows it (ADR 0034).
/// </summary>
internal sealed record RasterPageHeader
{
	/// <summary>Gets the page's width in pixels: the paper's width at the resolution.</summary>
	public required int Width { get; init; }

	/// <summary>Gets the page's height in pixels.</summary>
	public required int Height { get; init; }

	/// <summary>Gets the resolution, the same across and down, in dots per inch.</summary>
	public required int Dpi { get; init; }

	/// <summary>Gets the pixels' colour space.</summary>
	public required RasterColorSpace ColorSpace { get; init; }

	/// <summary>Gets the paper, whose PWG name and size the header carries.</summary>
	public required MediaSize Media { get; init; }

	/// <summary>Gets the number of pages in the document.</summary>
	public required int TotalPages { get; init; }

	/// <summary>Gets whether the job is two-sided.</summary>
	public bool Duplex { get; init; }

	/// <summary>Gets whether a two-sided job turns on the short edge.</summary>
	public bool Tumble { get; init; }

	/// <summary>Gets the IPP <c>print-quality</c>, 3 to 5, or 0 for the printer's default.</summary>
	public int PrintQuality { get; init; }

	/// <summary>Gets whether this page's pixels are mirrored left to right, as some printers want back sides.</summary>
	public bool FlipX { get; init; }

	/// <summary>Gets whether this page's lines are in reverse order, bottom to top.</summary>
	public bool FlipY { get; init; }

	/// <summary>Gets the left edge of the page's content, in pixels from the left.</summary>
	public int ImageLeft { get; init; }

	/// <summary>Gets the top edge of the page's content, in pixels from the top.</summary>
	public int ImageTop { get; init; }

	/// <summary>Gets the right edge of the page's content, exclusive.</summary>
	public int ImageRight { get; init; }

	/// <summary>Gets the bottom edge of the page's content, exclusive.</summary>
	public int ImageBottom { get; init; }
}
