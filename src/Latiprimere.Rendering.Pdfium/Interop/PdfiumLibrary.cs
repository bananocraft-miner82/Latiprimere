namespace Latiprimere.Rendering.Pdfium.Interop;

/// <summary>
/// Process-wide PDFium state: one-time initialisation and the lock every PDFium call must hold.
/// </summary>
/// <remarks>
/// PDFium keeps global state and is not thread-safe, so this is deliberately static: there is exactly one PDFium per
/// process. Documents can be opened from any thread, but their calls are serialised.
/// </remarks>
internal static class PdfiumLibrary
{
	private static bool isInitialised = false; // Whether FPDF_InitLibrary has been called

	/// <summary>
	/// Gets the lock that serialises every PDFium call in the process.
	/// </summary>
	internal static Lock Lock { get; } = new Lock();

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
}
