namespace Latiprimere.Rendering;

/// <summary>
/// Describes how a band is rendered, beyond its size, pixel format and rotation.
/// </summary>
/// <remarks>
/// Properties may be added in a later version, each with a default that keeps how pages render today, so code that
/// passes these options keeps working. An <see cref="IRasterDocument"/> that can't honour a setting other than its
/// default throws <see cref="NotSupportedException"/> rather than ignoring it.
/// </remarks>
public sealed record RasterRenderOptions
{
	/// <summary>
	/// The options used when a caller specifies none: everything smoothed, as on screen.
	/// </summary>
	public static readonly RasterRenderOptions Default = new RasterRenderOptions();

	/// <summary>
	/// Gets a value indicating whether the edges of text are smoothed (anti-aliased); <see langword="true"/> by default.
	/// </summary>
	/// <remarks>
	/// Turn it off for small type on a printer that prints only black dots, such as a 203 dpi thermal printer, where
	/// smoothed edges turn grey and blur when the band is made black and white.
	/// </remarks>
	public bool SmoothText { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether the edges of lines and shapes are smoothed (anti-aliased);
	/// <see langword="true"/> by default.
	/// </summary>
	/// <remarks>
	/// Turn it off for barcodes and rules on a printer that prints only black dots, so that bars keep sharp edges and
	/// scan.
	/// </remarks>
	public bool SmoothLines { get; init; } = true;

	/// <summary>
	/// Gets a value indicating whether images are smoothed as they are scaled; <see langword="true"/> by default.
	/// </summary>
	public bool SmoothImages { get; init; } = true;
}
