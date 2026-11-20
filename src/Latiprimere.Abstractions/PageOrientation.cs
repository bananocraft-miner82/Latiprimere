namespace Latiprimere;

/// <summary>
/// Enumerates the orientations in which pages can be printed.
/// </summary>
/// <remarks>
/// Later versions may add orientations, as IPP also has reverse portrait and reverse landscape. Treat one you don't
/// know as <see cref="PrinterDefault"/>, as print systems do.
/// </remarks>
public enum PageOrientation
{
	/// <summary>
	/// Use the printer's default setting, or the orientation in the document itself.
	/// </summary>
	PrinterDefault = 0,

	/// <summary>
	/// Print with the long edge of the page vertical.
	/// </summary>
	Portrait = 1,

	/// <summary>
	/// Print with the long edge of the page horizontal.
	/// </summary>
	Landscape = 2,
}
