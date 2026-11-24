using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Reads the PDFs that virtual printers write, through Latiprimere's own PDFium package: each page's size, and from a
/// small rendering of it, how much is inked and how much is in colour.
/// </summary>
/// <remarks>
/// Appearance isn't compared with a reference image: driver and PDFium updates would keep breaking such a check. A
/// person checks appearance in the harness's manual mode.
/// </remarks>
public static class PdfInspector
{
	/// <summary>
	/// The resolution pages are rendered at for measuring: enough to see text and images, small enough to be quick.
	/// </summary>
	private const double MeasureDpi = 36;

	/// <summary>
	/// A pixel darker than this in its lightest channel counts as ink.
	/// </summary>
	private const int InkThreshold = 230;

	/// <summary>
	/// A pixel whose channels differ by more than this counts as colour rather than grey.
	/// </summary>
	private const int ColourThreshold = 40;

	/// <summary>
	/// The first bytes of every PDF file.
	/// </summary>
	private static readonly byte[] PdfHeader = "%PDF-"u8.ToArray();

	/// <summary>
	/// Says whether bytes start like a PDF file.
	/// </summary>
	/// <param name="content">The bytes.</param>
	/// <returns><see langword="true"/> if they start with <c>%PDF-</c>.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	public static bool HasPdfHeader(byte[] content)
	{
		ArgumentNullException.ThrowIfNull(content);

		return content.AsSpan().StartsWith(PdfInspector.PdfHeader);
	}

	/// <summary>
	/// Reads every page of a PDF: its size, and how much of it is inked and in colour.
	/// </summary>
	/// <param name="content">The PDF.</param>
	/// <returns>What it holds.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	/// <exception cref="PrintException">PDFium can't open it.</exception>
	public static PdfSummary Inspect(byte[] content)
	{
		ArgumentNullException.ThrowIfNull(content);

		using MemoryStream stream = new MemoryStream(content, writable: false);
		using IRasterDocument document = new PdfiumRasterizer().Open(stream);
		List<PdfPage> pages = new List<PdfPage>();

		for (int index = 0; index < document.PageCount; index++)
		{
			pages.Add(PdfInspector.InspectPage(document, index));
		}

		return new PdfSummary(pages);
	}

	/// <summary>
	/// Reads a PDF file.
	/// </summary>
	/// <param name="path">The file.</param>
	/// <returns>What it holds.</returns>
	public static PdfSummary Inspect(string path)
	{
		return PdfInspector.Inspect(File.ReadAllBytes(path));
	}

	/// <summary>
	/// Renders one page small and measures it.
	/// </summary>
	/// <param name="document">The document.</param>
	/// <param name="index">The page's index.</param>
	/// <returns>The page.</returns>
	private static PdfPage InspectPage(IRasterDocument document, int index)
	{
		PageSize size = document.GetPageSize(index);
		int width = Math.Max(1, (int)Math.Round(size.WidthPoints / PageSize.PointsPerInch * PdfInspector.MeasureDpi));
		int height = Math.Max(1, (int)Math.Round(size.HeightPoints / PageSize.PointsPerInch * PdfInspector.MeasureDpi));
		RasterBand band = RasterBand.Create(width, height, 0, height);
		byte[] pixels = new byte[band.ByteCount];
		document.RenderBand(index, band, pixels);

		long inked = 0;
		long coloured = 0;

		for (int offset = 0; offset < pixels.Length; offset += RasterBand.GetBytesPerPixel(RasterPixelFormat.Bgra32))
		{
			// Blue, green, red, unused
			int blue = pixels[offset];
			int green = pixels[offset + 1];
			int red = pixels[offset + 2];
			int lightest = Math.Min(red, Math.Min(green, blue));
			int spread = Math.Max(red, Math.Max(green, blue)) - lightest;

			if (lightest < PdfInspector.InkThreshold)
			{
				inked++;
			}

			if (spread > PdfInspector.ColourThreshold)
			{
				coloured++;
			}
		}

		double total = (double)width * height;

		return new PdfPage(size, inked / total, coloured / total);
	}
}
