using Latiprimere.Rendering;

namespace Latiprimere.Windows.Tests.Fakes;

/// <summary>
/// A document of A4 pages that renders plain white, and records whether it was disposed.
/// </summary>
internal sealed class FakeRasterDocument : IRasterDocument
{
	/// <summary>
	/// Initialises a new instance of the <see cref="FakeRasterDocument"/> class.
	/// </summary>
	/// <param name="pageCount">The number of pages.</param>
	public FakeRasterDocument(int pageCount)
	{
		this.PageCount = pageCount;
	}

	/// <summary>Gets the number of pages.</summary>
	public int PageCount { get; }

	/// <summary>Gets a value indicating whether the document has been disposed.</summary>
	public bool IsDisposed { get; private set; }

	/// <summary>Returns A4 portrait for every page.</summary>
	/// <param name="pageIndex">Not used.</param>
	/// <returns>The page size.</returns>
	public PageSize GetPageSize(int pageIndex)
	{
		return new PageSize(595, 842);
	}

	/// <summary>Fills the band with white.</summary>
	/// <param name="pageIndex">Not used.</param>
	/// <param name="band">The band.</param>
	/// <param name="pixels">The pixels to fill.</param>
	/// <param name="options">Not used.</param>
	public void RenderBand(int pageIndex, RasterBand band, Span<byte> pixels, RasterRenderOptions? options = null)
	{
		pixels[..(int)band.ByteCount].Fill(byte.MaxValue);
	}

	/// <summary>Marks the document as disposed.</summary>
	public void Dispose()
	{
		this.IsDisposed = true;
	}
}
