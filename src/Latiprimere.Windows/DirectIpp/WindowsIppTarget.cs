namespace Latiprimere.Windows.DirectIpp;

/// <summary>
/// Where a Windows queue's PDF jobs can go over IPP.
/// </summary>
internal sealed record WindowsIppTarget
{
	/// <summary>Gets the printer's IPP URI.</summary>
	public required Uri PrinterUri { get; init; }

	/// <summary>
	/// Gets the UUID the queue's WSD port records, normalised, when the printer was found by it over mDNS; or
	/// <see langword="null"/> for a URI known exactly. The printer's own <c>printer-uuid</c> must match it before a
	/// job is sent (ADR 0036).
	/// </summary>
	public string? Uuid { get; init; }
}
