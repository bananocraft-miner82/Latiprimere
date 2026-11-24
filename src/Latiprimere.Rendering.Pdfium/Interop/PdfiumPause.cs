using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Latiprimere.Rendering.Pdfium.Interop;

/// <summary>
/// Mirrors PDFium's <c>IFSDK_PAUSE</c>, which progressive rendering asks whether to stop for now. Its user data is a
/// deadline, a <see cref="Stopwatch"/> timestamp, so a page that takes too long to render can be abandoned (ADR 0058).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PdfiumPause
{
	/// <summary><c>version</c>: must be 1.</summary>
	public int Version;

	/// <summary><c>NeedToPauseNow</c>: called between steps of rendering; non-zero pauses it.</summary>
	public delegate* unmanaged[Cdecl]<PdfiumPause*, int> NeedToPauseNow;

	/// <summary><c>user</c>: the deadline, a pointer to a <see cref="Stopwatch"/> timestamp.</summary>
	public long* Deadline;

	/// <summary>
	/// Creates the pause for a deadline, which must stay where it is until rendering ends, as a local does.
	/// </summary>
	/// <param name="deadline">The deadline, a <see cref="Stopwatch"/> timestamp.</param>
	/// <returns>The pause.</returns>
	public static PdfiumPause Create(long* deadline)
	{
		return new PdfiumPause
		{
			Version        = 1,
			NeedToPauseNow = &PdfiumPause.IsPastDeadline,
			Deadline       = deadline
		};
	}

	/// <summary>
	/// Tells PDFium to pause once the deadline has passed.
	/// </summary>
	/// <param name="pause">The pause, with its deadline.</param>
	/// <returns>1 to pause, 0 to carry on.</returns>
	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static int IsPastDeadline(PdfiumPause* pause)
	{
		return Stopwatch.GetTimestamp() >= *pause->Deadline ? 1 : 0;
	}
}
