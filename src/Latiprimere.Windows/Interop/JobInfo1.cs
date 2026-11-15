using System.Runtime.InteropServices;

namespace Latiprimere.Windows.Interop;


/// <summary>
/// Mirrors <c>JOB_INFO_1W</c>. Blittable: string fields are pointers into the buffer returned by the spooler.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct JobInfo1
{
	/// <summary><c>JobId</c>.</summary>
	public uint JobId;

	/// <summary><c>pPrinterName</c>.</summary>
	public IntPtr PrinterName;

	/// <summary><c>pMachineName</c>.</summary>
	public IntPtr MachineName;

	/// <summary><c>pUserName</c>: the job's owner.</summary>
	public IntPtr UserName;

	/// <summary><c>pDocument</c>: the document name.</summary>
	public IntPtr Document;

	/// <summary><c>pDatatype</c>.</summary>
	public IntPtr DataType;

	/// <summary><c>pStatus</c>: a status description, if the port monitor or driver gave one.</summary>
	public IntPtr StatusText;

	/// <summary><c>Status</c>: JOB_STATUS_* flags.</summary>
	public uint Status;

	/// <summary><c>Priority</c>.</summary>
	public uint Priority;

	/// <summary><c>Position</c>: the job's position in the queue.</summary>
	public uint Position;

	/// <summary><c>TotalPages</c>: the number of pages in the job, if known.</summary>
	public uint TotalPages;

	/// <summary><c>PagesPrinted</c>.</summary>
	public uint PagesPrinted;

	/// <summary><c>Submitted.wYear</c> (SYSTEMTIME is eight WORDs, so it is not padded to 8 bytes).</summary>
	public ushort SubmittedYear;

	/// <summary><c>Submitted.wMonth</c>.</summary>
	public ushort SubmittedMonth;

	/// <summary><c>Submitted.wDayOfWeek</c>.</summary>
	public ushort SubmittedDayOfWeek;

	/// <summary><c>Submitted.wDay</c>.</summary>
	public ushort SubmittedDay;

	/// <summary><c>Submitted.wHour</c>.</summary>
	public ushort SubmittedHour;

	/// <summary><c>Submitted.wMinute</c>.</summary>
	public ushort SubmittedMinute;

	/// <summary><c>Submitted.wSecond</c>.</summary>
	public ushort SubmittedSecond;

	/// <summary><c>Submitted.wMilliseconds</c>.</summary>
	public ushort SubmittedMilliseconds;
}
