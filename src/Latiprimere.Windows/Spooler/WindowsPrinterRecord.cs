using Latiprimere.Windows.Interop;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// A printer as reported by the Windows spooler, copied out of the native buffer.
/// </summary>
internal sealed record WindowsPrinterRecord
{
	/// <summary>Gets the printer's name, as used to open it.</summary>
	public required string Name { get; init; }

	/// <summary>Gets the printer's description (<c>pComment</c>), if any.</summary>
	public string? Comment { get; init; }

	/// <summary>Gets the printer's location, if any.</summary>
	public string? Location { get; init; }

	/// <summary>Gets the printer driver's name, if known.</summary>
	public string? DriverName { get; init; }

	/// <summary>Gets the port the printer uses, if known.</summary>
	public string? PortName { get; init; }

	/// <summary>Gets the printer's status flags.</summary>
	public PrinterStatusFlags Status { get; init; }

	/// <summary>Gets a value indicating whether the user has set the printer to work offline.</summary>
	public bool IsWorkingOffline { get; init; }
}
