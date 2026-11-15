namespace Latiprimere;

/// <summary>
/// What a printer can do, as reported by its print system.
/// </summary>
/// <remarks>
/// <para>
/// An empty list, or a <see langword="null"/> value, means the print system didn't say, not that the printer can't.
/// Lists never contain the <c>PrinterDefault</c> values.
/// </para>
/// <para>
/// Records compare lists by reference, so two capability records read separately are not equal even when they
/// describe the same printer. Compare the lists' contents instead, and compare paper sizes by
/// <see cref="MediaSize.Name"/>.
/// </para>
/// </remarks>
public sealed record PrinterCapabilities
{
	/// <summary>
	/// Gets the paper sizes the printer supports.
	/// </summary>
	public IReadOnlyList<MediaSize> MediaSizes { get; init; } = Array.Empty<MediaSize>();

	/// <summary>
	/// Gets the paper size used when a job doesn't choose one, or <see langword="null"/> if not reported.
	/// </summary>
	public MediaSize? DefaultMediaSize { get; init; }

	/// <summary>
	/// Gets the one- and two-sided modes the printer supports.
	/// </summary>
	public IReadOnlyList<DuplexMode> DuplexModes { get; init; } = Array.Empty<DuplexMode>();

	/// <summary>
	/// Gets the duplex mode used when a job doesn't choose one, or <see langword="null"/> if not reported.
	/// </summary>
	public DuplexMode? DefaultDuplexMode { get; init; }

	/// <summary>
	/// Gets the colour modes the printer supports.
	/// </summary>
	public IReadOnlyList<ColorMode> ColorModes { get; init; } = Array.Empty<ColorMode>();

	/// <summary>
	/// Gets the colour mode used when a job doesn't choose one, or <see langword="null"/> if not reported.
	/// </summary>
	public ColorMode? DefaultColorMode { get; init; }

	/// <summary>
	/// Gets the most copies one job can ask for, or <see langword="null"/> if not reported.
	/// </summary>
	public int? MaxCopies { get; init; }

	/// <summary>
	/// Gets the page orientations the printer supports.
	/// </summary>
	public IReadOnlyList<PageOrientation> Orientations { get; init; } = Array.Empty<PageOrientation>();

	/// <summary>
	/// Gets the printer's paper sources.
	/// </summary>
	public IReadOnlyList<InputTray> InputTrays { get; init; } = Array.Empty<InputTray>();
}
