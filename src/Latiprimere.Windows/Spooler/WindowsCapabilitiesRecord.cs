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

	/// <summary>Gets the paper types, as DMMEDIA_* numbers and names.</summary>
	public IReadOnlyList<WindowsMediaTypeRecord> MediaTypes { get; init; } = Array.Empty<WindowsMediaTypeRecord>();

	/// <summary>Gets the smallest custom paper, width and length in tenths of a millimetre, or <see langword="null"/>
	/// if the driver takes no custom size.</summary>
	public (short Width, short Length)? MinExtent { get; init; }

	/// <summary>Gets the largest custom paper, as <see cref="MinExtent"/>.</summary>
	public (short Width, short Length)? MaxExtent { get; init; }

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

	/// <summary>Gets the driver's Print Capabilities document, as UTF-8 XML, or <see langword="null"/> if the driver
	/// has no Print Ticket support.</summary>
	public byte[]? PrintCapabilities { get; init; }

	/// <summary>Gets the driver's default Print Ticket, as UTF-8 XML, or <see langword="null"/>.</summary>
	public byte[]? DefaultPrintTicket { get; init; }

	/// <summary>Gets why the Print Capabilities couldn't be read, such as a driver without Print Ticket support; or
	/// <see langword="null"/>.</summary>
	public Exception? PrintSchemaFailure { get; init; }
}
