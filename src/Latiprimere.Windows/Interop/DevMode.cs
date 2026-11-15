using System.Runtime.InteropServices;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Mirrors the public part of <c>DEVMODEW</c> (220 bytes), using the printer branch of its unions. Drivers append
/// private data after it (<c>dmDriverExtra</c> bytes), so instances live at the start of a larger buffer.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DevMode
{
	/// <summary><c>dmDeviceName</c>.</summary>
	public fixed char DeviceName[32];

	/// <summary><c>dmSpecVersion</c>.</summary>
	public ushort SpecVersion;

	/// <summary><c>dmDriverVersion</c>.</summary>
	public ushort DriverVersion;

	/// <summary><c>dmSize</c>: the size of the public part.</summary>
	public ushort Size;

	/// <summary><c>dmDriverExtra</c>: the size of the driver's private data that follows.</summary>
	public ushort DriverExtra;

	/// <summary><c>dmFields</c>: which members are set (DM_* flags).</summary>
	public uint Fields;

	/// <summary><c>dmOrientation</c>: DMORIENT_*.</summary>
	public short Orientation;

	/// <summary><c>dmPaperSize</c>: DMPAPER_*.</summary>
	public short PaperSize;

	/// <summary><c>dmPaperLength</c> in tenths of a millimetre.</summary>
	public short PaperLength;

	/// <summary><c>dmPaperWidth</c> in tenths of a millimetre.</summary>
	public short PaperWidth;

	/// <summary><c>dmScale</c> in percent.</summary>
	public short Scale;

	/// <summary><c>dmCopies</c>.</summary>
	public short Copies;

	/// <summary><c>dmDefaultSource</c>: the paper tray.</summary>
	public short DefaultSource;

	/// <summary><c>dmPrintQuality</c>.</summary>
	public short PrintQuality;

	/// <summary><c>dmColor</c>: DMCOLOR_*.</summary>
	public short Color;

	/// <summary><c>dmDuplex</c>: DMDUP_*.</summary>
	public short Duplex;

	/// <summary><c>dmYResolution</c>.</summary>
	public short YResolution;

	/// <summary><c>dmTTOption</c>.</summary>
	public short TrueTypeOption;

	/// <summary><c>dmCollate</c>: DMCOLLATE_*.</summary>
	public short Collate;

	/// <summary><c>dmFormName</c>.</summary>
	public fixed char FormName[32];

	/// <summary><c>dmLogPixels</c>.</summary>
	public ushort LogPixels;

	/// <summary><c>dmBitsPerPel</c>.</summary>
	public uint BitsPerPixel;

	/// <summary><c>dmPelsWidth</c>.</summary>
	public uint PixelsWidth;

	/// <summary><c>dmPelsHeight</c>.</summary>
	public uint PixelsHeight;

	/// <summary><c>dmNup</c> (union with <c>dmDisplayFlags</c>).</summary>
	public uint Nup;

	/// <summary><c>dmDisplayFrequency</c>.</summary>
	public uint DisplayFrequency;

	/// <summary><c>dmICMMethod</c>.</summary>
	public uint IcmMethod;

	/// <summary><c>dmICMIntent</c>.</summary>
	public uint IcmIntent;

	/// <summary><c>dmMediaType</c>.</summary>
	public uint MediaType;

	/// <summary><c>dmDitherType</c>.</summary>
	public uint DitherType;

	/// <summary><c>dmReserved1</c>.</summary>
	public uint Reserved1;

	/// <summary><c>dmReserved2</c>.</summary>
	public uint Reserved2;

	/// <summary><c>dmPanningWidth</c>.</summary>
	public uint PanningWidth;

	/// <summary><c>dmPanningHeight</c>.</summary>
	public uint PanningHeight;
}
