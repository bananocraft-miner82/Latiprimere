namespace Latiprimere;

/// <summary>
/// Enumerates the ways a printer can render colour.
/// </summary>
public enum ColorMode
{
	/// <summary>
	/// Use the printer's default setting.
	/// </summary>
	PrinterDefault = 0,

	/// <summary>
	/// Print in shades of grey (or black only).
	/// </summary>
	Monochrome = 1,

	/// <summary>
	/// Print in colour.
	/// </summary>
	Color = 2,
}
