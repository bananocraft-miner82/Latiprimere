using Latiprimere.Rendering;

namespace Latiprimere.Ipp.Tests.Raster;

/// <summary>
/// A rasteriser that opens every PDF as a <see cref="QuadrantDocument"/> of portrait A4 pages, so tests can follow
/// what is rendered without PDFium.
/// </summary>
internal sealed class QuadrantRasterizer : IPdfRasterizer
{
	/// <summary>Portrait A4, in points.</summary>
	internal static readonly PageSize PortraitA4 = new PageSize(595.28, 841.89);

	private readonly int pageCount; // Pages in each document opened

	/// <summary>
	/// Initialises a new instance of the <see cref="QuadrantRasterizer"/> class.
	/// </summary>
	/// <param name="pageCount">Pages in each document opened.</param>
	internal QuadrantRasterizer(int pageCount)
	{
		this.pageCount = pageCount;
	}

	/// <summary>Gets every document opened, in order.</summary>
	internal List<QuadrantDocument> Opened { get; } = [];

	/// <summary>Gets the options each document was opened with, in order.</summary>
	internal List<PdfOpenOptions?> OpenedWith { get; } = [];

	/// <summary>Gets or sets the failure rendering throws, if any.</summary>
	internal Exception? RenderFailure { get; set; }

	/// <inheritdoc/>
	public IRasterDocument Open(Stream content, PdfOpenOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(content);

		PageSize[] pages = Enumerable.Repeat(QuadrantRasterizer.PortraitA4, this.pageCount).ToArray();
		QuadrantDocument document = new QuadrantDocument(pages) { RenderFailure = this.RenderFailure };
		this.Opened.Add(document);
		this.OpenedWith.Add(options);

		return document;
	}
}
