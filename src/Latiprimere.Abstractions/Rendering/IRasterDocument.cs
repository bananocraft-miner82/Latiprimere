namespace Latiprimere.Rendering;

/// <summary>
/// An opened document whose pages can be rendered to pixels, one horizontal band at a time.
/// </summary>
/// <remarks>
/// Pixels are 32 bits each in B, G, R, unused byte order, rows top to bottom, on a white background: the layout of a
/// top-down 32-bit Windows device-independent bitmap. Rendering in bands keeps memory bounded at printer
/// resolutions, where a whole A4 page at 600 DPI would need about 139 MB.
/// </remarks>
public interface IRasterDocument : IDisposable
{
	/// <summary>
	/// Gets the number of pages in the document.
	/// </summary>
	int PageCount { get; }

	/// <summary>
	/// Gets the size of a page, as it would be displayed (after any rotation the document specifies).
	/// </summary>
	/// <param name="pageIndex">The zero-based page index.</param>
	/// <returns>The page size in points (1/72 inch).</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a valid page
	/// index.</exception>
	/// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
	PageSize GetPageSize(int pageIndex);

	/// <summary>
	/// Renders part of a page.
	/// </summary>
	/// <param name="pageIndex">The zero-based page index.</param>
	/// <param name="band">The size the whole page is rendered at, and which rows of it to produce.</param>
	/// <param name="pixels">Receives the band's pixels; at least <see cref="RasterBand.Stride"/> × <see
	/// cref="RasterBand.Height"/> bytes.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> or <paramref name="band"/> is
	/// invalid.</exception>
	/// <exception cref="ArgumentException"><paramref name="pixels"/> is too small for the band.</exception>
	/// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
	/// <exception cref="PrintException">The rasteriser failed to render the page.</exception>
	void RenderBand(int pageIndex, RasterBand band, Span<byte> pixels);
}
