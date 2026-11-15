namespace Latiprimere.Windows.Interop;

/// <summary>
/// Constants from the Windows print spooler API (winspool.h) and the Win32 error codes it returns.
/// </summary>
internal static class WinSpoolConstants
{
	/// <summary>The spooler library.</summary>
	internal const string LibraryName = "winspool.drv";

	/// <summary>EnumPrinters flag: printers installed on this machine.</summary>
	internal const uint PrinterEnumLocal = 0x00000002;

	/// <summary>EnumPrinters flag: printers this user has connected to on print servers.</summary>
	internal const uint PrinterEnumConnections = 0x00000004;

	/// <summary>Structure level for PRINTER_INFO_2.</summary>
	internal const uint PrinterInfoLevel2 = 2;

	/// <summary>Structure level for PRINTER_INFO_4: names only, read without contacting print servers.</summary>
	internal const uint PrinterInfoLevel4 = 4;

	/// <summary>Structure level for JOB_INFO_1.</summary>
	internal const uint JobInfoLevel1 = 1;

	/// <summary>Structure level for DOC_INFO_1.</summary>
	internal const uint DocInfoLevel1 = 1;

	/// <summary>SetJob command: delete (cancel) the job.</summary>
	internal const uint JobControlDelete = 5;

	/// <summary>Spooler data type for data sent to the printer unchanged.</summary>
	internal const string RawDataType = "RAW";

	/// <summary>Size in bytes of each chunk written to the spooler.</summary>
	internal const int WriteChunkSize = 64 * 1024;
}
