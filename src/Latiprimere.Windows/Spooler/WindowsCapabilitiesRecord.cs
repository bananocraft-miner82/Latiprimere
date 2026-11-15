namespace Latiprimere.Windows.Spooler;

/// <summary>
/// A printer driver's capabilities and defaults, as the spooler reports them.
/// </summary>
internal sealed record WindowsCapabilitiesRecord
{
	/// <summary>Gets the paper sizes.</summary>
	public IReadOnlyList<WindowsPaperRecord> Papers { get; init; } = Array.Empty<WindowsPaperRecord>();

	/// <summary>Gets the paper sources.</summary>
	public IReadOnlyList<WindowsBinRecord> Bins { get; init; } = Array.Empty<WindowsBinRecord>();

	/// <summary>Gets a value indicating whether the printer can print on both sides.</summary>
	public bool CanDuplex { get; init; }

	/// <summary>Gets a value indicating whether the printer can print in colour.</summary>
	public bool IsColorDevice { get; init; }

	/// <summary>Gets the most copies the driver can make, or <see langword="null"/> if it doesn't say.</summary>
	public int? MaxCopies { get; init; }

	/// <summary>Gets a value indicating whether the printer can print landscape.</summary>
	public bool CanPrintLandscape { get; init; }

	/// <summary>Gets the default DMPAPER_* identifier, or <see langword="null"/> if the DEVMODE sets none.</summary>
	public short? DefaultPaperId { get; init; }

	/// <summary>Gets the default DMDUP_* value, or <see langword="null"/> if the DEVMODE doesn't set one.</summary>
	public short? DefaultDuplex { get; init; }

	/// <summary>Gets the default DMCOLOR_* value, or <see langword="null"/> if the DEVMODE doesn't set one.</summary>
	public short? DefaultColor { get; init; }
}
