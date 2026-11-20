using Latiprimere.Ipp.Documents;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// How to turn a document into raster pages for a printer (ADR 0034).
/// </summary>
internal sealed record RasterSettings
{
	/// <summary>The default limit on the memory one rendered band takes: 16 MiB.</summary>
	internal const long DefaultMaxBandBytes = 16L * 1024 * 1024;

	/// <summary>Gets the format to write.</summary>
	public required RasterFormat Format { get; init; }

	/// <summary>Gets the colour space of the pixels.</summary>
	public required RasterColorSpace ColorSpace { get; init; }

	/// <summary>Gets the resolution, in dots per inch, the same across and down.</summary>
	public required int Dpi { get; init; }

	/// <summary>Gets the paper: each page is its whole size, with the document's page fitted inside.</summary>
	public required MediaSize Media { get; init; }

	/// <summary>Gets one- or two-sided printing; <see cref="DuplexMode.PrinterDefault"/> is written as
	/// one-sided.</summary>
	public DuplexMode Duplex { get; init; } = DuplexMode.OneSided;

	/// <summary>Gets how the printer wants the back of each two-sided sheet.</summary>
	public RasterBackSide BackSide { get; init; } = RasterBackSide.Normal;

	/// <summary>Gets the orientation: <see cref="PageOrientation.PrinterDefault"/> follows each page's own
	/// shape.</summary>
	public PageOrientation Orientation { get; init; } = PageOrientation.PrinterDefault;

	/// <summary>Gets the IPP <c>print-quality</c> to put in the page headers, 3 to 5, or 0 for the printer's
	/// default.</summary>
	public int PrintQuality { get; init; }

	/// <summary>Gets the most memory one rendered band may take; a page is rendered in as many bands as that
	/// needs.</summary>
	public long MaxBandBytes { get; init; } = RasterSettings.DefaultMaxBandBytes;

	/// <summary>Gets the lines in each strip of a PCLm page, from the printer's <c>pclm-strip-height-preferred</c>.
	/// Defaults to 16, Mopria's own default.</summary>
	public int PclmStripHeight { get; init; } = 16;

	/// <summary>Gets how each strip of a PCLm page is compressed: Flate or run-length, as the printer prefers.
	/// Defaults to Flate.</summary>
	public PdfStreamEncoding PclmCompression { get; init; } = PdfStreamEncoding.Flate;
}
