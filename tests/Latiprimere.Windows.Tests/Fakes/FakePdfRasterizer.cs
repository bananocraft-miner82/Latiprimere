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

	/// <summary>Gets the options the last document was opened with.</summary>
	public PdfOpenOptions? LastOptions { get; private set; }

	/// <summary>Gets the bytes of the last stream opened, from its position, such as a PDF an image became.</summary>
	public byte[]? LastContent { get; private set; }

	/// <summary>Opens a fake document.</summary>
	/// <param name="content">Read and kept, but not parsed; a stream that can seek is put back where it was, so tests
	/// can check where it was given from.</param>
	/// <param name="options">Kept, as <see cref="LastOptions"/>.</param>
	/// <returns>The document.</returns>
	public IRasterDocument Open(Stream content, PdfOpenOptions? options = null)
	{
		this.LastOptions = options;
		long? start = content.CanSeek ? content.Position : null;
		using MemoryStream copy = new MemoryStream();
		content.CopyTo(copy);
		this.LastContent = copy.ToArray();

		if (start is not null)
		{
			content.Position = start.Value;
		}

		this.LastDocument = new FakeRasterDocument(this.PageCount);

		return this.LastDocument;
	}
}
