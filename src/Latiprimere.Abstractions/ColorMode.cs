namespace Latiprimere;

/// <summary>
/// Enumerates the ways a printer can render colour.
/// </summary>
/// <remarks>
/// Later versions may add modes, as IPP has more, such as bi-level. Treat one you don't know as
/// <see cref="PrinterDefault"/>, as print systems do.
/// </remarks>
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
