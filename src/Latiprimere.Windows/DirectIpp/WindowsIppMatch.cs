using Latiprimere.Ipp;

namespace Latiprimere.Windows.DirectIpp;

/// <summary>
/// A queue's IPP printer, once known to take its PDF jobs, with the UUID it was found by. Kept as one object, so a
/// printer and its UUID are always replaced or forgotten together, even with several jobs at once.
/// </summary>
internal sealed record WindowsIppMatch
{
	/// <summary>Gets the printer, reached over IPP, whose job IDs start <c>ipp:</c>.</summary>
	public required IppPrinter Printer { get; init; }

	/// <summary>Gets the UUID a WSD queue's printer was found by, or <see langword="null"/> for a known URI.</summary>
	public string? Uuid { get; init; }
}
