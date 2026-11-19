namespace Latiprimere.Ipp.Raster;

/// <summary>
/// How a printer wants the back of each two-sided sheet, from PWG raster's <c>pwg-raster-document-sheet-back</c> or
/// Apple raster's <c>DM</c> value in <c>urf-supported</c> (ADR 0034).
/// </summary>
/// <remarks>
/// A duplexing unit turns the sheet over, so some printers expect the back side's image already turned to match. The
/// transforms are libcups': see <see cref="RasterDocumentEncoder"/>.
/// </remarks>
internal enum RasterBackSide
{
	/// <summary>As the front: <c>normal</c>, <c>DM1</c>.</summary>
	Normal = 0,

	/// <summary>Mirrored top to bottom for long-edge, left to right for short-edge: <c>flipped</c>, <c>DM2</c>.</summary>
	Flipped = 1,

	/// <summary>Turned upside down for long-edge, as the front for short-edge: <c>rotated</c>, <c>DM3</c>.</summary>
	Rotated = 2,

	/// <summary>As the front for long-edge, upside down for short-edge: <c>manual-tumble</c>, <c>DM4</c>.</summary>
	ManualTumble = 3,
}
