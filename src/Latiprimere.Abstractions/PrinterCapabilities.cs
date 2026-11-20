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

	/// <summary>
	/// Gets the document formats the printer can be sent, whether it takes them itself or Latiprimere converts them.
	/// </summary>
	/// <remarks>
	/// A format whose conversion needs a PDF rasteriser, such as a PDF to the Windows spooler, is listed only when
	/// one is registered. A format that isn't listed is refused by <see cref="IPrinter.SubmitAsync"/> with
	/// <see cref="NotSupportedException"/>.
	/// </remarks>
	public IReadOnlyList<DocumentFormat> DocumentFormats { get; init; } = Array.Empty<DocumentFormat>();

	/// <summary>
	/// Gets the printer's other settings, such as print quality, finishing or a driver's own settings, with the keys
	/// and values to set them through <see cref="PrintOptions.VendorOptions"/>.
	/// </summary>
	/// <remarks>
	/// IPP printers and CUPS queues list the job attributes they accept with their supported values, under
	/// <c>ipp:</c> keys. Windows printers list their driver's Print Capabilities features and parameters, under
	/// <c>windows:feature:</c> and <c>windows:parameter:</c> keys. Settings the typed options cover aren't listed.
	/// </remarks>
	public IReadOnlyList<VendorOption> VendorOptions { get; init; } = Array.Empty<VendorOption>();
}
