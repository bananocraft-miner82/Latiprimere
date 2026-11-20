namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Converts a JPEG or PNG into a one-page PDF on the job's paper, for print systems and printers that can't take the
/// image itself (ADR 0047). A JPEG goes into the PDF as it is; a PNG is decoded, unless its data can go in as it is.
/// </summary>
internal static class ImageDocumentConverter
{
	/// <summary>
	/// Checks whether a document format is an image this converter reads.
	/// </summary>
	/// <param name="format">The format.</param>
	/// <returns>Whether it is a JPEG or PNG.</returns>
	public static bool IsImage(DocumentFormat format)
	{
		return format is DocumentFormat.Jpeg or DocumentFormat.Png;
	}

	/// <summary>
	/// Reads an image and writes it as a PDF.
	/// </summary>
	/// <param name="content">The image, read from its stream's position to its end.</param>
	/// <param name="paper">The paper to place it on.</param>
	/// <param name="options">The job's options: its orientation and scaling.</param>
	/// <returns>The PDF, from its start, and where the image went on it. The caller disposes the stream.</returns>
	/// <exception cref="ArgumentException">The content isn't a JPEG or PNG.</exception>
	/// <exception cref="PrintException">The image can't be read, or is too large.</exception>
	public static (MemoryStream Pdf, ImagePlacement Placement) ToPdf(PrintContent content, MediaSize paper, PrintOptions options)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(paper);
		ArgumentNullException.ThrowIfNull(options);

		if (!ImageDocumentConverter.IsImage(content.Format))
		{
			throw new ArgumentException(null, nameof(content));
		}

		byte[] data = ImageDocumentConverter.ReadAll(content.Content);
		PageImage image = content.Format == DocumentFormat.Jpeg ? JpegReader.Read(data) : PngDecoder.Read(data);
		ImagePlacement placement = ImagePageLayout.Place(image, paper, options.Orientation, options.Scaling);
		MemoryStream pdf = new MemoryStream();

		try
		{
			using (PdfDocumentWriter writer = new PdfDocumentWriter(pdf))
			{
				writer.BeginPage(placement.PageWidth, placement.PageHeight);
				writer.DrawImage(image.Image, placement.Matrix);
				writer.EndPage();
				writer.Complete();
			}

			pdf.Position = 0;

			return (pdf, placement);
		}
		catch
		{
			pdf.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Reads a stream to its end into an array of its size: straight into it when the stream can say how long it is,
	/// otherwise through a buffer.
	/// </summary>
	/// <param name="stream">The stream, from its position.</param>
	/// <returns>The bytes.</returns>
	/// <exception cref="PrintException">The stream is longer than an array can hold.</exception>
	private static byte[] ReadAll(Stream stream)
	{
		if (stream.CanSeek)
		{
			long remaining = Math.Max(0, stream.Length - stream.Position);

			if (remaining > Array.MaxLength)
			{
				throw new PrintException(Resources.ExceptionMessages.FormatImageFileTooLarge(remaining));
			}

			byte[] data = new byte[remaining];
			stream.ReadExactly(data);

			return data;
		}

		using MemoryStream copy = new MemoryStream();
		stream.CopyTo(copy);

		return copy.ToArray();
	}
}
