using Latiprimere.Rendering;

namespace Latiprimere.Windows.Tests.Fakes;

/// <summary>
/// A rasteriser that opens every stream as a document with a fixed number of pages.
/// </summary>
internal sealed class FakePdfRasterizer : IPdfRasterizer
{
	/// <summary>Gets or sets the number of pages each opened document has.</summary>
	public int PageCount { get; set; } = 2;

	/// <summary>Gets the last document opened.</summary>
	public FakeRasterDocument? LastDocument { get; private set; }

	/// <summary>Opens a fake document.</summary>
	/// <param name="content">Not read.</param>
	/// <returns>The document.</returns>
	public IRasterDocument Open(Stream content)
	{
		this.LastDocument = new FakeRasterDocument(this.PageCount);

		return this.LastDocument;
	}
}
