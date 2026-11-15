using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Resources;

namespace Latiprimere.Windows.Mapping;

/// <summary>
/// Turns the Win32 error of a failed spooler or GDI call into the exception Latiprimere's contracts promise, with a
/// message that says what went wrong and, where Windows' own text doesn't, what to do about it.
/// </summary>
/// <remarks>
/// <para>
/// Most errors become a <see cref="PrintException"/> with the Win32 error as its
/// <see cref="PrintException.NativeErrorCode"/>. Some get a more specific type:
/// </para>
/// <list type="bullet">
/// <item>the spooler service not running: <see cref="PrintSystemUnavailableException"/>, so the catalog treats the
/// whole print system as unavailable;</item>
/// <item>a printer removed from Windows: <see cref="PrinterNotFoundException"/>;</item>
/// <item>a driver that refuses raw data: <see cref="NotSupportedException"/>, as <see cref="IPrinter.SubmitAsync"/>
/// promises for a format the printer can't accept.</item>
/// </list>
/// <para>
/// Pure, with no platform calls, so it can be tested on any operating system.
/// </para>
/// </remarks>
internal static class WindowsErrorMapper
{
	/// <summary>
	/// Creates the exception for a failed call.
	/// </summary>
	/// <param name="win32Error">The Win32 error code; 0 if the call failed without setting one.</param>
	/// <param name="functionName">The Win32 function that failed.</param>
	/// <param name="printerName">The printer it was called for, or empty for calls about no one printer.</param>
	/// <param name="windowsDescription">Windows' own description of the error.</param>
	/// <returns>The exception, ready to throw.</returns>
	internal static Exception ToException(int win32Error, string functionName, string printerName, string windowsDescription)
	{
		string message = WindowsErrorMapper.CreateMessage(win32Error, functionName, printerName, windowsDescription);

		if (WindowsErrorMapper.IsSpoolerUnreachable(win32Error) && !WindowsErrorMapper.IsShared(printerName))
		{
			return new PrintSystemUnavailableException(message) { NativeErrorCode = win32Error };
		}

		return win32Error switch
		{
			Win32ErrorCodes.InvalidPrinterName or Win32ErrorCodes.PrinterDeleted or Win32ErrorCodes.PrinterNotFound
				=> new PrinterNotFoundException(message)
				{
					PrinterId       = printerName.Length == 0 ? null : WindowsPrintSystem.PrinterIdPrefix + printerName,
					NativeErrorCode = win32Error
				},
			Win32ErrorCodes.InvalidDataType => new NotSupportedException(message),
			_                                => new PrintException(message) { NativeErrorCode = win32Error },
		};
	}

	/// <summary>
	/// Builds the message: an explanation where one helps, then the failed call and Windows' description.
	/// </summary>
	/// <param name="win32Error">The Win32 error code.</param>
	/// <param name="functionName">The Win32 function that failed.</param>
	/// <param name="printerName">The printer it was called for, or empty.</param>
	/// <param name="windowsDescription">Windows' own description of the error.</param>
	/// <returns>The message.</returns>
	private static string CreateMessage(int win32Error, string functionName, string printerName, string windowsDescription)
	{
		// Windows describes error 0 as "The operation completed successfully", which is wrong for a failed call
		string description = win32Error == Win32ErrorCodes.Success ? ExceptionMessages.NoErrorReported : windowsDescription;

		string call = printerName.Length == 0
			? ExceptionMessages.FormatSpoolerCallFailedWithoutPrinter(functionName, description)
			: ExceptionMessages.FormatSpoolerCallFailed(functionName, printerName, description);

		string? explanation = WindowsErrorMapper.Explain(win32Error, printerName);

		return explanation is null ? call : ExceptionMessages.FormatExplainedSpoolerError(explanation, call);
	}

	/// <summary>
	/// Gets an explanation for errors whose Windows description doesn't say what happened or what to do.
	/// </summary>
	/// <param name="win32Error">The Win32 error code.</param>
	/// <param name="printerName">The printer the call was for, or empty.</param>
	/// <returns>The explanation, or <see langword="null"/> when Windows' description is enough.</returns>
	private static string? Explain(int win32Error, string printerName)
	{
		if (WindowsErrorMapper.IsSpoolerUnreachable(win32Error))
		{
			return WindowsErrorMapper.IsShared(printerName)
				? ExceptionMessages.PrintServerUnreachable
				: ExceptionMessages.SpoolerNotRunning;
		}

		return win32Error switch
		{
			Win32ErrorCodes.AccessDenied         => ExceptionMessages.AccessDenied,
			Win32ErrorCodes.InvalidPrinterName   => ExceptionMessages.PrinterRemoved,
			Win32ErrorCodes.PrinterDeleted       => ExceptionMessages.PrinterRemoved,
			Win32ErrorCodes.PrinterNotFound      => ExceptionMessages.PrinterRemoved,
			Win32ErrorCodes.InvalidDataType      => ExceptionMessages.DriverRefusesRawData,
			Win32ErrorCodes.UnknownPrinterDriver => ExceptionMessages.DriverNotInstalled,
			Win32ErrorCodes.PrintCancelled       => ExceptionMessages.JobDeletedWhileSending,
			Win32ErrorCodes.Cancelled            => ExceptionMessages.CancelledByDriverDialog,
			Win32ErrorCodes.FileNotFound         => ExceptionMessages.PathNotFound,
			Win32ErrorCodes.PathNotFound         => ExceptionMessages.PathNotFound,
			Win32ErrorCodes.DiskFull             => ExceptionMessages.DiskFull,
			Win32ErrorCodes.HandleDiskFull       => ExceptionMessages.DiskFull,
			_                                    => null,
		};
	}

	/// <summary>
	/// Decides whether an error means the RPC call to a spooler didn't get through.
	/// </summary>
	/// <param name="win32Error">The Win32 error code.</param>
	/// <returns>Whether the spooler, local or remote, couldn't be reached.</returns>
	private static bool IsSpoolerUnreachable(int win32Error)
	{
		return win32Error is Win32ErrorCodes.RpcServerUnavailable
			or Win32ErrorCodes.RpcCallFailed
			or Win32ErrorCodes.RpcCallFailedDidNotExecute;
	}

	/// <summary>
	/// Decides whether a printer is a connection to one shared by a print server, such as
	/// <c>\\server\Office</c>, whose spooler is the server's rather than this computer's.
	/// </summary>
	/// <param name="printerName">The printer name.</param>
	/// <returns>Whether the printer is shared from another computer.</returns>
	private static bool IsShared(string printerName)
	{
		return printerName.StartsWith(@"\\", StringComparison.Ordinal);
	}
}
