namespace Latiprimere.Windows.Interop;

/// <summary>
/// DeviceCapabilities queries (DC_* in wingdi.h) and the fixed sizes of the names they return.
/// </summary>
internal static class DeviceCapabilityConstants
{
	/// <summary>DC_PAPERS: the DMPAPER_* identifiers of the supported paper sizes (WORD each).</summary>
	internal const ushort Papers = 2;

	/// <summary>DC_PAPERSIZE: the paper sizes, in tenths of a millimetre (POINT each).</summary>
	internal const ushort PaperSize = 3;

	/// <summary>DC_BINS: the DMBIN_* identifiers of the paper sources (WORD each).</summary>
	internal const ushort Bins = 6;

	/// <summary>DC_DUPLEX: 1 if the printer can print on both sides.</summary>
	internal const ushort Duplex = 7;

	/// <summary>DC_BINNAMES: the paper source names (24 characters each).</summary>
	internal const ushort BinNames = 12;

	/// <summary>DC_PAPERNAMES: the paper size names (64 characters each).</summary>
	internal const ushort PaperNames = 16;

	/// <summary>DC_ORIENTATION: 90 or 270 if the printer can print landscape, 0 if not.</summary>
	internal const ushort Orientation = 17;

	/// <summary>DC_COPIES: the most copies the driver can make.</summary>
	internal const ushort Copies = 18;

	/// <summary>DC_COLORDEVICE: 1 if the printer can print in colour.</summary>
	internal const ushort ColorDevice = 32;

	/// <summary>The length of each name returned by DC_BINNAMES, in characters.</summary>
	internal const int BinNameLength = 24;

	/// <summary>The length of each name returned by DC_PAPERNAMES, in characters.</summary>
	internal const int PaperNameLength = 64;
}
