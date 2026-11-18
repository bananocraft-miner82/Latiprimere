namespace Latiprimere.Testing.Raster;

/// <summary>
/// A raster document read back by <see cref="RasterDocumentReader"/>.
/// </summary>
public sealed record RasterDocument
{
	/// <summary>Gets the format: <c>image/pwg-raster</c> or <c>image/urf</c>.</summary>
	public required string Format { get; init; }

	/// <summary>Gets the page count Apple raster's file header gives, or 0 for PWG raster, which has none.</summary>
	public int PageCount { get; init; }

	/// <summary>Gets the pages.</summary>
	public required IReadOnlyList<RasterPage> Pages { get; init; }
}
