using System.Runtime.InteropServices;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Mirrors <c>DOCINFOW</c>, used by GDI's <c>StartDoc</c>. Blittable: string fields are pointers the caller keeps
/// pinned during the call.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct GdiDocInfo
{
	/// <summary><c>cbSize</c>: the size of this structure.</summary>
	public int Size;

	/// <summary><c>lpszDocName</c>: the name shown in the print queue.</summary>
	public IntPtr DocumentName;

	/// <summary><c>lpszOutput</c>: a file to print to instead of the printer's port, or null.</summary>
	public IntPtr Output;

	/// <summary><c>lpszDatatype</c>: the spool data type, or null for the default.</summary>
	public IntPtr DataType;

	/// <summary><c>fwType</c>.</summary>
	public uint Type;
}
