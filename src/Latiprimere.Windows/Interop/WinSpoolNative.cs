using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Source-generated declarations for the winspool.drv functions Latiprimere uses.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class WinSpoolNative
{
	/// <summary>EnumPrintersW: lists printers into a caller-supplied buffer.</summary>
	/// <param name="flags">PRINTER_ENUM_* flags.</param>
	/// <param name="name">The server name, or null for this machine.</param>
	/// <param name="level">The structure level.</param>
	/// <param name="printerEnum">The buffer, or null to ask for the size needed.</param>
	/// <param name="bufferSize">The buffer size in bytes.</param>
	/// <param name="bytesNeeded">The size needed.</param>
	/// <param name="returnedCount">The number of structures written.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "EnumPrintersW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool EnumPrinters
	(
		uint     flags,
		IntPtr   name,
		uint     level,
		byte*    printerEnum,
		uint     bufferSize,
		out uint bytesNeeded,
		out uint returnedCount
	);

	/// <summary>OpenPrinterW: opens a handle to a printer.</summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="printerHandle">The opened handle.</param>
	/// <param name="printerDefaults">PRINTER_DEFAULTS, or null for the defaults.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport
	(
		WinSpoolConstants.LibraryName,
		EntryPoint        = "OpenPrinterW",
		SetLastError      = true,
		StringMarshalling = StringMarshalling.Utf16
	)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool OpenPrinter
	(
		string                printerName,
		out SafePrinterHandle printerHandle,
		IntPtr                printerDefaults
	);

	/// <summary>ClosePrinter: closes a printer handle.</summary>
	/// <param name="printerHandle">The handle.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "ClosePrinter", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool ClosePrinter(IntPtr printerHandle);

	/// <summary>GetPrinterW: reads one printer's information into a caller-supplied buffer.</summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <param name="level">The structure level.</param>
	/// <param name="printer">The buffer, or null to ask for the size needed.</param>
	/// <param name="bufferSize">The buffer size in bytes.</param>
	/// <param name="bytesNeeded">The size needed.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "GetPrinterW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool GetPrinter
	(
		SafePrinterHandle printerHandle,
		uint              level,
		byte*             printer,
		uint              bufferSize,
		out uint          bytesNeeded
	);

	/// <summary>GetDefaultPrinterW: reads the current user's default printer name.</summary>
	/// <param name="buffer">The buffer, or null to ask for the size needed.</param>
	/// <param name="bufferSize">The buffer size in characters; on return, the size needed.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "GetDefaultPrinterW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool GetDefaultPrinter(char* buffer, ref uint bufferSize);

	/// <summary>StartDocPrinterW: starts a print job.</summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <param name="level">The structure level (1).</param>
	/// <param name="docInfo">The document information.</param>
	/// <returns>The job ID, or 0 on failure.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "StartDocPrinterW", SetLastError = true)]
	internal static partial uint StartDocPrinter(SafePrinterHandle printerHandle, uint level, DocInfo1* docInfo);

	/// <summary>StartPagePrinter: starts a page.</summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "StartPagePrinter", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool StartPagePrinter(SafePrinterHandle printerHandle);

	/// <summary>WritePrinter: sends data to the spooler for the current job.</summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <param name="buffer">The data.</param>
	/// <param name="count">The number of bytes to write.</param>
	/// <param name="written">The number of bytes written.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "WritePrinter", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool WritePrinter
	(
		SafePrinterHandle printerHandle,
		byte* buffer,
		uint count,
		out uint written
	);

	/// <summary>EndPagePrinter: ends a page.</summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "EndPagePrinter", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool EndPagePrinter(SafePrinterHandle printerHandle);

	/// <summary>EndDocPrinter: ends the job, releasing it to print.</summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "EndDocPrinter", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool EndDocPrinter(SafePrinterHandle printerHandle);

	/// <summary>GetJobW: reads a job's information into a caller-supplied buffer.</summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <param name="jobId">The job ID.</param>
	/// <param name="level">The structure level.</param>
	/// <param name="job">The buffer, or null to ask for the size needed.</param>
	/// <param name="bufferSize">The buffer size in bytes.</param>
	/// <param name="bytesNeeded">The size needed.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "GetJobW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool GetJob
	(
		SafePrinterHandle printerHandle,
		uint              jobId,
		uint              level,
		byte*             job,
		uint              bufferSize,
		out uint          bytesNeeded
	);

	/// <summary>SetJobW: controls a job, for example to delete it.</summary>
	/// <param name="printerHandle">The printer handle.</param>
	/// <param name="jobId">The job ID.</param>
	/// <param name="level">The structure level, or 0 when only sending a command.</param>
	/// <param name="job">The job information, or null when only sending a command.</param>
	/// <param name="command">The JOB_CONTROL_* command.</param>
	/// <returns><see langword="true"/> on success.</returns>
	[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "SetJobW", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool SetJob
	(
		SafePrinterHandle printerHandle,
		uint              jobId,
		uint              level,
		IntPtr            job,
		uint              command
	);

	/// <summary>DocumentPropertiesW: reads or adjusts a printer's DEVMODE.</summary>
	/// <param name="window">A parent window for any dialog; zero for none.</param>
	/// <param name="printerHandle">The printer handle.</param>
	/// <param name="deviceName">The printer name.</param>
	/// <param name="output">Receives the DEVMODE, or null when asking for the size.</param>
	/// <param name="input">The DEVMODE to merge, or null.</param>
	/// <param name="mode">DM_* mode flags, or 0 to ask for the size.</param>
	/// <returns>The size needed when <paramref name="mode"/> is 0; otherwise IDOK (1) on success, negative on
	/// failure.</returns>
	[LibraryImport
	(
		WinSpoolConstants.LibraryName,
		EntryPoint        = "DocumentPropertiesW",
		SetLastError      = true,
		StringMarshalling = StringMarshalling.Utf16
	)]
	internal static partial int DocumentProperties
	(
		IntPtr            window,
		SafePrinterHandle printerHandle,
		string            deviceName,
		DevMode*          output,
		DevMode*          input,
		uint              mode
	);

	/// <summary>DeviceCapabilitiesW: reads one of a printer driver's capabilities.</summary>
	/// <param name="device">The printer name.</param>
	/// <param name="port">The printer's port, or null.</param>
	/// <param name="capability">The DC_* capability to read.</param>
	/// <param name="output">Receives an array for list capabilities, or null to ask for the element count.</param>
	/// <param name="devMode">The DEVMODE to read capabilities for, or null for the driver's defaults.</param>
	/// <returns>The value or element count; -1 if the capability isn't supported or the call failed.</returns>
	[LibraryImport
	(
		WinSpoolConstants.LibraryName,
		EntryPoint        = "DeviceCapabilitiesW",
		SetLastError      = true,
		StringMarshalling = StringMarshalling.Utf16
	)]
	internal static partial int DeviceCapabilities
	(
		string   device,
		string?  port,
		ushort   capability,
		void*    output,
		DevMode* devMode
	);
}
