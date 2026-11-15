namespace Latiprimere;

/// <summary>
/// Enumerates the ways pages can be printed on one or both sides of the paper.
/// </summary>
public enum DuplexMode
{
	/// <summary>
	/// Use the printer's default setting.
	/// </summary>
	PrinterDefault = 0,

	/// <summary>
	/// Print on one side of the paper only.
	/// </summary>
	OneSided = 1,

	/// <summary>
	/// Print on both sides, turning the page on its long edge (like a book in portrait).
	/// </summary>
	LongEdge = 2,

	/// <summary>
	/// Print on both sides, turning the page on its short edge (like a notepad in portrait).
	/// </summary>
	ShortEdge = 3,
}
