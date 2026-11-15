using System.Runtime.InteropServices;
using Latiprimere.Rendering.Pdfium.Interop;
using Latiprimere.Rendering.Pdfium.Resources;

namespace Latiprimere.Rendering.Pdfium;

/// <summary>
/// A PDF opened in PDFium, rendered one band at a time.
/// </summary>
/// <remarks>
/// The most recently rendered page stays loaded, because banded rendering asks for the same page many times in a row.
/// </remarks>
internal sealed unsafe class PdfiumDocument : IRasterDocument
{
	private readonly IntPtr data;            // Native copy of the PDF bytes, read by PDFium until it closes
	private IntPtr document;                 // PDFium document handle
	private IntPtr loadedPage = IntPtr.Zero; // The page kept loaded between bands
	private int loadedPageIndex = -1;        // Index of the loaded page, or -1
	private bool isDisposed = false;         // Whether Dispose has run

	/// <summary>
	/// Initialises a new instance of the <see cref="PdfiumDocument"/> class.
	/// </summary>
	/// <param name="document">The PDFium document handle. The new instance owns it.</param>
	/// <param name="data">The native memory holding the PDF bytes. The new instance owns it.</param>
	/// <param name="pageCount">The number of pages.</param>
	internal PdfiumDocument(IntPtr document, IntPtr data, int pageCount)
	{
		this.document = document;
		this.data = data;
		this.PageCount = pageCount;
	}

	/// <summary>
	/// Finalises an instance of the <see cref="PdfiumDocument"/> class, releasing PDFium resources if Dispose was not
	/// called.
	/// </summary>
	~PdfiumDocument()
	{
		this.Release();
	}

	/// <inheritdoc/>
	public int PageCount { get; }

	/// <inheritdoc/>
	public PageSize GetPageSize(int pageIndex)
	{
		this.ValidatePageIndex(pageIndex);

		lock (PdfiumLibrary.Lock)
		{
			// Again under the lock: Dispose on another thread may have closed the document since the first check
			ObjectDisposedException.ThrowIf(this.isDisposed, this);
			PdfiumNative.GetPageSizeByIndex(this.document, pageIndex, out PdfiumSize size);

			return new PageSize(size.Width, size.Height);
		}
	}

	/// <inheritdoc/>
	public void RenderBand(int pageIndex, RasterBand band, Span<byte> pixels)
	{
		this.ValidatePageIndex(pageIndex);
		PdfiumDocument.ValidateBand(band);

		if (pixels.Length < band.ByteCount)
		{
			string message = ExceptionMessages.FormatPixelBufferTooSmall(pixels.Length, band.ByteCount);

			throw new ArgumentException(message, nameof(pixels));
		}

		// The pixels stay pinned while PDFium writes into them
		fixed (byte* firstRow = pixels)
		{
			lock (PdfiumLibrary.Lock)
			{
				// Again under the lock: Dispose on another thread may have closed the document since the first check
				ObjectDisposedException.ThrowIf(this.isDisposed, this);
				IntPtr page = this.LoadPage(pageIndex);
				PdfiumDocument.RenderInto(page, band, firstRow);
			}
		}
	}

	/// <summary>
	/// Closes the document and frees its memory.
	/// </summary>
	public void Dispose()
	{
		this.Release();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Checks a band's geometry.
	/// </summary>
	/// <param name="band">The band.</param>
	/// <exception cref="ArgumentOutOfRangeException">The band's geometry is invalid.</exception>
	private static void ValidateBand(RasterBand band)
	{
		bool isInsidePage = band.PageWidth > 0
								&& band.PageHeight > 0
								&& band.Top >= 0
								&& band.Height > 0
								&& band.Top + band.Height <= band.PageHeight;

		if (!isInsidePage)
		{
			throw new ArgumentOutOfRangeException(nameof(band), band, ExceptionMessages.BandOutsidePage);
		}

		if (band.Stride < band.PageWidth * RasterBand.BytesPerPixel)
		{
			throw new ArgumentOutOfRangeException(nameof(band), band, ExceptionMessages.StrideTooSmall);
		}
	}

	/// <summary>
	/// Renders a band of a loaded page into the caller's pixels. Call while holding the PDFium lock.
	/// </summary>
	/// <param name="page">The loaded page.</param>
	/// <param name="band">The band.</param>
	/// <param name="firstRow">The first byte of the pixel buffer.</param>
	/// <exception cref="PrintException">PDFium could not create the bitmap.</exception>
	private static void RenderInto(IntPtr page, RasterBand band, byte* firstRow)
	{
		IntPtr bitmap = PdfiumNative.CreateBitmap
		(
			band.PageWidth, band.Height, PdfiumConstants.BitmapFormatBgrx, firstRow, band.Stride
		);

		if (bitmap == IntPtr.Zero)
		{
			throw new PrintException(ExceptionMessages.BitmapCreateFailed);
		}

		try
		{
			// Paint white, then draw the page shifted up so only this band's rows land in the bitmap
			// The result is ignored: filling a valid bitmap can't fail
			_ = PdfiumNative.FillRect(bitmap, 0, 0, band.PageWidth, band.Height, PdfiumConstants.White);

			PdfiumNative.RenderPageBitmap
			(
				bitmap, page, 0, -band.Top, band.PageWidth, band.PageHeight, 0,
				PdfiumConstants.RenderAnnotations | PdfiumConstants.RenderPrinting
			);
		}
		finally
		{
			PdfiumNative.DestroyBitmap(bitmap);
		}
	}

	/// <summary>
	/// Returns the requested page, loading it if it isn't the one already loaded. Call while holding the PDFium lock.
	/// </summary>
	/// <param name="pageIndex">The zero-based page index.</param>
	/// <returns>The page handle.</returns>
	/// <exception cref="PrintException">PDFium could not load the page.</exception>
	private IntPtr LoadPage(int pageIndex)
	{
		if (this.loadedPageIndex == pageIndex)
		{
			return this.loadedPage;
		}

		this.CloseLoadedPage();
		IntPtr page = PdfiumNative.LoadPage(this.document, pageIndex);

		if (page == IntPtr.Zero)
		{
			string message = ExceptionMessages.FormatPageLoadFailed(pageIndex + 1);

			throw new PrintException(message);
		}

		this.loadedPage = page;
		this.loadedPageIndex = pageIndex;

		return page;
	}

	/// <summary>
	/// Closes the loaded page, if any. Call while holding the PDFium lock.
	/// </summary>
	private void CloseLoadedPage()
	{
		if (this.loadedPage != IntPtr.Zero)
		{
			PdfiumNative.ClosePage(this.loadedPage);
			this.loadedPage = IntPtr.Zero;
			this.loadedPageIndex = -1;
		}
	}

	/// <summary>
	/// Checks the document is open and the page index is valid.
	/// </summary>
	/// <param name="pageIndex">The zero-based page index.</param>
	/// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The page index is out of range.</exception>
	private void ValidatePageIndex(int pageIndex)
	{
		ObjectDisposedException.ThrowIf(this.isDisposed, this);
		ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, this.PageCount);
	}

	/// <summary>
	/// Releases the page, the document and the PDF bytes, once.
	/// </summary>
	private void Release()
	{
		lock (PdfiumLibrary.Lock)
		{
			if (this.isDisposed)
			{
				return;
			}

			this.CloseLoadedPage();
			PdfiumNative.CloseDocument(this.document);
			this.document = IntPtr.Zero;
			this.isDisposed = true;
		}

		// PDFium may read the bytes until the document is closed, so free them last
		NativeMemory.Free((void*)this.data);
	}
}
