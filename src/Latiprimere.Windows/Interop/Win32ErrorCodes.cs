namespace Latiprimere.Windows.Interop;

/// <summary>
/// The Win32 error codes the spooler and GDI calls report that Latiprimere handles or explains.
/// </summary>
/// <remarks>
/// Values from winerror.h. Plain constants, with no platform dependency, so the error mapping can be tested
/// anywhere.
/// </remarks>
internal static class Win32ErrorCodes
{
	/// <summary>No error was reported, although the call failed.</summary>
	internal const int Success = 0;

	/// <summary>ERROR_FILE_NOT_FOUND. Also what GetDefaultPrinter reports when no default is set.</summary>
	internal const int FileNotFound = 2;

	/// <summary>ERROR_PATH_NOT_FOUND, for example an output file in a folder that doesn't exist.</summary>
	internal const int PathNotFound = 3;

	/// <summary>ERROR_ACCESS_DENIED.</summary>
	internal const int AccessDenied = 5;

	/// <summary>ERROR_HANDLE_DISK_FULL.</summary>
	internal const int HandleDiskFull = 39;

	/// <summary>ERROR_PRINT_CANCELLED: the job was deleted while it was being sent.</summary>
	internal const int PrintCancelled = 63;

	/// <summary>ERROR_INVALID_PARAMETER. Also what GetJob and SetJob report for a job that has left the queue.</summary>
	internal const int InvalidParameter = 87;

	/// <summary>ERROR_DISK_FULL.</summary>
	internal const int DiskFull = 112;

	/// <summary>ERROR_INSUFFICIENT_BUFFER: the buffer passed was too small.</summary>
	internal const int InsufficientBuffer = 122;

	/// <summary>ERROR_CANCELLED, for example when a driver's Save dialog is closed.</summary>
	internal const int Cancelled = 1223;

	/// <summary>RPC_S_SERVER_UNAVAILABLE: the spooler service, or a remote print server, can't be reached.</summary>
	internal const int RpcServerUnavailable = 1722;

	/// <summary>RPC_S_CALL_FAILED: the call to the spooler service failed.</summary>
	internal const int RpcCallFailed = 1726;

	/// <summary>RPC_S_CALL_FAILED_DNE: the call to the spooler service failed and did not execute.</summary>
	internal const int RpcCallFailedDidNotExecute = 1727;

	/// <summary>ERROR_UNKNOWN_PRINTER_DRIVER: the printer's driver isn't installed.</summary>
	internal const int UnknownPrinterDriver = 1797;

	/// <summary>ERROR_INVALID_PRINTER_NAME. OpenPrinter reports it for printers that don't exist.</summary>
	internal const int InvalidPrinterName = 1801;

	/// <summary>ERROR_INVALID_DATATYPE: the print processor doesn't accept the data type, such as RAW.</summary>
	internal const int InvalidDataType = 1804;

	/// <summary>ERROR_PRINTER_DELETED: the printer was deleted while in use.</summary>
	internal const int PrinterDeleted = 1905;

	/// <summary>ERROR_PRINTER_NOT_FOUND.</summary>
	internal const int PrinterNotFound = 3012;
}
