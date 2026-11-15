namespace Latiprimere.Windows.Interop;

/// <summary>
/// Constants from wingdi.h used by the GDI print path.
/// </summary>
internal static class GdiConstants
{
	/// <summary>The GDI library.</summary>
	internal const string LibraryName = "gdi32.dll";

	/// <summary>The driver name CreateDC takes for printers.</summary>
	internal const string PrinterDriverName = "WINSPOOL";

	/// <summary>GetDeviceCaps: printable width in device pixels.</summary>
	internal const int HorizontalResolution = 8;

	/// <summary>GetDeviceCaps: printable height in device pixels.</summary>
	internal const int VerticalResolution = 10;

	/// <summary>GetDeviceCaps: horizontal dots per inch.</summary>
	internal const int LogicalPixelsX = 88;

	/// <summary>GetDeviceCaps: vertical dots per inch.</summary>
	internal const int LogicalPixelsY = 90;

	/// <summary>GetDeviceCaps: paper width in device pixels.</summary>
	internal const int PhysicalWidth = 110;

	/// <summary>GetDeviceCaps: paper height in device pixels.</summary>
	internal const int PhysicalHeight = 111;

	/// <summary>GetDeviceCaps: distance from the paper's left edge to the printable area.</summary>
	internal const int PhysicalOffsetX = 112;

	/// <summary>GetDeviceCaps: distance from the paper's top edge to the printable area.</summary>
	internal const int PhysicalOffsetY = 113;

	/// <summary>BI_RGB: uncompressed bitmap.</summary>
	internal const uint BitmapUncompressed = 0;

	/// <summary>DIB_RGB_COLORS.</summary>
	internal const uint DibRgbColors = 0;

	/// <summary>SRCCOPY raster operation.</summary>
	internal const uint SourceCopy = 0x00CC0020;

	/// <summary>Bits per pixel of the rendered bands.</summary>
	internal const ushort BitsPerPixel = 32;

	/// <summary>Win32 error: the operation was cancelled by the user (for example, a Save As dialog).</summary>
	internal const int ErrorCancelled = 1223;
}
