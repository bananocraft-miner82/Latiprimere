namespace Latiprimere.Windows.Spooler;

/// <summary>
/// The defaults read from a DEVMODE.
/// </summary>
internal sealed record DefaultSettings
{
	/// <summary>Gets the DMPAPER_* identifier, if set.</summary>
	public short? PaperId { get; init; }

	/// <summary>Gets the DMDUP_* value, if set.</summary>
	public short? Duplex { get; init; }

	/// <summary>Gets the DMCOLOR_* value, if set.</summary>
	public short? Color { get; init; }
}
