namespace Latiprimere.Windows.Spooler;

/// <summary>
/// A paper type a printer driver lists (DC_MEDIATYPES and DC_MEDIATYPENAMES).
/// </summary>
internal sealed record WindowsMediaTypeRecord
{
	/// <summary>Gets the DMMEDIA_* number, the driver's own above 256.</summary>
	public required uint Id { get; init; }

	/// <summary>Gets the name the driver gives it.</summary>
	public required string Name { get; init; }
}
