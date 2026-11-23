namespace Latiprimere.Windows.Interop;

/// <summary>
/// DEVMODE field flags and values (wingdi.h), and DocumentProperties modes (winspool.h).
/// </summary>
internal static class DevModeConstants
{
	/// <summary>DM_ORIENTATION.</summary>
	internal const uint FieldOrientation = 0x00000001;

	/// <summary>DM_PAPERSIZE.</summary>
	internal const uint FieldPaperSize = 0x00000002;

	/// <summary>DM_PAPERLENGTH: overrides the length of <c>dmPaperSize</c> when set.</summary>
	internal const uint FieldPaperLength = 0x00000004;

	/// <summary>DM_PAPERWIDTH: overrides the width of <c>dmPaperSize</c> when set.</summary>
	internal const uint FieldPaperWidth = 0x00000008;

	/// <summary>DM_FORMNAME: a form chosen by name, which some drivers prefer to <c>dmPaperSize</c>.</summary>
	internal const uint FieldFormName = 0x00010000;

	/// <summary>DM_DEFAULTSOURCE.</summary>
	internal const uint FieldDefaultSource = 0x00000200;

	/// <summary>DM_COLOR.</summary>
	internal const uint FieldColor = 0x00000800;

	/// <summary>DM_COPIES.</summary>
	internal const uint FieldCopies = 0x00000100;

	/// <summary>DM_DUPLEX.</summary>
	internal const uint FieldDuplex = 0x00001000;

	/// <summary>DM_COLLATE.</summary>
	internal const uint FieldCollate = 0x00008000;

	/// <summary>DM_PRINTQUALITY.</summary>
	internal const uint FieldPrintQuality = 0x00000400;

	/// <summary>DM_MEDIATYPE.</summary>
	internal const uint FieldMediaType = 0x02000000;

	/// <summary>DMPAPER_USER: a size given by dmPaperWidth and dmPaperLength.</summary>
	internal const short PaperUser = 256;

	/// <summary>DMCOLLATE_FALSE.</summary>
	internal const short CollateFalse = 0;

	/// <summary>DMRES_DRAFT.</summary>
	internal const short QualityDraft = -1;

	/// <summary>DMRES_MEDIUM, Windows' nearest to normal quality.</summary>
	internal const short QualityMedium = -3;

	/// <summary>DMRES_HIGH.</summary>
	internal const short QualityHigh = -4;

	/// <summary>DMORIENT_PORTRAIT.</summary>
	internal const short OrientationPortrait = 1;

	/// <summary>DMORIENT_LANDSCAPE.</summary>
	internal const short OrientationLandscape = 2;

	/// <summary>DMDUP_SIMPLEX: one-sided.</summary>
	internal const short DuplexSimplex = 1;

	/// <summary>DMDUP_VERTICAL: two-sided, flipped on the long edge in portrait.</summary>
	internal const short DuplexVertical = 2;

	/// <summary>DMDUP_HORIZONTAL: two-sided, flipped on the short edge in portrait.</summary>
	internal const short DuplexHorizontal = 3;

	/// <summary>DMCOLLATE_TRUE.</summary>
	internal const short CollateTrue = 1;

	/// <summary>DMCOLOR_MONOCHROME.</summary>
	internal const short ColorMonochrome = 1;

	/// <summary>DMCOLOR_COLOR.</summary>
	internal const short ColorColor = 2;

	/// <summary>DocumentProperties mode: write the result to the output buffer (DM_OUT_BUFFER).</summary>
	internal const uint ModeOutBuffer = 2;

	/// <summary>DocumentProperties mode: merge the input buffer's settings (DM_IN_BUFFER).</summary>
	internal const uint ModeInBuffer = 8;
}
