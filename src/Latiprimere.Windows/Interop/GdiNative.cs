using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Source-generated declarations for the gdi32.dll functions the GDI print path uses.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class GdiNative
{
	/// <summary>CreateDCW: creates a device context for a printer.</summary>
	/// <param name="driver">"WINSPOOL" for printers.</param>
	/// <param name="device">The printer name.</param>
	/// <param name="output">Must be null.</param>
	/// <param name="initData">The DEVMODE to use, or null for the printer's defaults.</param>
	/// <returns>The device context, invalid on failure.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "CreateDCW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	internal static partial SafeDeviceContextHandle CreateDC(string driver, string device, IntPtr output, DevMode* initData);

	/// <summary>DeleteDC.</summary>
	/// <param name="deviceContext">The device context.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "DeleteDC")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool DeleteDC(IntPtr deviceContext);

	/// <summary>GetDeviceCaps.</summary>
	/// <param name="deviceContext">The device context.</param>
	/// <param name="index">The capability to read.</param>
	/// <returns>The value.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "GetDeviceCaps")]
	internal static partial int GetDeviceCaps(SafeDeviceContextHandle deviceContext, int index);

	/// <summary>StartDocW: starts a print job.</summary>
	/// <param name="deviceContext">The device context.</param>
	/// <param name="docInfo">The document information.</param>
	/// <returns>The job ID, or zero or less on failure.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "StartDocW", SetLastError = true)]
	internal static partial int StartDoc(SafeDeviceContextHandle deviceContext, GdiDocInfo* docInfo);

	/// <summary>StartPage.</summary>
	/// <param name="deviceContext">The device context.</param>
	/// <returns>Greater than zero on success.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "StartPage", SetLastError = true)]
	internal static partial int StartPage(SafeDeviceContextHandle deviceContext);

	/// <summary>EndPage.</summary>
	/// <param name="deviceContext">The device context.</param>
	/// <returns>Greater than zero on success.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "EndPage", SetLastError = true)]
	internal static partial int EndPage(SafeDeviceContextHandle deviceContext);

	/// <summary>EndDoc: ends the job, releasing it to print.</summary>
	/// <param name="deviceContext">The device context.</param>
	/// <returns>Greater than zero on success.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "EndDoc", SetLastError = true)]
	internal static partial int EndDoc(SafeDeviceContextHandle deviceContext);

	/// <summary>AbortDoc: abandons the job.</summary>
	/// <param name="deviceContext">The device context.</param>
	/// <returns>Greater than zero on success.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "AbortDoc")]
	internal static partial int AbortDoc(SafeDeviceContextHandle deviceContext);

	/// <summary>StretchDIBits: draws device-independent bitmap rows onto the device.</summary>
	/// <param name="deviceContext">The device context.</param>
	/// <param name="destinationX">The destination left edge.</param>
	/// <param name="destinationY">The destination top edge.</param>
	/// <param name="destinationWidth">The destination width.</param>
	/// <param name="destinationHeight">The destination height.</param>
	/// <param name="sourceX">The source left edge.</param>
	/// <param name="sourceY">The source top edge.</param>
	/// <param name="sourceWidth">The source width.</param>
	/// <param name="sourceHeight">The source height.</param>
	/// <param name="bits">The pixel data.</param>
	/// <param name="bitmapInfo">The bitmap header.</param>
	/// <param name="usage">DIB_RGB_COLORS.</param>
	/// <param name="rasterOperation">SRCCOPY.</param>
	/// <returns>The number of rows drawn, or zero or GDI_ERROR on failure.</returns>
	[LibraryImport(GdiConstants.LibraryName, EntryPoint = "StretchDIBits", SetLastError = true)]
	internal static partial int StretchDIBits
	(
		SafeDeviceContextHandle deviceContext,
		int                     destinationX,
		int                     destinationY,
		int                     destinationWidth,
		int                     destinationHeight,
		int                     sourceX,
		int                     sourceY,
		int                     sourceWidth,
		int                     sourceHeight,
		byte*                   bits,
		BitmapInfoHeader*       bitmapInfo,
		uint                    usage,
		uint                    rasterOperation
	);
}
