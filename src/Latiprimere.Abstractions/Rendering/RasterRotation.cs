namespace Latiprimere.Rendering;

/// <summary>
/// How far to turn a page as it is rendered, clockwise, in quarter turns.
/// </summary>
/// <remarks>
/// A band's <see cref="RasterBand.PageWidth"/> and <see cref="RasterBand.PageHeight"/> are the turned page's: a
/// portrait page turned a quarter is rendered into a landscape area.
/// </remarks>
public enum RasterRotation
{
	/// <summary>Not turned.</summary>
	None = 0,

	/// <summary>Turned a quarter clockwise: the page's top goes to the right.</summary>
	Clockwise90 = 1,

	/// <summary>Turned half way: upside down.</summary>
	Half = 2,

	/// <summary>Turned a quarter anticlockwise: the page's top goes to the left, as for landscape printing.</summary>
	Anticlockwise90 = 3,
}
