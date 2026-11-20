namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Turns the documents Latiprimere lays out itself, images and text, into a PDF on the job's paper, for printers and
/// print systems that can't take them as they are (ADR 0047, ADR 0048).
/// </summary>
internal static class DocumentConverter
{
	/// <summary>
	/// Checks whether a document format is one this converter turns into a PDF.
	/// </summary>
	/// <param name="format">The format.</param>
	/// <returns>Whether it is a JPEG, a PNG or text.</returns>
	public static bool CanConvert(DocumentFormat format)
	{
		return ImageDocumentConverter.IsImage(format) || format == DocumentFormat.Text;
	}

	/// <summary>
	/// Turns an image or text into a PDF.
	/// </summary>
	/// <param name="content">The document.</param>
	/// <param name="paper">The paper.</param>
	/// <param name="options">The job's options.</param>
	/// <returns>The PDF, from its start, and whether its page or pages are landscape. The caller disposes the
	/// stream.</returns>
	/// <exception cref="ArgumentException">The content isn't an image or text.</exception>
	/// <exception cref="PrintException">An image can't be read.</exception>
	/// <exception cref="NotSupportedException">Text has a character that can't be shown.</exception>
	public static (MemoryStream Pdf, bool IsLandscape) ToPdf(PrintContent content, MediaSize paper, PrintOptions options)
	{
		ArgumentNullException.ThrowIfNull(content);

		if (content.Format == DocumentFormat.Text)
		{
			return TextDocumentConverter.ToPdf(content, paper, options);
		}

		(MemoryStream pdf, ImagePlacement placement) = ImageDocumentConverter.ToPdf(content, paper, options);

		return (pdf, placement.IsLandscape);
	}
}
