namespace Latiprimere.Ipp.Raster;

/// <summary>
/// How a PDF is sent to a printer that can't take PDF: the raster format, how its pages are encoded, and whether
/// the upload is gzipped.
/// </summary>
internal sealed record RasterJobPlan
{
	/// <summary>Gets the format's MIME type, sent as <c>document-format</c>.</summary>
	public required string MimeType { get; init; }

	/// <summary>Gets how the pages are encoded.</summary>
	public required RasterSettings Settings { get; init; }

	/// <summary>Gets a value indicating whether the upload is gzipped, sent as <c>compression</c>.</summary>
	public bool Gzip { get; init; }
}
