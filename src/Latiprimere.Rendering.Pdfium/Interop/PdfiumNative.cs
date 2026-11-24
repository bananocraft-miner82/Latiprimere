using System.Runtime.InteropServices;

namespace Latiprimere.Rendering.Pdfium.Interop;

/// <summary>
/// Source-generated declarations for the PDFium functions Latiprimere uses (fpdfview.h).
/// </summary>
/// <remarks>
/// PDFium is not thread-safe: every call must be made while holding <see cref="PdfiumLibrary.Lock"/>.
/// </remarks>
internal static unsafe partial class PdfiumNative
{
	/// <summary>The PDFium library name; resolves to pdfium.dll, libpdfium.so or libpdfium.dylib.</summary>
	internal const string LibraryName = "pdfium";

	/// <summary>FPDF_LoadMemDocument64: opens a document from memory that must stay valid until it is closed.</summary>
	/// <param name="data">The document bytes.</param>
	/// <param name="size">The number of bytes.</param>
	/// <param name="password">The password, or null.</param>
	/// <returns>The document handle, or zero on failure.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_LoadMemDocument64", StringMarshalling = StringMarshalling.Utf8)]
	internal static partial IntPtr LoadMemDocument(byte* data, nuint size, string? password);

	/// <summary>FPDF_CloseDocument.</summary>
	/// <param name="document">The document handle.</param>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_CloseDocument")]
	internal static partial void CloseDocument(IntPtr document);

	/// <summary>FPDF_GetLastError: the reason the last load failed.</summary>
	/// <returns>An FPDF_ERR_* code.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_GetLastError")]
	internal static partial uint GetLastError();

	/// <summary>FPDF_GetPageCount.</summary>
	/// <param name="document">The document handle.</param>
	/// <returns>The number of pages.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_GetPageCount")]
	internal static partial int GetPageCount(IntPtr document);

	/// <summary>FPDF_GetPageSizeByIndexF: a page's size without loading it.</summary>
	/// <param name="document">The document handle.</param>
	/// <param name="pageIndex">The zero-based page index.</param>
	/// <param name="size">Receives the width and height in points.</param>
	/// <returns>Non-zero on success.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_GetPageSizeByIndexF")]
	internal static partial int GetPageSizeByIndex(IntPtr document, int pageIndex, out PdfiumSize size);

	/// <summary>FPDF_LoadPage.</summary>
	/// <param name="document">The document handle.</param>
	/// <param name="pageIndex">The zero-based page index.</param>
	/// <returns>The page handle, or zero on failure.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_LoadPage")]
	internal static partial IntPtr LoadPage(IntPtr document, int pageIndex);

	/// <summary>FPDF_ClosePage.</summary>
	/// <param name="page">The page handle.</param>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_ClosePage")]
	internal static partial void ClosePage(IntPtr page);

	/// <summary>FPDFBitmap_CreateEx: a bitmap over caller-owned memory.</summary>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	/// <param name="format">An FPDFBitmap_* format.</param>
	/// <param name="firstScan">The caller's buffer.</param>
	/// <param name="stride">Bytes per row.</param>
	/// <returns>The bitmap handle, or zero on failure.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDFBitmap_CreateEx")]
	internal static partial IntPtr CreateBitmap(int width, int height, int format, byte* firstScan, int stride);

	/// <summary>FPDFBitmap_FillRect. Returns a BOOL in newer PDFium and void in older; the result is ignored.</summary>
	/// <param name="bitmap">The bitmap handle.</param>
	/// <param name="left">The left edge.</param>
	/// <param name="top">The top edge.</param>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <param name="color">The colour as 0xAARRGGBB.</param>
	/// <returns>Ignored.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDFBitmap_FillRect")]
	internal static partial int FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

	/// <summary>
	/// FPDF_RenderPageBitmap_Start: renders a page into a bitmap, clipped to the bitmap's bounds, asking the pause
	/// between steps whether to stop for now.
	/// </summary>
	/// <param name="bitmap">The bitmap handle.</param>
	/// <param name="page">The page handle.</param>
	/// <param name="startX">Where the page's left edge falls in the bitmap.</param>
	/// <param name="startY">Where the page's top edge falls in the bitmap.</param>
	/// <param name="sizeX">The width the whole page is rendered at.</param>
	/// <param name="sizeY">The height the whole page is rendered at.</param>
	/// <param name="rotate">Extra rotation in quarter turns.</param>
	/// <param name="flags">FPDF_* render flags.</param>
	/// <param name="pause">The pause.</param>
	/// <returns>An FPDF_RENDER_* status.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_RenderPageBitmap_Start")]
	internal static partial int RenderPageBitmapStart
	(
		IntPtr       bitmap,
		IntPtr       page,
		int          startX,
		int          startY,
		int          sizeX,
		int          sizeY,
		int          rotate,
		int          flags,
		PdfiumPause* pause
	);

	/// <summary>FPDF_RenderPage_Continue: carries on rendering a page that paused.</summary>
	/// <param name="page">The page handle.</param>
	/// <param name="pause">The pause.</param>
	/// <returns>An FPDF_RENDER_* status.</returns>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_RenderPage_Continue")]
	internal static partial int RenderPageContinue(IntPtr page, PdfiumPause* pause);

	/// <summary>FPDF_RenderPage_Close: ends a page's progressive rendering, finished or not, and frees its state.</summary>
	/// <param name="page">The page handle.</param>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_RenderPage_Close")]
	internal static partial void RenderPageClose(IntPtr page);

	/// <summary>FPDFBitmap_Destroy. Does not free caller-owned memory.</summary>
	/// <param name="bitmap">The bitmap handle.</param>
	[LibraryImport(LibraryName, EntryPoint = "FPDFBitmap_Destroy")]
	internal static partial void DestroyBitmap(IntPtr bitmap);

	/// <summary>FPDF_InitLibrary: must be called once before anything else.</summary>
	[LibraryImport(LibraryName, EntryPoint = "FPDF_InitLibrary")]
	internal static partial void InitLibrary();
}
