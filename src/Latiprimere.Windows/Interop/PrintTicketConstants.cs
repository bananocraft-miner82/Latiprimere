namespace Latiprimere.Windows.Interop;

/// <summary>
/// Constants from the Windows Print Ticket API (prntvpt.h) and the COM calls it needs.
/// </summary>
internal static class PrintTicketConstants
{
	/// <summary>The Print Ticket library.</summary>
	internal const string LibraryName = "prntvpt.dll";

	/// <summary>The COM library, for streams and initialisation.</summary>
	internal const string ComLibraryName = "ole32.dll";

	/// <summary>The OLE automation library, for BSTRs.</summary>
	internal const string AutomationLibraryName = "oleaut32.dll";

	/// <summary>The Print Schema version PTOpenProvider is asked for: 1, the only one.</summary>
	internal const uint SchemaVersion = 1;

	/// <summary>EPrintTicketScope kPTJobScope: job, document and page settings together.</summary>
	internal const int JobScope = 2;

	/// <summary>EDefaultDevmodeType kUserDefaultDevmode: fill what a ticket leaves out from the user's
	/// defaults.</summary>
	internal const int UserDefaultDevMode = 0;

	/// <summary>S_PT_NO_CONFLICT: the merged ticket needed no changes.</summary>
	internal const int NoConflict = 0x00040001;

	/// <summary>S_PT_CONFLICT_RESOLVED: the driver changed settings that conflicted, as DocumentProperties
	/// does.</summary>
	internal const int ConflictResolved = 0x00040002;

	/// <summary>E_PRINTTICKET_FORMAT: a ticket isn't a valid Print Ticket.</summary>
	internal const int PrintTicketFormatError = unchecked((int)0x80040003);

	/// <summary>E_DELTA_PRINTTICKET_FORMAT: the ticket merged in isn't a valid Print Ticket.</summary>
	internal const int DeltaPrintTicketFormatError = unchecked((int)0x80040005);

	/// <summary>The facility of an HRESULT that wraps a Win32 error, shifted into place.</summary>
	internal const int Win32Facility = unchecked((int)0x80070000);

	/// <summary>HRESULT_FROM_WIN32(ERROR_WRITE_FAULT): a stream took fewer bytes than it was given.</summary>
	internal const int WriteFault = unchecked((int)0x8007001D);

	/// <summary>RPC_E_CHANGED_MODE: the thread already has COM, as a single-threaded apartment.</summary>
	internal const int ChangedMode = unchecked((int)0x80010106);

	/// <summary>CoInitializeEx: COINIT_MULTITHREADED.</summary>
	internal const uint MultiThreaded = 0x0;

	/// <summary>IStream::Seek origin STREAM_SEEK_SET.</summary>
	internal const uint SeekFromStart = 0;
}
