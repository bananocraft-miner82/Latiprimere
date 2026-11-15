using System.Runtime.InteropServices;

namespace Latiprimere.Windows.Interop;


/// <summary>
/// Mirrors <c>DOC_INFO_1W</c>. Blittable: string fields are pointers the caller keeps pinned during the call.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DocInfo1
{
	/// <summary><c>pDocName</c>: the name shown in the print queue.</summary>
	public IntPtr DocumentName;

	/// <summary><c>pOutputFile</c>: a file to print to instead of the printer's port, or null.</summary>
	public IntPtr OutputFile;

	/// <summary><c>pDatatype</c>: the data type, such as RAW.</summary>
	public IntPtr DataType;
}
