namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Where an image goes: the page's size, and the matrix that draws the image on it.
/// </summary>
/// <param name="PageWidth">The page's width in points: the paper's, or its height for a landscape page.</param>
/// <param name="PageHeight">The page's height in points.</param>
/// <param name="Matrix">Maps the image's unit square to the page, turned as its orientation says.</param>
internal sealed record ImagePlacement(double PageWidth, double PageHeight, PdfMatrix Matrix)
{
	/// <summary>Gets a value indicating whether the page is wider than tall.</summary>
	public bool IsLandscape
	{
		get
		{
			return this.PageWidth > this.PageHeight;
		}
	}
}
