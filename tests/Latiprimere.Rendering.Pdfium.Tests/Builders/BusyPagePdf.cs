using System.Globalization;
using System.Text;

namespace Latiprimere.Rendering.Pdfium.Tests.Builders;

/// <summary>
/// Makes a one-page PDF whose page is drawn as thousands of separate shapes, so PDFium's progressive rendering checks
/// whether to pause many times while drawing it, as it would for a page built to keep a renderer busy.
/// </summary>
internal static class BusyPagePdf
{
	/// <summary>How many filled squares the page draws: far past the 100 PDFium draws between checks.</summary>
	internal const int ShapeCount = 5000;

	/// <summary>
	/// Makes the PDF.
	/// </summary>
	/// <returns>The PDF's bytes.</returns>
	internal static byte[] Create()
	{
		StringBuilder content = new StringBuilder();

		for (int shape = 0; shape < BusyPagePdf.ShapeCount; shape++)
		{
			int x = 20 + (shape % 100 * 5);
			int y = 20 + (shape / 100 * 5);
			content.Append(CultureInfo.InvariantCulture, $"{x} {y} 3 3 re f\n");
		}

		string[] objects =
		[
			"<< /Type /Catalog /Pages 2 0 R >>",
			"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
			"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R >>",
			string.Create(CultureInfo.InvariantCulture, $"<< /Length {content.Length} >>\nstream\n{content}endstream")
		];

		StringBuilder pdf = new StringBuilder("%PDF-1.4\n");
		List<int> offsets = new List<int>();

		for (int index = 0; index < objects.Length; index++)
		{
			offsets.Add(pdf.Length);
			pdf.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
		}

		int xref = pdf.Length;
		pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");

		foreach (int offset in offsets)
		{
			pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
		}

		pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

		return Encoding.ASCII.GetBytes(pdf.ToString());
	}
}
