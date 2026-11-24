namespace Latiprimere.Rendering.Pdfium.Interop;

/// <summary>
/// Constants from PDFium's public headers.
/// </summary>
internal static class PdfiumConstants
{
	/// <summary>FPDF_ERR_PASSWORD: the document needs a password.</summary>
	internal const uint ErrorPassword = 4;

	/// <summary>
	/// FPDFBitmap_BGRx: 4 bytes per pixel, B G R and an unused byte. (2 is FPDFBitmap_BGR, 3 bytes per pixel.)
	/// </summary>
	internal const int BitmapFormatBgrx = 3;

	/// <summary>FPDFBitmap_Gray: 1 byte per pixel of grey.</summary>
	internal const int BitmapFormatGray = 1;

	/// <summary>FPDF_ANNOT: render annotations.</summary>
	internal const int RenderAnnotations = 0x01;

	/// <summary>FPDF_PRINTING: render for printing, honouring print-only content.</summary>
	internal const int RenderPrinting = 0x800;

	/// <summary>FPDF_RENDER_NO_SMOOTHTEXT: text without anti-aliasing.</summary>
	internal const int RenderNoSmoothText = 0x1000;

	/// <summary>FPDF_RENDER_NO_SMOOTHIMAGE: images scaled without smoothing.</summary>
	internal const int RenderNoSmoothImage = 0x2000;

	/// <summary>FPDF_RENDER_NO_SMOOTHPATH: lines and shapes without anti-aliasing.</summary>
	internal const int RenderNoSmoothPath = 0x4000;

	/// <summary>Opaque white in 0xAARRGGBB form.</summary>
	internal const uint White = 0xFFFFFFFF;
}
