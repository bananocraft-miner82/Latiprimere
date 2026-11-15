namespace Latiprimere.Testing.Fixtures;

/// <summary>
/// Locates the shared test PDFs from tools/test-documents, which this library copies into the Data/ folder of every
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
