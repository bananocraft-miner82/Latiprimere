namespace Latiprimere.Windows.Interop;

/// <summary>
/// The PRINTER_STATUS_* flags the spooler reports in <c>PRINTER_INFO_2.Status</c>.
/// </summary>
[Flags]
internal enum PrinterStatusFlags : uint
{
	/// <summary>No flags: the printer is ready.</summary>
	None = 0,

	/// <summary>PRINTER_STATUS_PAUSED.</summary>
	Paused = 1 << 0,

	/// <summary>PRINTER_STATUS_ERROR.</summary>
	Error = 1 << 1,

	/// <summary>PRINTER_STATUS_PENDING_DELETION.</summary>
	PendingDeletion = 1 << 2,

	/// <summary>PRINTER_STATUS_PAPER_JAM.</summary>
	PaperJam = 1 << 3,

	/// <summary>PRINTER_STATUS_PAPER_OUT.</summary>
	PaperOut = 1 << 4,

	/// <summary>PRINTER_STATUS_MANUAL_FEED.</summary>
	ManualFeed = 1 << 5,

	/// <summary>PRINTER_STATUS_PAPER_PROBLEM.</summary>
	PaperProblem = 1 << 6,

	/// <summary>PRINTER_STATUS_OFFLINE.</summary>
	Offline = 1 << 7,

	/// <summary>PRINTER_STATUS_IO_ACTIVE.</summary>
	IOActive = 1 << 8,

	/// <summary>PRINTER_STATUS_BUSY.</summary>
	Busy = 1 << 9,

	/// <summary>PRINTER_STATUS_PRINTING.</summary>
	Printing = 1 << 10,

	/// <summary>PRINTER_STATUS_OUTPUT_BIN_FULL.</summary>
	OutputBinFull = 1 << 11,

	/// <summary>PRINTER_STATUS_NOT_AVAILABLE.</summary>
	NotAvailable = 1 << 12,

	/// <summary>PRINTER_STATUS_WAITING.</summary>
	Waiting = 1 << 13,

	/// <summary>PRINTER_STATUS_PROCESSING.</summary>
	Processing = 1 << 14,

	/// <summary>PRINTER_STATUS_INITIALIZING.</summary>
	Initializing = 1 << 15,

	/// <summary>PRINTER_STATUS_WARMING_UP.</summary>
	WarmingUp = 1 << 16,

	/// <summary>PRINTER_STATUS_TONER_LOW.</summary>
	TonerLow = 1 << 17,

	/// <summary>PRINTER_STATUS_NO_TONER.</summary>
	NoToner = 1 << 18,

	/// <summary>PRINTER_STATUS_PAGE_PUNT.</summary>
	PagePunt = 1 << 19,

	/// <summary>PRINTER_STATUS_USER_INTERVENTION.</summary>
	UserIntervention = 1 << 20,

	/// <summary>PRINTER_STATUS_OUT_OF_MEMORY.</summary>
	OutOfMemory = 1 << 21,

	/// <summary>PRINTER_STATUS_DOOR_OPEN.</summary>
	DoorOpen = 1 << 22,

	/// <summary>PRINTER_STATUS_SERVER_UNKNOWN.</summary>
	ServerUnknown = 1 << 23,

	/// <summary>PRINTER_STATUS_POWER_SAVE.</summary>
	PowerSave = 1 << 24,
}
