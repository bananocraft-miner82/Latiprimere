using System.Globalization;
using System.Text;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Lays plain text out as a PDF on the job's paper, for printers and print systems that don't lay text out
/// themselves (ADR 0048): Courier at 10 points within half-inch margins, long lines wrapped at a space where they
/// can be, tabs every 8 columns, and a form feed starting a new page.
/// </summary>
internal static class TextDocumentConverter
{
	private const int TabWidth = 8;

	/// <summary>
	/// Reads text and writes it as a PDF.
	/// </summary>
	/// <param name="content">The text, read from its stream's position to its end in its encoding; a byte order mark
	/// overrides the encoding.</param>
	/// <param name="paper">The paper.</param>
	/// <param name="options">The job's options: <see cref="PageOrientation.Landscape"/> turns the pages.</param>
	/// <returns>The PDF, from its start, and whether its pages are landscape. The caller disposes the stream.</returns>
	/// <exception cref="ArgumentException">The content isn't text.</exception>
	/// <exception cref="NotSupportedException">A character isn't one Courier shows without an embedded font, or is a
	/// control character other than tab, line breaks and form feed.</exception>
	public static (MemoryStream Pdf, bool IsLandscape) ToPdf(PrintContent content, MediaSize paper, PrintOptions options)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(paper);
		ArgumentNullException.ThrowIfNull(options);

		if (content.Format != DocumentFormat.Text)
		{
			throw new ArgumentException(null, nameof(content));
		}

		double shortSide = Math.Min(paper.WidthMillimeters, paper.HeightMillimeters) * 72 / 25.4;
		double longSide = Math.Max(paper.WidthMillimeters, paper.HeightMillimeters) * 72 / 25.4;
		bool landscape = options.Orientation == PageOrientation.Landscape;
		MemoryStream pdf = new MemoryStream();

		try
		{
			using (PdfDocumentWriter writer = new PdfDocumentWriter(pdf))
			{
				TextPageWriter pages = new TextPageWriter(writer, landscape ? longSide : shortSide, landscape ? shortSide : longSide);

				using (StreamReader reader = new StreamReader(content.Content, content.TextEncoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
				{
					TextDocumentConverter.Lay(reader, pages);
				}

				pages.Finish();
				writer.Complete();
			}

			pdf.Position = 0;

			return (pdf, landscape);
		}
		catch
		{
			pdf.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Reads the text a character at a time and sets it in lines: breaking at line ends, expanding tabs, wrapping
	/// long lines, and starting pages at form feeds.
	/// </summary>
	/// <param name="reader">The text.</param>
	/// <param name="pages">Where the lines go.</param>
	/// <exception cref="NotSupportedException">A character can't be shown.</exception>
	private static void Lay(TextReader reader, TextPageWriter pages)
	{
		StringBuilder line = new StringBuilder();
		int lineNumber = 1;
		int column = 0;
		int read;

		while ((read = reader.Read()) >= 0)
		{
			char character = (char)read;
			column++;

			switch (character)
			{
				case '\r':
				case '\n':
					// \r\n is one line end
					if (character == '\r' && reader.Peek() == '\n')
					{
						reader.Read();
					}

					pages.WriteLine(line.ToString());
					line.Clear();
					lineNumber++;
					column = 0;
					break;

				case '\f':
					if (line.Length > 0)
					{
						pages.WriteLine(line.ToString());
						line.Clear();
					}

					pages.FormFeed();
					break;

				case '\t':
					line.Append(' ', TextDocumentConverter.TabWidth - (line.Length % TextDocumentConverter.TabWidth));
					break;

				default:
					if (!PdfWinAnsiEncoding.TryEncode(character, out _))
					{
						throw TextDocumentConverter.CreateUnsupported(character, reader, lineNumber, column);
					}

					line.Append(character);
					break;
			}

			// A tab can carry a line past the edge as well as a character can
			while (line.Length > pages.Columns)
			{
				TextDocumentConverter.Wrap(line, pages);
			}
		}

		if (line.Length > 0)
		{
			pages.WriteLine(line.ToString());
		}
	}

	/// <summary>
	/// Sets the start of an over-long line, up to its last space that fits, or as much as fits if it has none, and
	/// keeps the rest for the next line.
	/// </summary>
	/// <param name="line">The line; what remains of it on return.</param>
	/// <param name="pages">Where the start goes.</param>
	private static void Wrap(StringBuilder line, TextPageWriter pages)
	{
		string text = line.ToString();
		int space = text.LastIndexOf(' ', pages.Columns);

		// A space at the start would leave nothing on the line, so the line breaks where it must instead
		if (space > 0)
		{
			pages.WriteLine(text[..space]);
			line.Clear().Append(text.AsSpan(space + 1));
		}
		else
		{
			pages.WriteLine(text[..pages.Columns]);
			line.Clear().Append(text.AsSpan(pages.Columns));
		}
	}

	/// <summary>
	/// Creates the refusal of a character, naming its code point, line and column.
	/// </summary>
	/// <param name="character">The character, or the first half of a surrogate pair.</param>
	/// <param name="reader">The text, to read the pair's second half from.</param>
	/// <param name="lineNumber">Its line, from 1.</param>
	/// <param name="column">Its column, from 1, counting characters as they are in the text.</param>
	/// <returns>The exception.</returns>
	private static TextCharacterNotSupportedException CreateUnsupported(char character, TextReader reader, int lineNumber, int column)
	{
		int codePoint = character;

		if (char.IsHighSurrogate(character) && reader.Peek() is int next and >= 0 && char.IsLowSurrogate((char)next))
		{
			codePoint = char.ConvertToUtf32(character, (char)reader.Read());
		}

		string code = "U+" + codePoint.ToString("X4", CultureInfo.InvariantCulture);

		return new TextCharacterNotSupportedException(ExceptionMessages.FormatTextCharacterUnsupported(code, lineNumber, column));
	}
}
