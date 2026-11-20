using System.Text;
using Latiprimere.Ipp.Documents;
using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// Text laid out as a PDF: lines, wrapping, tabs, form feeds and pages, read back from the PDF, and drawn where
/// PDFium shows them.
/// </summary>
public sealed class TextDocumentConverterTests
{
	private static readonly MediaSize A4 = new MediaSize { Name = "iso_a4_210x297mm", DisplayName = "A4", WidthMillimeters = 210, HeightMillimeters = 297 };
	private static readonly MediaSize Letter = new MediaSize { Name = "na_letter_8.5x11in", DisplayName = "Letter", WidthMillimeters = 215.9, HeightMillimeters = 279.4 };

	[Theory]
	[InlineData("A4", PageOrientation.PrinterDefault, 87, 64)]
	[InlineData("Letter", PageOrientation.Portrait, 90, 60)]
	[InlineData("A4", PageOrientation.Landscape, 128, 43)]
	public void GivenPaperAndOrientationWhenLaidOutThenColumnsAndRowsFitWithinHalfInchMargins(string paper, PageOrientation orientation, int columns, int rows)
	{
		// Arrange — a line of one more character than fits, and one more line than fits, of numbered lines (provided
		// by InlineData above)
		string text = new string('x', columns + 1) + "\n" + string.Join("\n", Enumerable.Range(2, rows).Select(number => number.ToString(System.Globalization.CultureInfo.InvariantCulture)));

		// Act
		IReadOnlyList<IReadOnlyList<(double X, double Y, string Text)>> pages = TextDocumentConverterTests.Lay(text, paper == "A4" ? TextDocumentConverterTests.A4 : TextDocumentConverterTests.Letter, orientation);

		// Assert — the long line breaks after the columns, taking two rows, so the last two lines spill onto a second
		// page
		Assert.Equal(2, pages.Count);
		Assert.Equal(new string('x', columns), pages[0][0].Text);
		Assert.Equal("x", pages[0][1].Text);
		Assert.Equal(rows, pages[0].Count);
		Assert.Equal([rows, rows + 1], pages[1].Select(line => int.Parse(line.Text, System.Globalization.CultureInfo.InvariantCulture)));
		Assert.All(pages.SelectMany(page => page), line => Assert.Equal(36, line.X));
	}

	[Fact]
	public void GivenLinesWhenLaidOutThenEachIsTwelvePointsBelowTheLastFromTheTopMargin()
	{
		// Arrange
		string text = "one\ntwo\n\nfour";

		// Act
		IReadOnlyList<(double X, double Y, string Text)> page = Assert.Single(TextDocumentConverterTests.Lay(text, TextDocumentConverterTests.A4));

		// Assert — the blank line takes its row; A4's top margin is 841.89 - 36, less the font size for the baseline
		Assert.Equal(["one", "two", "four"], page.Select(line => line.Text));
		Assert.Equal([795.8898, 783.8898, 759.8898], page.Select(line => Math.Round(line.Y, 4)));
	}

	[Fact]
	public void GivenALongLineWithSpacesWhenLaidOutThenItWrapsAtTheLastSpaceThatFits()
	{
		// Arrange — 20 words of 5 characters and a space: 14 fit in 87 columns
		string text = string.Concat(Enumerable.Repeat("word ", 20)).TrimEnd();

		// Act
		IReadOnlyList<(double X, double Y, string Text)> page = Assert.Single(TextDocumentConverterTests.Lay(text, TextDocumentConverterTests.A4));

		// Assert — no word is split, and the space at the break isn't carried over
		Assert.Equal(2, page.Count);
		Assert.Equal(string.Join(' ', Enumerable.Repeat("word", 17)), page[0].Text);
		Assert.Equal(string.Join(' ', Enumerable.Repeat("word", 3)), page[1].Text);
	}

	[Theory]
	[InlineData("a\tb", "a       b")]
	[InlineData("abcdefgh\tc", "abcdefgh        c")]
	[InlineData("\t\tx", "                x")]
	[InlineData("abc\t", "abc")]
	public void GivenTabsWhenLaidOutThenTheyStopEveryEightColumns(string text, string expected)
	{
		// Arrange (provided by InlineData above)

		// Act
		IReadOnlyList<(double X, double Y, string Text)> page = Assert.Single(TextDocumentConverterTests.Lay(text, TextDocumentConverterTests.A4));

		// Assert
		Assert.Equal(expected, Assert.Single(page).Text);
	}

	[Fact]
	public void GivenEachKindOfLineEndWhenLaidOutThenEachEndsOneLine()
	{
		// Arrange — Windows, old Mac and Unix line ends, and a final line end that adds no line
		string text = "a\r\nb\rc\nd\n";

		// Act
		IReadOnlyList<(double X, double Y, string Text)> page = Assert.Single(TextDocumentConverterTests.Lay(text, TextDocumentConverterTests.A4));

		// Assert — rows 0 to 3
		Assert.Equal(["a", "b", "c", "d"], page.Select(line => line.Text));
		Assert.Equal(795.8898 - 36, Math.Round(page[3].Y, 4), 4);
	}

	[Theory]
	[InlineData("a\fb", new[] { "a", "b" })]
	[InlineData("a\f", new[] { "a" })]
	[InlineData("\fa", new[] { "", "a" })]
	[InlineData("a\f\fb", new[] { "a", "", "b" })]
	[InlineData("a\nb\fc", new[] { "a b", "c" })]
	[InlineData("", new[] { "" })]
	public void GivenFormFeedsWhenLaidOutThenEachStartsAPageAndATrailingOneAddsNone(string text, string[] expected)
	{
		// Arrange — each page's lines joined by spaces (provided by InlineData above)

		// Act
		IReadOnlyList<IReadOnlyList<(double X, double Y, string Text)>> pages = TextDocumentConverterTests.Lay(text, TextDocumentConverterTests.A4);

		// Assert
		Assert.Equal(expected, pages.Select(page => string.Join(' ', page.Select(line => line.Text))));
	}

	[Fact]
	public void GivenAFormFeedRightAfterAPageFillsWhenLaidOutThenNoBlankPageIsLeft()
	{
		// Arrange — exactly a page of 64 lines, a form feed, then one more
		string text = string.Concat(Enumerable.Repeat("line\n", 64)) + "\fnext";

		// Act
		IReadOnlyList<IReadOnlyList<(double X, double Y, string Text)>> pages = TextDocumentConverterTests.Lay(text, TextDocumentConverterTests.A4);

		// Assert
		Assert.Equal([64, 1], pages.Select(page => page.Count));
		Assert.Equal("next", pages[1][0].Text);
	}

	[Theory]
	[InlineData("abc\n中", "U+4E2D", "line 2, column 1")]
	[InlineData("ok 😀", "U+1F600", "line 1, column 4")]
	[InlineData("bell\u0007", "U+0007", "line 1, column 5")]
	public void GivenACharacterCourierCantShowWhenLaidOutThenThrowsNotSupportedExceptionNamingIt(string text, string code, string position)
	{
		// Arrange — CJK, an emoji's surrogate pair, and a control character (provided by InlineData above)

		// Act
		Action lay = () => TextDocumentConverterTests.Lay(text, TextDocumentConverterTests.A4);

		// Assert
		NotSupportedException exception = Assert.ThrowsAny<NotSupportedException>(lay);
		Assert.Contains(code, exception.Message, StringComparison.Ordinal);
		Assert.Contains(position, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenWindows1252CharactersWhenLaidOutThenTheyAreDrawnInItsEncoding()
	{
		// Arrange — é in Latin-1, € and curly quotes among the characters Windows-1252 adds
		string text = "café €5 “ok”";

		// Act
		IReadOnlyList<(double X, double Y, string Text)> page = Assert.Single(TextDocumentConverterTests.Lay(text, TextDocumentConverterTests.A4));

		// Assert — their Windows-1252 bytes: E9, 80, 93 and 94
		Assert.Equal("café \u00805 \u0093ok\u0094", Assert.Single(page).Text);
	}

	[Fact]
	public void GivenAStreamInAnotherEncodingOrWithAByteOrderMarkWhenLaidOutThenItIsReadAsSuch()
	{
		// Arrange — Latin-1 bytes, and UTF-16 with its byte order mark though UTF-8 is named
		PrintContent latin1 = PrintContent.FromText(new MemoryStream(Encoding.Latin1.GetBytes("café")), Encoding.Latin1);
		PrintContent utf16 = PrintContent.FromText(new MemoryStream([.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("naïve")]), Encoding.UTF8);

		// Act
		string fromLatin1 = TextDocumentConverterTests.Lay(latin1, TextDocumentConverterTests.A4, PageOrientation.PrinterDefault)[0][0].Text;
		string fromUtf16 = TextDocumentConverterTests.Lay(utf16, TextDocumentConverterTests.A4, PageOrientation.PrinterDefault)[0][0].Text;

		// Assert
		Assert.Equal(("café", "naïve"), (fromLatin1, fromUtf16));
	}

	[Fact]
	public void GivenTextWhenLaidOutThenPdfiumDrawsItInTheTopLeftAndNothingElsewhere()
	{
		// Arrange — a full line of capitals across the top
		string text = new string('M', 87);
		(MemoryStream pdf, bool landscape) = TextDocumentConverter.ToPdf(PrintContent.FromText(text), TextDocumentConverterTests.A4, PrintOptions.Default);

		// Act — rendered at 72 dpi, a pixel a point
		using (pdf)
		{
			using IRasterDocument document = new PdfiumRasterizer().Open(pdf);
			RasterBand band = RasterBand.Create(595, 842, 0, 842);
			byte[] pixels = new byte[band.ByteCount];
			document.RenderBand(0, band, pixels);
			RenderedPage page = new RenderedPage(pixels, band.Stride);

			// Assert — the line runs from the left margin, 36 points, to 36 + 87 × 6 = 558 points, its glyphs
			// between 36 and about 48 points down
			Assert.False(landscape);
			Assert.Equal(1, document.PageCount);
			Assert.True(page.CountDark(36, 36, 30, 12) > 20);
			Assert.True(page.CountDark(528, 36, 30, 12) > 20);
			Assert.Equal(0, page.CountDark(0, 0, 595, 30));
			Assert.Equal(0, page.CountDark(0, 60, 595, 782));
			Assert.Equal(0, page.CountDark(565, 0, 30, 842));
		}
	}

	[Fact]
	public void GivenAnImageWhenLaidOutAsTextThenThrowsArgumentException()
	{
		// Arrange
		PrintContent content = PrintContent.FromPng(new MemoryStream());

		// Act
		Action lay = () => TextDocumentConverter.ToPdf(content, TextDocumentConverterTests.A4, PrintOptions.Default);

		// Assert
		Assert.Throws<ArgumentException>(lay);
	}

	/// <summary>
	/// Lays text out and reads back each page's lines.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="paper">The paper.</param>
	/// <param name="orientation">The job's orientation.</param>
	/// <returns>Each page's lines.</returns>
	private static IReadOnlyList<IReadOnlyList<(double X, double Y, string Text)>> Lay(string text, MediaSize paper, PageOrientation orientation = PageOrientation.PrinterDefault)
	{
		return TextDocumentConverterTests.Lay(PrintContent.FromText(text), paper, orientation);
	}

	/// <summary>
	/// Lays text content out and reads back each page's lines.
	/// </summary>
	/// <param name="content">The text.</param>
	/// <param name="paper">The paper.</param>
	/// <param name="orientation">The job's orientation.</param>
	/// <returns>Each page's lines.</returns>
	private static IReadOnlyList<IReadOnlyList<(double X, double Y, string Text)>> Lay(PrintContent content, MediaSize paper, PageOrientation orientation)
	{
		(MemoryStream pdf, bool landscape) = TextDocumentConverter.ToPdf(content, paper, new PrintOptions { Orientation = orientation });

		using (pdf)
		{
			Assert.Equal(orientation == PageOrientation.Landscape, landscape);

			return PdfTextLines.Read(pdf.ToArray());
		}
	}
}
