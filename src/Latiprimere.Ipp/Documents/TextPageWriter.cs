namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Sets lines of text on pages of a PDF, in Courier at 10 points within half-inch margins, starting a page when one
/// fills or a form feed asks (ADR 0048).
/// </summary>
internal sealed class TextPageWriter
{
	/// <summary>The font size, in points.</summary>
	internal const double FontSize = 10;

	/// <summary>The width of every Courier character at <see cref="FontSize"/>: 600 thousandths of an em.</summary>
	internal const double CharacterWidth = 6;

	/// <summary>The distance between lines, in points: 1.2 times the font size, as word processors set it.</summary>
	internal const double LineHeight = 12;

	/// <summary>The margin on every side, in points: half an inch.</summary>
	internal const double Margin = 36;

	private readonly PdfDocumentWriter pdf;   // Where the pages go, not owned
	private readonly double pageWidth;        // The page's width in points
	private readonly double pageHeight;       // Its height
	private bool pageOpen;                    // Whether a page is started and not yet ended
	private int row;                          // The next line's row on the open page
	private bool endedByFormFeed;             // Whether the last page to end was ended by a form feed

	/// <summary>
	/// Initialises a new instance of the <see cref="TextPageWriter"/> class.
	/// </summary>
	/// <param name="pdf">Where the pages go. It isn't completed or disposed here.</param>
	/// <param name="pageWidth">The page's width in points.</param>
	/// <param name="pageHeight">Its height in points.</param>
	public TextPageWriter(PdfDocumentWriter pdf, double pageWidth, double pageHeight)
	{
		ArgumentNullException.ThrowIfNull(pdf);

		this.pdf = pdf;
		this.pageWidth = pageWidth;
		this.pageHeight = pageHeight;
		this.Columns = Math.Max(1, (int)((pageWidth - (2 * TextPageWriter.Margin)) / TextPageWriter.CharacterWidth));
		this.Rows = Math.Max(1, (int)((pageHeight - (2 * TextPageWriter.Margin)) / TextPageWriter.LineHeight));
	}

	/// <summary>Gets the characters that fit across a line.</summary>
	public int Columns { get; }

	/// <summary>Gets the lines that fit down a page.</summary>
	public int Rows { get; }

	/// <summary>Gets the pages written so far.</summary>
	public int PageCount { get; private set; }

	/// <summary>
	/// Sets a line on the next row, starting a page if none is open, and ending it if the line fills it.
	/// </summary>
	/// <param name="line">The line, no longer than <see cref="Columns"/>, in characters Courier shows.</param>
	public void WriteLine(string line)
	{
		ArgumentNullException.ThrowIfNull(line);

		if (!this.pageOpen)
		{
			this.pdf.BeginPage(this.pageWidth, this.pageHeight);
			this.pageOpen = true;
			this.row = 0;
		}

		// Spaces print nothing, so a line of them only takes its row
		string text = line.TrimEnd(' ');

		if (text.Length > 0)
		{
			double baseline = this.pageHeight - TextPageWriter.Margin - TextPageWriter.FontSize - (this.row * TextPageWriter.LineHeight);
			this.pdf.DrawText(text, TextPageWriter.Margin, baseline, TextPageWriter.FontSize);
		}

		this.row++;

		if (this.row == this.Rows)
		{
			this.EndPage(byFormFeed: false);
		}
	}

	/// <summary>
	/// Starts a new page, as a form feed asks. A form feed straight after a page filled doesn't leave a blank page;
	/// one at the start, or after another form feed, does.
	/// </summary>
	public void FormFeed()
	{
		if (this.pageOpen)
		{
			this.EndPage(byFormFeed: true);
		}
		else if (this.PageCount == 0 || this.endedByFormFeed)
		{
			this.pdf.BeginPage(this.pageWidth, this.pageHeight);
			this.pageOpen = true;
			this.EndPage(byFormFeed: true);
		}
		else
		{
			this.endedByFormFeed = true;
		}
	}

	/// <summary>
	/// Ends the open page, and writes a blank one if the text had none, as a document needs a page.
	/// </summary>
	public void Finish()
	{
		if (this.pageOpen)
		{
			this.EndPage(byFormFeed: false);
		}

		if (this.PageCount == 0)
		{
			this.pdf.BeginPage(this.pageWidth, this.pageHeight);
			this.pageOpen = true;
			this.EndPage(byFormFeed: false);
		}
	}

	/// <summary>
	/// Ends the open page.
	/// </summary>
	/// <param name="byFormFeed">Whether a form feed ended it.</param>
	private void EndPage(bool byFormFeed)
	{
		this.pdf.EndPage();
		this.pageOpen = false;
		this.endedByFormFeed = byFormFeed;
		this.PageCount++;
	}
}
