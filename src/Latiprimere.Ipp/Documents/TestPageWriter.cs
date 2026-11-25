using System.Globalization;
using System.Reflection;
using System.Text;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Draws Latiprimere's test page as a one-page PDF (ADR 0059): what the printer is and what it says it can do, a line
/// a quarter of an inch in from each edge, rulers to measure, and colour and grey patches.
/// </summary>
/// <remarks>
/// The page is laid out in a column <see cref="ContentWidth"/> wide and <see cref="ContentHeight"/> tall, at its true
/// size when the paper has room for it inside <see cref="Margin"/>, and shrunk to fit otherwise. The edge line is
/// always at its true place, as it is about the paper, not the layout.
/// </remarks>
internal sealed class TestPageWriter
{
	/// <summary>How far the edge line is from each edge, in points: a quarter of an inch.</summary>
	internal const double EdgeInset = 18;

	/// <summary>The width of the column the page is laid out in, in points: six and a half inches.</summary>
	internal const double ContentWidth = 468;

	/// <summary>The height of the column, in points.</summary>
	internal const double ContentHeight = 484;

	/// <summary>The space round the column when the paper has room for it, in points: three quarters of an inch.</summary>
	internal const double Margin = 54;

	/// <summary>The space round the column on paper too small for it, in points: inside the edge line.</summary>
	internal const double SmallPaperMargin = 27;

	/// <summary>The length of the metric ruler, in millimetres.</summary>
	internal const double RulerMillimetres = 100;

	/// <summary>The length of the imperial ruler, in inches.</summary>
	internal const double RulerInches = 4;

	/// <summary>The job's name, unless the options give one.</summary>
	internal const string JobName = "Latiprimere test page";

	private const double PointsPerInch = 72;           // PDF's unit
	private const double MillimetresPerInch = 25.4;
	private const double TitleSize = 16;              // The title's size in points
	private const double BodySize = 9;                // Every other line's
	private const double CourierAdvance = 0.6;        // Courier's character width, as a part of its size
	private const double LineHeight = 12;             // From one line to the next
	private const double SectionGap = 12;             // Between sections
	private const double TitleHeight = 28;            // The title's line and the space below it
	private const int LabelColumns = 15;              // A fact's label, padded
	private const double PatchSize = 36;              // A colour patch's side
	private const double PatchGap = 6;                // Between colour patches
	private const double PatchBlock = 54;             // A row of patches, its labels and the space below
	private const double RulerBlock = 30;             // A ruler, its labels and the space below
	private const double LongTick = 8;                // A ruler's tick at every centimetre or inch
	private const double ShortTick = 4;               // And between
	private const double ThinLine = 0.5;              // Rulers and outlines
	private const string NotReported = "not reported";
	private const string Ellipsis = "...";

	// Cyan, magenta, yellow, black, red, green and blue, as their letters and RGB components
	private static readonly (string Label, double Red, double Green, double Blue)[] Patches =
	[
		("C", 0, 1, 1),
		("M", 1, 0, 1),
		("Y", 1, 1, 0),
		("K", 0, 0, 0),
		("R", 1, 0, 0),
		("G", 0, 1, 0),
		("B", 0, 0, 1),
	];

	private readonly PdfDocumentWriter pdf; // The document
	private readonly double left;           // The column's left on the page, in points
	private readonly double top;            // Its top
	private readonly double scale;          // 1, or less on paper too small for the column
	private double cursor;                  // How far down the column the next line goes, unscaled

	/// <summary>
	/// Initialises a new instance of the <see cref="TestPageWriter"/> class, placing the column on the paper.
	/// </summary>
	/// <param name="pdf">The document, with the page started.</param>
	/// <param name="pageWidth">The page's width in points.</param>
	/// <param name="pageHeight">Its height in points.</param>
	private TestPageWriter(PdfDocumentWriter pdf, double pageWidth, double pageHeight)
	{
		this.pdf = pdf;

		double scale = TestPageWriter.ChooseScale(pageWidth, pageHeight);
		double margin = scale < 1 ? TestPageWriter.SmallPaperMargin : TestPageWriter.Margin;

		this.scale = scale;
		this.left = margin;
		this.top = pageHeight - margin;
	}

	/// <summary>
	/// Draws the test page.
	/// </summary>
	/// <param name="facts">What the page says.</param>
	/// <returns>The PDF, positioned at its start.</returns>
	internal static MemoryStream Write(TestPageFacts facts)
	{
		ArgumentNullException.ThrowIfNull(facts);

		double width = facts.Paper.WidthMillimeters * TestPageWriter.PointsPerInch / TestPageWriter.MillimetresPerInch;
		double height = facts.Paper.HeightMillimeters * TestPageWriter.PointsPerInch / TestPageWriter.MillimetresPerInch;
		MemoryStream output = new MemoryStream();

		using (PdfDocumentWriter pdf = new PdfDocumentWriter(output))
		{
			pdf.BeginPage(width, height);

			// At its true place whatever the scale: it shows what the printer can't reach
			pdf.StrokeRectangle
			(
				TestPageWriter.EdgeInset,
				TestPageWriter.EdgeInset,
				width - (2 * TestPageWriter.EdgeInset),
				height - (2 * TestPageWriter.EdgeInset),
				TestPageWriter.ThinLine
			);

			TestPageWriter page = new TestPageWriter(pdf, width, height);
			page.WriteFacts(facts);
			page.WriteCapabilities(facts.Capabilities);
			page.WriteEdgeNote();
			page.WriteRulers();
			page.WritePatches();

			pdf.EndPage();
			pdf.Complete();
		}

		output.Position = 0;

		return output;
	}

	/// <summary>
	/// Chooses the column's scale: 1 when the paper has room for it inside <see cref="Margin"/>, and otherwise what
	/// fits it inside <see cref="SmallPaperMargin"/>.
	/// </summary>
	/// <param name="pageWidth">The page's width in points.</param>
	/// <param name="pageHeight">Its height in points.</param>
	/// <returns>The scale, at most 1.</returns>
	internal static double ChooseScale(double pageWidth, double pageHeight)
	{
		if (pageWidth - (2 * TestPageWriter.Margin) >= TestPageWriter.ContentWidth
			&& pageHeight - (2 * TestPageWriter.Margin) >= TestPageWriter.ContentHeight)
		{
			return 1;
		}

		double across = (pageWidth - (2 * TestPageWriter.SmallPaperMargin)) / TestPageWriter.ContentWidth;
		double down = (pageHeight - (2 * TestPageWriter.SmallPaperMargin)) / TestPageWriter.ContentHeight;

		return Math.Max(0.05, Math.Min(1, Math.Min(across, down)));
	}

	/// <summary>
	/// Makes text Courier can show without being embedded: a character outside Windows-1252, or a control character,
	/// becomes <c>?</c>, as a test page shouldn't fail over a printer's name; and text longer than fits is cut short.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="maxLength">The most characters there is room for.</param>
	/// <returns>The text to draw.</returns>
	internal static string ToDrawable(string text, int maxLength)
	{
		StringBuilder drawable = new StringBuilder(text.Length);

		for (int index = 0; index < text.Length; index++)
		{
			char character = text[index];

			// A pair of surrogates is one character, so one ?
			if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
			{
				index++;
				drawable.Append('?');

				continue;
			}

			bool drawn = !char.IsControl(character) && PdfWinAnsiEncoding.TryEncode(character, out _);
			drawable.Append(drawn ? character : '?');
		}

		if (drawable.Length > maxLength)
		{
			drawable.Length = Math.Max(0, maxLength - TestPageWriter.Ellipsis.Length);
			drawable.Append(TestPageWriter.Ellipsis);
		}

		return drawable.ToString();
	}

	/// <summary>
	/// Gets Latiprimere's version, without the commit it was built from.
	/// </summary>
	/// <returns>The version, such as <c>11.0.12-preview</c>.</returns>
	internal static string ReadVersion()
	{
		string? version = typeof(TestPageWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

		if (string.IsNullOrEmpty(version))
		{
			return TestPageWriter.NotReported;
		}

		int metadata = version.IndexOf('+', StringComparison.Ordinal);

		return metadata < 0 ? version : version[..metadata];
	}

	/// <summary>
	/// Writes the title and what the printer is.
	/// </summary>
	/// <param name="facts">What the page says.</param>
	private void WriteFacts(TestPageFacts facts)
	{
		this.DrawText(TestPageWriter.JobName, 0, this.cursor + TestPageWriter.TitleSize, TestPageWriter.TitleSize);
		this.cursor += TestPageWriter.TitleHeight;

		MediaSize paper = facts.Paper;
		string paperText = string.Create
		(
			CultureInfo.InvariantCulture,
			$"{paper.DisplayName} ({paper.WidthMillimeters:0.#} x {paper.HeightMillimeters:0.#} mm)"
		);

		this.WriteFact("Printer", facts.Printer.Name);
		this.WriteFact("ID", facts.Printer.Id);
		this.WriteFact("Make and model", facts.Printer.MakeAndModel);
		this.WriteFact("Location", facts.Printer.Location);
		this.WriteFact("Print system", facts.PrintSystem);
		this.WriteFact("Address", facts.Address);
		this.WriteFact("Paper", paperText);
		this.WriteFact("Printed", facts.PrintedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture));
		this.WriteFact("Latiprimere", TestPageWriter.ReadVersion());

		this.cursor += TestPageWriter.SectionGap;
	}

	/// <summary>
	/// Writes what the printer's capabilities say.
	/// </summary>
	/// <param name="capabilities">The capabilities.</param>
	private void WriteCapabilities(PrinterCapabilities capabilities)
	{
		this.WriteLine("What the printer says it can do:");

		string paperSizes = capabilities.MediaSizes.Count == 0
			? NotReported
			: capabilities.MediaSizes.Count.ToString(CultureInfo.InvariantCulture)
			  + (capabilities.DefaultMediaSize is null ? string.Empty : ", by default " + capabilities.DefaultMediaSize.DisplayName);

		this.WriteFact("Paper sizes", paperSizes);
		this.WriteFact("Two-sided", TestPageWriter.JoinOrNone(capabilities.DuplexModes.Select(TestPageWriter.Describe)));
		this.WriteFact("Colour", TestPageWriter.JoinOrNone(capabilities.ColorModes.Select(TestPageWriter.Describe)));
		this.WriteFact("Quality", TestPageWriter.JoinOrNone(capabilities.PrintQualities.Select(TestPageWriter.Describe)));
		this.WriteFact("Trays", TestPageWriter.JoinOrNone(capabilities.InputTrays.Select(tray => tray.Name)));
		this.WriteFact("Formats", TestPageWriter.JoinOrNone(capabilities.DocumentFormats.Select(TestPageWriter.Describe)));

		this.cursor += TestPageWriter.SectionGap;
	}

	/// <summary>
	/// Writes what the edge line shows, and that the page was shrunk, when it was.
	/// </summary>
	private void WriteEdgeNote()
	{
		this.WriteLine("The line round the page is 1/4 inch (6.35 mm) from each edge. A side that is missing");
		this.WriteLine("is outside what this printer can print.");

		if (this.scale < 1)
		{
			this.WriteLine("This paper is too small for the page, so it was shrunk to fit: the rulers are shorter.");
		}
		else
		{
			this.cursor += TestPageWriter.LineHeight;
		}

		this.cursor += TestPageWriter.SectionGap;
	}

	/// <summary>
	/// Draws the rulers, 100 mm and 4 inches, and what they show.
	/// </summary>
	private void WriteRulers()
	{
		double millimetre = TestPageWriter.PointsPerInch / TestPageWriter.MillimetresPerInch;

		this.DrawRuler
		(
			(int)(TestPageWriter.RulerMillimetres / 5),
			5 * millimetre,
			2,
			[(0, "0"), (50 * millimetre, "50"), (TestPageWriter.RulerMillimetres * millimetre, "100 mm")]
		);

		this.DrawRuler
		(
			(int)(TestPageWriter.RulerInches * 4),
			TestPageWriter.PointsPerInch / 4,
			4,
			[(0, "0"), (TestPageWriter.PointsPerInch, "1"), (2 * TestPageWriter.PointsPerInch, "2"), (3 * TestPageWriter.PointsPerInch, "3"), (4 * TestPageWriter.PointsPerInch, "4 in")]
		);

		this.WriteLine("Each ruler should measure exactly what it says. Shorter means the page was scaled.");
		this.cursor += TestPageWriter.SectionGap;
	}

	/// <summary>
	/// Draws a ruler: a line with ticks, longer at every few, and labels below.
	/// </summary>
	/// <param name="intervals">How many intervals between ticks it has.</param>
	/// <param name="interval">An interval's length, unscaled, in points.</param>
	/// <param name="longEvery">Which ticks are long: every this many.</param>
	/// <param name="labels">Where labels go along it, unscaled, in points, and what they say.</param>
	private void DrawRuler(int intervals, double interval, int longEvery, (double At, string Text)[] labels)
	{
		double baseline = this.cursor + TestPageWriter.LongTick + 2;

		this.DrawLine(0, baseline, intervals * interval, baseline);

		for (int tick = 0; tick <= intervals; tick++)
		{
			double length = tick % longEvery == 0 ? TestPageWriter.LongTick : TestPageWriter.ShortTick;

			this.DrawLine(tick * interval, baseline, tick * interval, baseline - length);
		}

		foreach ((double at, string text) in labels)
		{
			this.DrawText(text, at, baseline + TestPageWriter.BodySize + 1, TestPageWriter.BodySize);
		}

		this.cursor += TestPageWriter.RulerBlock;
	}

	/// <summary>
	/// Draws the colour patches and the grey ramp, with their labels.
	/// </summary>
	private void WritePatches()
	{
		// Cyan to blue, outlined, as yellow on white is hard to see
		for (int index = 0; index < TestPageWriter.Patches.Length; index++)
		{
			(string label, double red, double green, double blue) = TestPageWriter.Patches[index];
			double x = index * (TestPageWriter.PatchSize + TestPageWriter.PatchGap);

			this.FillRectangle(x, this.cursor, TestPageWriter.PatchSize, TestPageWriter.PatchSize, red, green, blue);
			this.StrokeRectangle(x, this.cursor, TestPageWriter.PatchSize, TestPageWriter.PatchSize);
			this.DrawText(label, x + (TestPageWriter.PatchSize / 2) - 3, this.cursor + TestPageWriter.PatchSize + 11, TestPageWriter.BodySize);
		}

		this.cursor += TestPageWriter.PatchBlock;

		// White to black in tenths, as greys, which a printer prints with black alone
		for (int step = 0; step <= 10; step++)
		{
			double grey = 1 - (step / 10.0);
			double x = step * TestPageWriter.PatchSize;

			// White can't be filled, as it isn't drawn: the outline shows it
			if (step > 0)
			{
				this.FillRectangle(x, this.cursor, TestPageWriter.PatchSize, TestPageWriter.PatchSize, grey, grey, grey);
			}
		}

		this.StrokeRectangle(0, this.cursor, 11 * TestPageWriter.PatchSize, TestPageWriter.PatchSize);

		double labels = this.cursor + TestPageWriter.PatchSize + 11;

		this.DrawText("0%", 0, labels, TestPageWriter.BodySize);
		this.DrawText("50%", 5 * TestPageWriter.PatchSize, labels, TestPageWriter.BodySize);
		this.DrawText("100%", 10 * TestPageWriter.PatchSize, labels, TestPageWriter.BodySize);

		this.cursor += TestPageWriter.PatchBlock;
	}

	/// <summary>
	/// Writes a fact: its label, padded, and its value, or that it wasn't reported.
	/// </summary>
	/// <param name="label">The label.</param>
	/// <param name="value">The value, or <see langword="null"/>.</param>
	private void WriteFact(string label, string? value)
	{
		string text = label.PadRight(TestPageWriter.LabelColumns) + (string.IsNullOrWhiteSpace(value) ? TestPageWriter.NotReported : value);

		this.WriteLine(text);
	}

	/// <summary>
	/// Writes a line of body text, cut short if it doesn't fit, and moves down a line.
	/// </summary>
	/// <param name="text">The text.</param>
	private void WriteLine(string text)
	{
		this.DrawText(text, 0, this.cursor + TestPageWriter.BodySize, TestPageWriter.BodySize);
		this.cursor += TestPageWriter.LineHeight;
	}

	/// <summary>
	/// Draws text, unscaled positions from the column's top left, cut short if it doesn't fit the column.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="x">Its left.</param>
	/// <param name="baseline">Its baseline, down from the column's top.</param>
	/// <param name="size">Its size.</param>
	private void DrawText(string text, double x, double baseline, double size)
	{
		int room = (int)((TestPageWriter.ContentWidth - x) / (size * TestPageWriter.CourierAdvance));

		this.pdf.DrawText(TestPageWriter.ToDrawable(text, room), this.ToPageX(x), this.ToPageY(baseline), size * this.scale);
	}

	/// <summary>
	/// Draws a line, unscaled positions from the column's top left.
	/// </summary>
	/// <param name="x1">Its start's left.</param>
	/// <param name="y1">Its start, down from the column's top.</param>
	/// <param name="x2">Its end's left.</param>
	/// <param name="y2">Its end, down from the column's top.</param>
	private void DrawLine(double x1, double y1, double x2, double y2)
	{
		this.pdf.DrawLine(this.ToPageX(x1), this.ToPageY(y1), this.ToPageX(x2), this.ToPageY(y2), TestPageWriter.ThinLine * this.scale);
	}

	/// <summary>
	/// Fills a rectangle, unscaled positions from the column's top left.
	/// </summary>
	/// <param name="x">Its left.</param>
	/// <param name="y">Its top, down from the column's top.</param>
	/// <param name="width">Its width.</param>
	/// <param name="height">Its height.</param>
	/// <param name="red">The colour's red, from 0 to 1.</param>
	/// <param name="green">Its green.</param>
	/// <param name="blue">Its blue.</param>
	private void FillRectangle(double x, double y, double width, double height, double red, double green, double blue)
	{
		this.pdf.FillRectangle(this.ToPageX(x), this.ToPageY(y + height), width * this.scale, height * this.scale, red, green, blue);
	}

	/// <summary>
	/// Outlines a rectangle, unscaled positions from the column's top left.
	/// </summary>
	/// <param name="x">Its left.</param>
	/// <param name="y">Its top, down from the column's top.</param>
	/// <param name="width">Its width.</param>
	/// <param name="height">Its height.</param>
	private void StrokeRectangle(double x, double y, double width, double height)
	{
		this.pdf.StrokeRectangle(this.ToPageX(x), this.ToPageY(y + height), width * this.scale, height * this.scale, TestPageWriter.ThinLine * this.scale);
	}

	/// <summary>
	/// Maps an unscaled distance from the column's left to the page.
	/// </summary>
	/// <param name="x">The distance.</param>
	/// <returns>The page's x, in points.</returns>
	private double ToPageX(double x)
	{
		return this.left + (x * this.scale);
	}

	/// <summary>
	/// Maps an unscaled distance down from the column's top to the page, whose y goes up from its bottom.
	/// </summary>
	/// <param name="y">The distance.</param>
	/// <returns>The page's y, in points.</returns>
	private double ToPageY(double y)
	{
		return this.top - (y * this.scale);
	}

	/// <summary>
	/// Joins descriptions with commas, or says none were reported.
	/// </summary>
	/// <param name="values">The descriptions.</param>
	/// <returns>The text.</returns>
	private static string JoinOrNone(IEnumerable<string> values)
	{
		string joined = string.Join(", ", values);

		return joined.Length == 0 ? TestPageWriter.NotReported : joined;
	}

	/// <summary>Describes a two-sided mode.</summary>
	/// <param name="mode">The mode.</param>
	/// <returns>Its description.</returns>
	private static string Describe(DuplexMode mode)
	{
		return mode switch
		{
			DuplexMode.OneSided  => "one-sided",
			DuplexMode.LongEdge  => "long edge",
			DuplexMode.ShortEdge => "short edge",
			_                    => mode.ToString(),
		};
	}

	/// <summary>Describes a colour mode.</summary>
	/// <param name="mode">The mode.</param>
	/// <returns>Its description.</returns>
	private static string Describe(ColorMode mode)
	{
		return mode switch
		{
			ColorMode.Monochrome => "monochrome",
			ColorMode.Color      => "colour",
			_                    => mode.ToString(),
		};
	}

	/// <summary>Describes a print quality.</summary>
	/// <param name="quality">The quality.</param>
	/// <returns>Its description.</returns>
	private static string Describe(PrintQuality quality)
	{
		return quality switch
		{
			PrintQuality.Draft  => "draft",
			PrintQuality.Normal => "normal",
			PrintQuality.High   => "high",
			_                   => quality.ToString(),
		};
	}

	/// <summary>Describes a document format.</summary>
	/// <param name="format">The format.</param>
	/// <returns>Its description.</returns>
	private static string Describe(DocumentFormat format)
	{
		return format switch
		{
			DocumentFormat.Pdf  => "PDF",
			DocumentFormat.Raw  => "raw",
			DocumentFormat.Jpeg => "JPEG",
			DocumentFormat.Png  => "PNG",
			DocumentFormat.Text => "text",
			_                   => format.ToString(),
		};
	}
}
