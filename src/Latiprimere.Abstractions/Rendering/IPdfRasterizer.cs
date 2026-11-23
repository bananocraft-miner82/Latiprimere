namespace Latiprimere.Rendering;

/// <summary>
/// Turns PDF documents into pixels, for print systems that need images rather than PDF, such as the Windows spooler.
/// </summary>
/// <remarks>
/// Rasterising is CPU-bound, so the members are synchronous; print systems call them off the caller's thread.
/// </remarks>
public interface IPdfRasterizer
{
	/// <summary>
	/// Opens a PDF document for rasterising.
	/// </summary>
	/// <param name="content">
	/// A readable stream containing the PDF, read from its current position to the end. The caller keeps ownership of
	/// the stream; the document does not need it after this call returns.
	/// </param>
	/// <param name="options">How to open it, such as its password; <see langword="null"/> for
	/// <see cref="PdfOpenOptions.Default"/>.</param>
	/// <returns>The opened document. Dispose it when finished.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	/// <exception cref="PrintException">The content is not a PDF the rasteriser can read, or is password-protected
	/// and the password is missing or wrong.</exception>
	IRasterDocument Open(Stream content, PdfOpenOptions? options = null);
}
