using System.Runtime.InteropServices;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Mirrors <c>PRINTER_INFO_2W</c>. Blittable: string fields are pointers into the buffer returned by the spooler.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PrinterInfo2
{
	/// <summary><c>pServerName</c>: the print server, or null for a local printer.</summary>
	public IntPtr ServerName;

	/// <summary><c>pPrinterName</c>: the printer's name.</summary>
	public IntPtr PrinterName;

	/// <summary><c>pShareName</c>: the share name, if shared.</summary>
	public IntPtr ShareName;

	/// <summary><c>pPortName</c>: the port or ports the printer uses.</summary>
	public IntPtr PortName;

	/// <summary><c>pDriverName</c>: the printer driver's name.</summary>
	public IntPtr DriverName;

	/// <summary><c>pComment</c>: the printer's description.</summary>
	public IntPtr Comment;

	/// <summary><c>pLocation</c>: the printer's location.</summary>
	public IntPtr Location;

	/// <summary><c>pDevMode</c>: the default device mode.</summary>
	public IntPtr DevMode;

	/// <summary><c>pSepFile</c>: the separator page file.</summary>
	public IntPtr SeparatorFile;

	/// <summary><c>pPrintProcessor</c>: the print processor.</summary>
	public IntPtr PrintProcessor;

	/// <summary><c>pDatatype</c>: the default data type.</summary>
	public IntPtr DataType;

	/// <summary><c>pParameters</c>: the print processor parameters.</summary>
	public IntPtr Parameters;

	/// <summary><c>pSecurityDescriptor</c>: the printer's security descriptor.</summary>
	public IntPtr SecurityDescriptor;

	/// <summary><c>Attributes</c>: PRINTER_ATTRIBUTE_* flags.</summary>
	public uint Attributes;

	/// <summary><c>Priority</c>.</summary>
	public uint Priority;

	/// <summary><c>DefaultPriority</c>.</summary>
	public uint DefaultPriority;

	/// <summary><c>StartTime</c>.</summary>
	public uint StartTime;

	/// <summary><c>UntilTime</c>.</summary>
	public uint UntilTime;

	/// <summary><c>Status</c>: PRINTER_STATUS_* flags.</summary>
	public uint Status;

	/// <summary><c>cJobs</c>: the number of queued jobs.</summary>
	public uint JobCount;

	/// <summary><c>AveragePPM</c>.</summary>
	public uint AveragePagesPerMinute;
}
