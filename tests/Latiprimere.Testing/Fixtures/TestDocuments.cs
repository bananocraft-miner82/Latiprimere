namespace Latiprimere.Testing.Fixtures;

/// <summary>
/// Locates the shared test PDFs and images from tools/test-documents, which this library copies into the Data/ folder of every
/// test project that references it.
/// </summary>
public static class TestDocuments
{
	/// <summary>One A4 page of mixed content.</summary>
	public const string MixedOnePage = "mixed-1-page.pdf";

	/// <summary>The same page repeated 100 times.</summary>
	public const string MixedHundredPages = "mixed-100-pages.pdf";

	/// <summary>Three pages of plain text, small enough for quick print jobs.</summary>
	public const string ThreePages = "three-pages.pdf";

	/// <summary>
	/// A 300 × 200 colour PNG at 96 dpi with a transparent disc: a black bar along the top and a red square in the
	/// bottom right show its orientation.
	/// </summary>
	public const string PhotoPng = "photo-300x200.png";

	/// <summary>The same image as a JPEG, its disc white.</summary>
	public const string PhotoJpeg = "photo-300x200.jpg";

	/// <summary>
	/// UTF-8 text in two pages split by a form feed: a tabbed table, accented words, a line long enough to wrap,
	/// and Windows-1252 punctuation.
	/// </summary>
	public const string TwoPagesText = "text-2-pages.txt";

	/// <summary>
	/// Opens a test document for reading.
	/// </summary>
	/// <param name="fileName">The document's file name.</param>
	/// <returns>A read-only stream. The caller disposes it.</returns>
	public static FileStream Open(string fileName)
	{
		string path = Path.Combine(AppContext.BaseDirectory, "Data", fileName);

		return File.OpenRead(path);
	}
}
