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

	/// <summary>FPDF_ANNOT: render annotations.</summary>
	internal const int RenderAnnotations = 0x01;

	/// <summary>FPDF_PRINTING: render for printing, honouring print-only content.</summary>
	internal const int RenderPrinting = 0x800;

	/// <summary>Opaque white in 0xAARRGGBB form.</summary>
	internal const uint White = 0xFFFFFFFF;
}
