namespace Latiprimere;

/// <summary>
/// A part of a printer's firmware and its version, as the printer reports them.
/// </summary>
public sealed record PrinterFirmware
{
	/// <summary>
	/// Gets the part's name, such as "Firmware" or "Network", as the printer gives it.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets the part's version, as the printer gives it, such as "2.4.1". Its form is the maker's, so compare it
	/// with the maker's own versions, not with another maker's.
	/// </summary>
	public required string Version { get; init; }
}
