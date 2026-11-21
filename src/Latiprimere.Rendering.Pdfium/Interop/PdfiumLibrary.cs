using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Latiprimere.Rendering.Pdfium.Interop;

/// <summary>
/// Process-wide PDFium state: one-time initialisation, the lock every PDFium call must hold, and documents left for
/// the next call to close.
/// </summary>
/// <remarks>
/// PDFium keeps global state and is not thread-safe, so this is deliberately static: there is exactly one PDFium per
/// process. Documents can be opened from any thread, but their calls are serialised.
/// </remarks>
internal static unsafe class PdfiumLibrary
{
	private static readonly ConcurrentQueue<PendingRelease> PendingReleases = new ConcurrentQueue<PendingRelease>();

	private static bool isInitialised = false; // Whether FPDF_InitLibrary has been called

	/// <summary>
	/// Gets the lock that serialises every PDFium call in the process.
	/// </summary>
	internal static Lock Lock { get; } = new Lock();

	/// <summary>
	/// Gets the number of documents waiting to be closed, for tests.
	/// </summary>
	internal static int PendingReleaseCount
	{
		get
		{
			return PdfiumLibrary.PendingReleases.Count;
		}
	}

	/// <summary>
	/// Leaves a document for the next PDFium call to close, for a finaliser that found <see cref="Lock"/> held. The
	/// finaliser thread serves the whole process, so it mustn't wait while another thread renders.
	/// </summary>
	/// <param name="document">The PDFium document handle.</param>
	/// <param name="page">Its loaded page, or <see cref="IntPtr.Zero"/>.</param>
	/// <param name="data">The native memory holding the PDF bytes, freed once the document is closed.</param>
	internal static void QueueRelease(IntPtr document, IntPtr page, IntPtr data)
	{
		PdfiumLibrary.PendingReleases.Enqueue(new PendingRelease(document, page, data));
	}

	/// <summary>
	/// Closes the documents left by finalisers. Call while holding <see cref="Lock"/>.
	/// </summary>
	internal static void ReleasePending()
	{
		while (PdfiumLibrary.PendingReleases.TryDequeue(out PendingRelease pending))
		{
			if (pending.Page != IntPtr.Zero)
			{
				PdfiumNative.ClosePage(pending.Page);
			}

			if (pending.Document != IntPtr.Zero)
			{
				PdfiumNative.CloseDocument(pending.Document);
			}

			// PDFium reads the bytes until the document is closed, so they go last
			NativeMemory.Free((void*)pending.Data);
		}
	}

	/// <summary>
	/// Initialises PDFium if it hasn't been yet. Call while holding <see cref="Lock"/>.
	/// </summary>
	internal static void EnsureInitialised()
	{
		if (!PdfiumLibrary.isInitialised)
		{
			PdfiumNative.InitLibrary();
			PdfiumLibrary.isInitialised = true;
		}
	}

	/// <summary>
	/// A document a finaliser left to be closed.
	/// </summary>
	/// <param name="Document">The PDFium document handle.</param>
	/// <param name="Page">Its loaded page, or <see cref="IntPtr.Zero"/>.</param>
	/// <param name="Data">The native memory holding the PDF bytes.</param>
	private readonly record struct PendingRelease(IntPtr Document, IntPtr Page, IntPtr Data);
}
