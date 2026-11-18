using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Source-generated declarations for the Print Ticket API (prntvpt.dll) and the COM functions it needs. Every
/// function returns an HRESULT.
/// </summary>
/// <remarks>
/// Streams are passed as raw <c>IStream</c> pointers (<see cref="SafeComStreamHandle"/>), not as COM interfaces, so
/// no COM marshalling is needed, which keeps the code trimmable and Native AOT compatible.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static unsafe partial class PrintTicketNative
{
	/// <summary>PTOpenProvider: opens the printer's Print Ticket provider.</summary>
	/// <param name="printerName">The printer name.</param>
	/// <param name="version">The Print Schema version, 1.</param>
	/// <param name="provider">The provider, on success.</param>
	/// <returns>The HRESULT.</returns>
	[LibraryImport(PrintTicketConstants.LibraryName, EntryPoint = "PTOpenProvider", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int PTOpenProvider(string printerName, uint version, out SafePrintTicketProviderHandle provider);

	/// <summary>PTCloseProvider.</summary>
	/// <param name="provider">The provider.</param>
	/// <returns>The HRESULT.</returns>
	[LibraryImport(PrintTicketConstants.LibraryName, EntryPoint = "PTCloseProvider")]
	internal static partial int PTCloseProvider(IntPtr provider);

	/// <summary>PTConvertDevModeToPrintTicket: writes a DEVMODE's settings as a Print Ticket.</summary>
	/// <param name="provider">The provider.</param>
	/// <param name="devModeSize">The DEVMODE's size, with the driver's data.</param>
	/// <param name="devMode">The DEVMODE.</param>
	/// <param name="scope">Which settings: <see cref="PrintTicketConstants.JobScope"/>.</param>
	/// <param name="printTicket">The stream to write the ticket to.</param>
	/// <returns>The HRESULT.</returns>
	[LibraryImport(PrintTicketConstants.LibraryName, EntryPoint = "PTConvertDevModeToPrintTicket")]
	internal static partial int PTConvertDevModeToPrintTicket
	(
		SafePrintTicketProviderHandle provider,
		uint                          devModeSize,
		DevMode*                      devMode,
		int                           scope,
		SafeComStreamHandle           printTicket
	);

	/// <summary>PTMergeAndValidatePrintTicket: merges a ticket onto a base ticket, and lets the driver resolve
	/// conflicts.</summary>
	/// <param name="provider">The provider.</param>
	/// <param name="baseTicket">The base ticket, positioned at its start.</param>
	/// <param name="deltaTicket">The ticket to merge in, positioned at its start.</param>
	/// <param name="scope">Which settings: <see cref="PrintTicketConstants.JobScope"/>.</param>
	/// <param name="resultTicket">The stream to write the merged ticket to.</param>
	/// <param name="errorMessage">A BSTR describing a failure, or null; free it with <see cref="SysFreeString"/>.</param>
	/// <returns>The HRESULT: <see cref="PrintTicketConstants.NoConflict"/> or
	/// <see cref="PrintTicketConstants.ConflictResolved"/> on success.</returns>
	[LibraryImport(PrintTicketConstants.LibraryName, EntryPoint = "PTMergeAndValidatePrintTicket")]
	internal static partial int PTMergeAndValidatePrintTicket
	(
		SafePrintTicketProviderHandle provider,
		SafeComStreamHandle           baseTicket,
		SafeComStreamHandle           deltaTicket,
		int                           scope,
		SafeComStreamHandle           resultTicket,
		IntPtr*                       errorMessage
	);

	/// <summary>PTConvertPrintTicketToDevMode: turns a Print Ticket into a DEVMODE for the printer's driver.</summary>
	/// <param name="provider">The provider.</param>
	/// <param name="printTicket">The ticket, positioned at its start.</param>
	/// <param name="baseDevModeType">Which defaults fill what the ticket leaves out.</param>
	/// <param name="scope">Which settings: <see cref="PrintTicketConstants.JobScope"/>.</param>
	/// <param name="devModeSize">The DEVMODE's size, with the driver's data.</param>
	/// <param name="devMode">The DEVMODE; free it with <see cref="PTReleaseMemory"/>.</param>
	/// <param name="errorMessage">A BSTR describing a failure, or null; free it with <see cref="SysFreeString"/>.</param>
	/// <returns>The HRESULT.</returns>
	[LibraryImport(PrintTicketConstants.LibraryName, EntryPoint = "PTConvertPrintTicketToDevMode")]
	internal static partial int PTConvertPrintTicketToDevMode
	(
		SafePrintTicketProviderHandle provider,
		SafeComStreamHandle           printTicket,
		int                           baseDevModeType,
		int                           scope,
		uint*                         devModeSize,
		DevMode**                     devMode,
		IntPtr*                       errorMessage
	);

	/// <summary>PTGetPrintCapabilities: writes the printer's Print Capabilities, for the settings a ticket
	/// chooses.</summary>
	/// <param name="provider">The provider.</param>
	/// <param name="printTicket">A full ticket, positioned at its start, whose choices constrain the result.</param>
	/// <param name="capabilities">The stream to write the Print Capabilities document to.</param>
	/// <param name="errorMessage">A BSTR describing a failure, or null; free it with <see cref="SysFreeString"/>.</param>
	/// <returns>The HRESULT.</returns>
	[LibraryImport(PrintTicketConstants.LibraryName, EntryPoint = "PTGetPrintCapabilities")]
	internal static partial int PTGetPrintCapabilities
	(
		SafePrintTicketProviderHandle provider,
		SafeComStreamHandle           printTicket,
		SafeComStreamHandle           capabilities,
		IntPtr*                       errorMessage
	);

	/// <summary>PTReleaseMemory: frees memory the Print Ticket API allocated.</summary>
	/// <param name="buffer">The memory.</param>
	/// <returns>The HRESULT.</returns>
	[LibraryImport(PrintTicketConstants.LibraryName, EntryPoint = "PTReleaseMemory")]
	internal static partial int PTReleaseMemory(void* buffer);

	/// <summary>CreateStreamOnHGlobal: creates an empty memory stream, freed when the stream is released.</summary>
	/// <param name="global">Null, for a new block of memory.</param>
	/// <param name="deleteOnRelease">Whether releasing the stream frees the memory: always true here.</param>
	/// <param name="stream">The stream.</param>
	/// <returns>The HRESULT.</returns>
	[LibraryImport(PrintTicketConstants.ComLibraryName, EntryPoint = "CreateStreamOnHGlobal")]
	internal static partial int CreateStreamOnHGlobal
	(
		IntPtr                                    global,
		[MarshalAs(UnmanagedType.Bool)] bool      deleteOnRelease,
		out SafeComStreamHandle                   stream
	);

	/// <summary>CoInitializeEx.</summary>
	/// <param name="reserved">Null.</param>
	/// <param name="coInit">The apartment: <see cref="PrintTicketConstants.MultiThreaded"/>.</param>
	/// <returns>S_OK, S_FALSE if COM was already initialised the same way, or
	/// <see cref="PrintTicketConstants.ChangedMode"/>.</returns>
	[LibraryImport(PrintTicketConstants.ComLibraryName, EntryPoint = "CoInitializeEx")]
	internal static partial int CoInitializeEx(IntPtr reserved, uint coInit);

	/// <summary>CoUninitialize: balances a successful <see cref="CoInitializeEx"/>.</summary>
	[LibraryImport(PrintTicketConstants.ComLibraryName, EntryPoint = "CoUninitialize")]
	internal static partial void CoUninitialize();

	/// <summary>SysFreeString: frees a BSTR.</summary>
	/// <param name="value">The BSTR, or null.</param>
	[LibraryImport(PrintTicketConstants.AutomationLibraryName, EntryPoint = "SysFreeString")]
	internal static partial void SysFreeString(IntPtr value);
}
