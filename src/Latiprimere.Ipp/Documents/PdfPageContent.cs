using System.Text;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// What a page of a <see cref="PdfDocumentWriter"/> holds until it ends: its size, its content's operators, and the
/// resources they name.
/// </summary>
internal sealed class PdfPageContent
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PdfPageContent"/> class.
	/// </summary>
	/// <param name="width">The page's width in points.</param>
	/// <param name="height">Its height in points.</param>
	public PdfPageContent(double width, double height)
	{
		this.Width = width;
		this.Height = height;
	}

	/// <summary>Gets the page's width in points.</summary>
	public double Width { get; }

	/// <summary>Gets the page's height in points.</summary>
	public double Height { get; }

	/// <summary>Gets its content stream's operators so far.</summary>
	public StringBuilder Content { get; } = new StringBuilder();

	/// <summary>Gets the images it draws, by resource name and object number.</summary>
	public List<(string Name, int Number)> Images { get; } = new List<(string Name, int Number)>();

	/// <summary>Gets or sets a value indicating whether it sets text in Courier.</summary>
	public bool UsesFont { get; set; }
}
