using System.Runtime.InteropServices;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Mirrors <c>PRINTER_INFO_4W</c>: a printer's name only. Blittable: string fields are pointers into the buffer
/// returned by the spooler.
/// </summary>
/// <remarks>
/// EnumPrinters fills this level from the local registry, without contacting the print servers of the user's
/// printer connections, so it stays fast when one of those servers is unreachable.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct PrinterInfo4
{
	/// <summary><c>pPrinterName</c>: the printer's name; <c>\\server\printer</c> for a connection.</summary>
	public IntPtr PrinterName;

	/// <summary><c>pServerName</c>: the print server, or null for a local printer.</summary>
	public IntPtr ServerName;

	/// <summary><c>Attributes</c>: PRINTER_ATTRIBUTE_* flags.</summary>
	public uint Attributes;
}
