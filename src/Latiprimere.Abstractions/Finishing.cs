namespace Latiprimere;

/// <summary>
/// Something a printer does to the printed sheets: staples, holes or a fold.
/// </summary>
/// <remarks>
/// These are the finishings office printers commonly offer; IPP's others, such as binding or trimming, are set with
/// <see cref="PrintOptions.VendorOptions"/>. Values may be added in a later version: treat one you don't know as a
/// finishing the printer may not offer, and a print system refuses one it doesn't know with
/// <see cref="NotSupportedException"/>. Positions are as the sheet is read, in its orientation.
/// </remarks>
public enum Finishing
{
	/// <summary>A staple where the printer chooses.</summary>
	Staple = 1,

	/// <summary>A staple in the top left corner.</summary>
	StapleTopLeft = 2,

	/// <summary>A staple in the bottom left corner.</summary>
	StapleBottomLeft = 3,

	/// <summary>A staple in the top right corner.</summary>
	StapleTopRight = 4,

	/// <summary>A staple in the bottom right corner.</summary>
	StapleBottomRight = 5,

	/// <summary>Two staples along the left edge.</summary>
	StapleDualLeft = 6,

	/// <summary>Two staples along the top edge.</summary>
	StapleDualTop = 7,

	/// <summary>Holes punched where the printer chooses.</summary>
	Punch = 8,

	/// <summary>Two holes along the left edge.</summary>
	PunchDualLeft = 9,

	/// <summary>Two holes along the top edge.</summary>
	PunchDualTop = 10,

	/// <summary>A fold where the printer chooses.</summary>
	Fold = 11,
}
