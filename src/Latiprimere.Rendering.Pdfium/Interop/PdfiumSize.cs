using System.Runtime.InteropServices;

namespace Latiprimere.Rendering.Pdfium.Interop;

/// <summary>
/// Mirrors PDFium's <c>FS_SIZEF</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct PdfiumSize
{
	/// <summary><c>width</c> in points.</summary>
	public float Width;

	/// <summary><c>height</c> in points.</summary>
	public float Height;
}
