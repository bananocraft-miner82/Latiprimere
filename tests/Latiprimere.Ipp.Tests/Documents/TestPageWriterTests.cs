using System.Text.RegularExpressions;
using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Mapping;
using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// Latiprimere's test page (ADR 0059): what it says, read back from its content stream, and where its lines are,
/// read back and rendered by PDFium.
/// </summary>
public sealed partial class TestPageWriterTests
{
	private static readonly MediaSize A4 = PwgMediaNames.ToMediaSize("iso_a4_210x297mm")!;          // Known to the table
	private static readonly MediaSize Letter = PwgMediaNames.ToMediaSize("na_letter_8.5x11in")!;    // Known to the table
	private static readonly MediaSize Label = PwgMediaNames.ToMediaSize("na_index-4x6_4x6in")!;     // Known to the table

	[Fact]
	public void GivenAPrinterWhenItsTestPageIsWrittenThenItSaysWhatThePrinterIsAndCanDo()
	{
		// Arrange
		TestPageFacts facts = TestPageWriterTests.CreateFacts(TestPageWriterTests.A4);

		// Act
		using MemoryStream pdf = TestPageWriter.Write(facts);

		// Assert
		List<string> lines = TestPageWriterTests.ReadText(pdf);
		Assert.Equal("Latiprimere test page", lines[0]);
		Assert.Contains("Printer        Office Laser", lines);
		Assert.Contains("ID             ipp:dnssd:Office Laser", lines);
		Assert.Contains("Make and model HP LaserJet", lines);
		Assert.Contains("Location       not reported", lines);
		Assert.Contains("Print system   Network printer over IPP", lines);
		Assert.Contains("Address        ipp://192.168.1.20/ipp/print", lines);
		Assert.Contains("Paper          A4 (210 x 297 mm)", lines);
		Assert.Contains("Printed        2026-11-25 09:30:00 UTC", lines);
		Assert.Contains("Paper sizes    2, by default A4", lines);
		Assert.Contains("Two-sided      one-sided, long edge", lines);
		Assert.Contains("Colour         monochrome, colour", lines);
		Assert.Contains("Quality        not reported", lines);
		Assert.Contains("Trays          Tray 1, Manual feed", lines);
		Assert.Contains("Formats        PDF, JPEG", lines);
		Assert.Contains(lines, line => line.StartsWith("Latiprimere    ", StringComparison.Ordinal));
		Assert.DoesNotContain(lines, line => line.Contains("shrunk", StringComparison.Ordinal));
	}

	[Theory]
	[InlineData("iso_a4_210x297mm", "18 18 559.2756 805.8898 re")]
	[InlineData("na_letter_8.5x11in", "18 18 576 756 re")]
	[InlineData("na_index-4x6_4x6in", "18 18 252 396 re")]
	public void GivenPaperWhenItsTestPageIsWrittenThenTheEdgeLineIsAQuarterInchFromEachEdge(string paperName, string expected)
	{
		// Arrange (provided by InlineData above)
		TestPageFacts facts = TestPageWriterTests.CreateFacts(PwgMediaNames.ToMediaSize(paperName)!);

		// Act
		using MemoryStream pdf = TestPageWriter.Write(facts);

		// Assert — at its true place even when the rest is shrunk
		Assert.Contains(expected, Assert.Single(PdfTextLines.ReadContents(pdf.ToArray())), StringComparison.Ordinal);
	}

	[Theory]
	[InlineData("iso_a4_210x297mm")]
	[InlineData("na_letter_8.5x11in")]
	public void GivenPaperWithRoomWhenItsTestPageIsWrittenThenTheRulersAreTrueToLength(string paperName)
	{
		// Arrange
		TestPageFacts facts = TestPageWriterTests.CreateFacts(PwgMediaNames.ToMediaSize(paperName)!);

		// Act
		using MemoryStream pdf = TestPageWriter.Write(facts);

		// Assert — each ruler's line from the column's left, 54 points in: 100 mm and 4 inches long
		string content = Assert.Single(PdfTextLines.ReadContents(pdf.ToArray()));
		List<double> lengths = TestPageWriterTests.LinePattern()
			.Matches(content)
			.Where(match => match.Groups["x1"].Value == "54" && match.Groups["y1"].Value == match.Groups["y2"].Value)
			.Select(match => double.Parse(match.Groups["x2"].Value, System.Globalization.CultureInfo.InvariantCulture) - 54)
			.ToList();
		Assert.Equal([283.4646, 288], lengths.Select(length => Math.Round(length, 4)));
	}

	[Fact]
	public void GivenPaperTooSmallForThePageWhenItsTestPageIsWrittenThenItIsShrunkInsideTheEdgeLineAndSaysSo()
	{
		// Arrange — 4 × 6 inches, a label
		TestPageFacts facts = TestPageWriterTests.CreateFacts(TestPageWriterTests.Label);

		// Act
		using MemoryStream pdf = TestPageWriter.Write(facts);

		// Assert — every line inside the edge line, at 18 points from each edge of 288 × 432
		IReadOnlyList<(double X, double Y, string Text)> lines = Assert.Single(PdfTextLines.Read(pdf.ToArray()));
		Assert.Contains(lines, line => line.Text.Contains("shrunk to fit", StringComparison.Ordinal));
		Assert.All(lines, line => Assert.InRange(line.X, 18, 270));
		Assert.All(lines, line => Assert.InRange(line.Y, 18, 414));
		Assert.True(TestPageWriter.ChooseScale(288, 432) < 1);
	}

	[Theory]
	[InlineData(595.2756, 841.8898)]
	[InlineData(612, 792)]
	public void GivenA4OrLetterWhenScaleIsChosenThenThePageIsAtItsTrueSize(double width, double height)
	{
		// Arrange (provided by InlineData above)

		// Act
		double scale = TestPageWriter.ChooseScale(width, height);

		// Assert
		Assert.Equal(1, scale);
	}

	[Fact]
	public void GivenA4WhenItsTestPageIsWrittenThenNoTextGoesBelowTheMargin()
	{
		// Arrange
		TestPageFacts facts = TestPageWriterTests.CreateFacts(TestPageWriterTests.A4);

		// Act
		using MemoryStream pdf = TestPageWriter.Write(facts);

		// Assert — the column's height holds everything drawn
		IReadOnlyList<(double X, double Y, string Text)> lines = Assert.Single(PdfTextLines.Read(pdf.ToArray()));
		double bottom = 841.8898 - TestPageWriter.Margin - TestPageWriter.ContentHeight;
		Assert.All(lines, line => Assert.True(line.Y >= bottom, $"{line.Text} at {line.Y}"));
	}

	[Theory]
	[InlineData("Drucker ✓ Büro", "Drucker ? Büro")]
	[InlineData("Smile \U0001F600!", "Smile ?!")]
	[InlineData("Tab\there", "Tab?here")]
	public void GivenTextCourierCannotShowWhenMadeDrawableThenEachSuchCharacterIsAQuestionMark(string text, string expected)
	{
		// Arrange (provided by InlineData above)

		// Act
		string drawable = TestPageWriter.ToDrawable(text, 80);

		// Assert
		Assert.Equal(expected, drawable);
	}

	[Fact]
	public void GivenTextLongerThanFitsWhenMadeDrawableThenItIsCutShortWithAnEllipsis()
	{
		// Arrange
		string text = new string('x', 20);

		// Act
		string drawable = TestPageWriter.ToDrawable(text, 10);

		// Assert
		Assert.Equal("xxxxxxx...", drawable);
	}

	[Fact]
	public void GivenAPrinterNameCourierCannotShowWhenItsTestPageIsWrittenThenItIsStillWritten()
	{
		// Arrange
		TestPageFacts facts = TestPageWriterTests.CreateFacts(TestPageWriterTests.A4) with
		{
			Printer = new PrinterInfo { Id = "cups:プリンタ", Name = "プリンタ" }
		};

		// Act
		using MemoryStream pdf = TestPageWriter.Write(facts);

		// Assert
		Assert.Contains("Printer        ????", TestPageWriterTests.ReadText(pdf));
	}

	[Fact]
	public void GivenA4WhenItsTestPageIsRenderedThenTheEdgeLinePrintsAndNothingIsOutsideIt()
	{
		// Arrange — rendered at 144 dpi, two pixels a point, so the edge line is 36 pixels in
		TestPageFacts facts = TestPageWriterTests.CreateFacts(TestPageWriterTests.A4);
		using MemoryStream pdf = TestPageWriter.Write(facts);
		using IRasterDocument document = new PdfiumRasterizer().Open(pdf);
		RasterBand band = RasterBand.Create(1191, 1684, 0, 1684);
		byte[] pixels = new byte[band.ByteCount];

		// Act
		document.RenderBand(0, band, pixels);

		// Assert — ink at the left edge line, none between it and the paper's edge, and the black patch black
		RenderedPage page = new RenderedPage(pixels, band.Stride);
		Assert.True(TestPageWriterTests.CountInk(page, 34, 600, 5, 400) > 0);
		Assert.Equal(0, TestPageWriterTests.CountInk(page, 0, 0, 32, 1684));
		Assert.Equal(0, TestPageWriterTests.CountInk(page, 0, 0, 1191, 32));
		Assert.True(page.CountDark(36, 36, 1119, 1612) > 1000);
	}

	/// <summary>
	/// Makes the facts of a network printer, on the paper given.
	/// </summary>
	/// <param name="paper">The paper.</param>
	/// <returns>The facts.</returns>
	private static TestPageFacts CreateFacts(MediaSize paper)
	{
		return new TestPageFacts
		{
			Printer      = new PrinterInfo { Id = "ipp:dnssd:Office Laser", Name = "Office Laser", MakeAndModel = "HP LaserJet" },
			PrintSystem  = "Network printer over IPP",
			Address      = "ipp://192.168.1.20/ipp/print",
			Paper        = paper,
			PrintedAt    = new DateTimeOffset(2026, 11, 25, 10, 30, 0, TimeSpan.FromHours(1)),
			Capabilities = new PrinterCapabilities
			{
				MediaSizes       = [TestPageWriterTests.A4, TestPageWriterTests.Letter],
				DefaultMediaSize = TestPageWriterTests.A4,
				DuplexModes      = [DuplexMode.OneSided, DuplexMode.LongEdge],
				ColorModes       = [ColorMode.Monochrome, ColorMode.Color],
				InputTrays       = [new InputTray { Id = "tray-1", Name = "Tray 1" }, new InputTray { Id = "manual", Name = "Manual feed" }],
				DocumentFormats  = [DocumentFormat.Pdf, DocumentFormat.Jpeg]
			}
		};
	}

	/// <summary>
	/// Reads the page's lines of text, in the order they were drawn.
	/// </summary>
	/// <param name="pdf">The page.</param>
	/// <returns>The lines.</returns>
	private static List<string> ReadText(MemoryStream pdf)
	{
		return Assert.Single(PdfTextLines.Read(pdf.ToArray())).Select(line => line.Text).ToList();
	}

	/// <summary>
	/// Counts the pixels in a rectangle that aren't white, or nearly.
	/// </summary>
	/// <param name="page">The rendered page.</param>
	/// <param name="left">The rectangle's left column.</param>
	/// <param name="top">Its top row.</param>
	/// <param name="width">Its width.</param>
	/// <param name="height">Its height.</param>
	/// <returns>How many have a component below 230.</returns>
	private static int CountInk(RenderedPage page, int left, int top, int width, int height)
	{
		int count = 0;

		for (int y = top; y < top + height; y++)
		{
			for (int x = left; x < left + width; x++)
			{
				(int r, int g, int b) = page.GetRgb(x, y);
				count += r < 230 || g < 230 || b < 230 ? 1 : 0;
			}
		}

		return count;
	}

	/// <summary>
	/// Matches a line as the writer draws it.
	/// </summary>
	/// <returns>The pattern.</returns>
	[GeneratedRegex(@"w 0 G (?<x1>[\d.]+) (?<y1>[\d.]+) m (?<x2>[\d.]+) (?<y2>[\d.]+) l S", RegexOptions.CultureInvariant)]
	private static partial Regex LinePattern();
}
