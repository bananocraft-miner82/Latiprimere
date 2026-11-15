using System.ComponentModel;
using System.Runtime.InteropServices;
using Latiprimere.Windows.Mapping;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// Turns failed spooler and GDI calls into Latiprimere exceptions, through <see cref="WindowsErrorMapper"/>.
/// </summary>
internal static class SpoolerErrors
{
	/// <summary>
	/// Creates the exception for a failed call.
	/// </summary>
	/// <param name="functionName">The Win32 function that failed.</param>
	/// <param name="printerName">The printer it was called for, or empty for calls about no one printer.</param>
	/// <param name="error">The Win32 error, or -1 to read the last error of the calling thread.</param>
	/// <returns>
	/// A <see cref="PrintException"/>, or a subclass of it, with the Win32 error as its native error code; or a
	/// <see cref="NotSupportedException"/> for a driver that refuses the data type. See
	/// <see cref="WindowsErrorMapper"/>.
	/// </returns>
	internal static Exception Create(string functionName, string printerName, int error = -1)
	{
		int win32Error = error < 0 ? Marshal.GetLastPInvokeError() : error;
		string description = new Win32Exception(win32Error).Message;

		return WindowsErrorMapper.ToException(win32Error, functionName, printerName, description);
	}

	/// <summary>
	/// Throws the last error if a call failed.
	/// </summary>
	/// <param name="isSuccess">Whether the call succeeded.</param>
	/// <param name="functionName">The Win32 function, for the message.</param>
	/// <param name="printerName">The printer, for the message.</param>
	/// <exception cref="PrintException">The call failed.</exception>
	/// <exception cref="NotSupportedException">The printer's driver refuses the data type.</exception>
	internal static void ThrowIfFailed(bool isSuccess, string functionName, string printerName)
	{
		if (!isSuccess)
		{
			throw SpoolerErrors.Create(functionName, printerName);
		}
	}
}
