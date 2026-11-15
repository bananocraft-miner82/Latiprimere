using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// Represents a document to be printed: its data and its format.
/// </summary>
/// <remarks>
/// <para>
/// The caller owns the stream and remains responsible for disposing it once the job has been submitted.
/// </para>
/// <para>
/// The document is read from the stream's current position to its end. Prefer a seekable stream, such as a file or
/// memory stream: a network printer busy with another job turns the job away, and only a seekable stream can be sent
/// again from where it started. A job from a stream that can't seek fails on the first busy answer.
/// </para>
/// </remarks>
public sealed class PrintContent
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PrintContent"/> class.
	/// </summary>
	/// <param name="content">The readable stream containing the document data.</param>
	/// <param name="format">The format of the document data.</param>
	private PrintContent(Stream content, DocumentFormat format)
	{
		this.Content = content;
		this.Format = format;
	}

	/// <summary>
	/// Gets the stream containing the document data.
	/// </summary>
	public Stream Content { get; }

	/// <summary>
	/// Gets the format of the document data.
	/// </summary>
	public DocumentFormat Format { get; }

	/// <summary>
	/// Creates print content from a stream containing a PDF document.
	/// </summary>
	/// <param name="content">A readable stream containing the PDF document.</param>
	/// <returns>The print content.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="content"/> cannot be read.</exception>
	public static PrintContent FromPdf(Stream content)
	{
		// Validate and wrap the PDF stream
		return PrintContent.Create(content, DocumentFormat.Pdf);
	}

	/// <summary>
	/// Creates print content from a stream containing raw printer-language data, such as ZPL, ESC/POS or PCL.
	/// </summary>
	/// <param name="content">A readable stream containing the raw printer data.</param>
	/// <returns>The print content.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="content"/> cannot be read.</exception>
	public static PrintContent FromRaw(Stream content)
	{
		// Validate and wrap the raw data stream
		return PrintContent.Create(content, DocumentFormat.Raw);
	}

	/// <summary>
	/// Validates the stream and creates print content in the given format.
	/// </summary>
	/// <param name="content">The stream containing the document data.</param>
	/// <param name="format">The format of the document data.</param>
	/// <returns>The print content.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="content"/> cannot be read.</exception>
	private static PrintContent Create(Stream content, DocumentFormat format)
	{
		// The stream must exist and be readable for the print system to consume it
		ArgumentNullException.ThrowIfNull(content);

		if (!content.CanRead)
		{
			throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(content));
		}

		return new PrintContent(content, format);
	}
}
