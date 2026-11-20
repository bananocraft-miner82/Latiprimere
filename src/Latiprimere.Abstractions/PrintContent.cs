using System.Text;
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
	/// <param name="textEncoding">The encoding of text, or <see langword="null"/> for other formats.</param>
	private PrintContent(Stream content, DocumentFormat format, Encoding? textEncoding)
	{
		this.Content = content;
		this.Format = format;
		this.TextEncoding = textEncoding;
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
	/// Gets the encoding of a text document's characters, or <see langword="null"/> for other formats.
	/// </summary>
	public Encoding? TextEncoding { get; }

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
	/// Creates print content from a stream containing a JPEG image, printed on one page.
	/// </summary>
	/// <param name="content">A readable stream containing the JPEG image.</param>
	/// <returns>The print content.</returns>
	/// <remarks>
	/// The image is sized on the paper as <see cref="PrintOptions.Scaling"/> says, turned as its EXIF orientation
	/// says, and printed landscape when it is wider than tall, unless <see cref="PrintOptions.Orientation"/> says
	/// otherwise. Baseline and progressive JPEGs in grey, colour or CMYK can be printed.
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="content"/> cannot be read.</exception>
	public static PrintContent FromJpeg(Stream content)
	{
		// Validate and wrap the JPEG stream
		return PrintContent.Create(content, DocumentFormat.Jpeg);
	}

	/// <summary>
	/// Creates print content from a stream containing a PNG image, printed on one page.
	/// </summary>
	/// <param name="content">A readable stream containing the PNG image.</param>
	/// <returns>The print content.</returns>
	/// <remarks>
	/// The image is sized on the paper as <see cref="PrintOptions.Scaling"/> says, turned as its EXIF orientation
	/// says, and printed landscape when it is wider than tall, unless <see cref="PrintOptions.Orientation"/> says
	/// otherwise. Every PNG can be printed, with any transparency over white. An animated PNG prints its first frame.
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="content"/> cannot be read.</exception>
	public static PrintContent FromPng(Stream content)
	{
		// Validate and wrap the PNG stream
		return PrintContent.Create(content, DocumentFormat.Png);
	}

	/// <summary>
	/// Creates print content from plain text.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The print content, holding the text as UTF-8.</returns>
	/// <remarks>
	/// <para>
	/// Text is laid out in Courier at 10 points, within half-inch margins: long lines wrap at a space where they can,
	/// tabs stop every 8 columns, and a form feed starts a new page. <see cref="PrintOptions.Orientation"/> chooses
	/// portrait or landscape pages. CUPS lays text out itself.
	/// </para>
	/// <para>
	/// Courier shows the Windows-1252 characters, which cover Western European languages. Elsewhere than CUPS, text
	/// with any other character, or a control character other than tab, line breaks and form feed, is refused by
	/// <see cref="IPrinter.SubmitAsync"/> with <see cref="NotSupportedException"/> naming it.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
	public static PrintContent FromText(string text)
	{
		ArgumentNullException.ThrowIfNull(text);

		UTF8Encoding utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		return new PrintContent(new MemoryStream(utf8.GetBytes(text), writable: false), DocumentFormat.Text, utf8);
	}

	/// <summary>
	/// Creates print content from a stream containing plain text.
	/// </summary>
	/// <param name="content">A readable stream containing the text.</param>
	/// <param name="encoding">The text's encoding, such as <see cref="Encoding.UTF8"/>. A byte order mark at the start
	/// of the stream overrides it.</param>
	/// <returns>The print content.</returns>
	/// <remarks>The text is laid out as <see cref="FromText(string)"/> describes.</remarks>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="encoding"/> is
	/// <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="content"/> cannot be read.</exception>
	public static PrintContent FromText(Stream content, Encoding encoding)
	{
		ArgumentNullException.ThrowIfNull(encoding);

		PrintContent.Validate(content);

		return new PrintContent(content, DocumentFormat.Text, encoding);
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
		PrintContent.Validate(content);

		return new PrintContent(content, format, textEncoding: null);
	}

	/// <summary>
	/// Checks a stream exists and can be read, as the print system must read it.
	/// </summary>
	/// <param name="content">The stream.</param>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException"><paramref name="content"/> cannot be read.</exception>
	private static void Validate(Stream content)
	{
		ArgumentNullException.ThrowIfNull(content);

		if (!content.CanRead)
		{
			throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(content));
		}
	}
}
