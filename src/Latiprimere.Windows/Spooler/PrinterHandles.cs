using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Latiprimere.Windows.Interop;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// Opens spooler printer handles.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class PrinterHandles
{
	/// <summary>
	/// Opens a printer handle with the default (use) access rights.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>The handle, or <see langword="null"/> if no printer has that name.</returns>
	/// <exception cref="PrintException">The spooler failed for another reason.</exception>
	internal static SafePrinterHandle? TryOpen(string printerName)
	{
		if (WinSpoolNative.OpenPrinter(printerName, out SafePrinterHandle printerHandle, IntPtr.Zero))
		{
			return printerHandle;
		}

		int error = Marshal.GetLastPInvokeError();
		printerHandle.Dispose();

		if (error == Win32ErrorCodes.InvalidPrinterName)
		{
			return null;
		}

		throw SpoolerErrors.Create(nameof(WinSpoolNative.OpenPrinter), printerName, error);
	}

	/// <summary>
	/// Opens a printer handle, treating an unknown printer as an error.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>The handle.</returns>
	/// <exception cref="PrintException">The printer doesn't exist or the spooler failed.</exception>
	internal static SafePrinterHandle Open(string printerName)
	{
		return PrinterHandles.TryOpen(printerName)
				?? throw SpoolerErrors.Create(nameof(WinSpoolNative.OpenPrinter), printerName, Win32ErrorCodes.InvalidPrinterName);
	}
}
