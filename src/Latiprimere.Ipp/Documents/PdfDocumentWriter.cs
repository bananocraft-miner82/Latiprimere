using System.Globalization;
using System.Text;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Writes a PDF document page by page: images, lines of Courier text, rectangles and lines placed on each page
/// (ADR 0045). For PCLm, whose pages are images in strips; for images and text sent as PDF to printers and the
/// Windows spooler that can't take them (Phase 4 plan, P4-1); and for the test page (ADR 0059).
/// </summary>
/// <remarks>
/// <para>
/// Each image is written as soon as it is drawn and each page as soon as it ends, so neither has to be held whole;
/// only the page tree waits for the end. Positions and sizes are in points, a 1/72 of an inch, from the page's bottom
/// left corner, as PDF has them.
/// </para>
/// <para>
/// Not safe to use from several threads at once.
/// </para>
/// </remarks>
internal sealed class PdfDocumentWriter : IDisposable
{
	/// <summary>The font resource name of Courier, the one font text is set in.</summary>
	private const string FontName = "F1";

	private readonly PdfObjectWriter objects;               // The file's objects
	private readonly int catalogNumber;                     // The document catalog
	private readonly int pagesNumber;                       // The page tree, written at the end
	private readonly List<int> pageNumbers = new List<int>(); // Each page, in order
	private int? fontNumber;                                // Courier, once a page uses it
	private PdfPageContent? page;                           // The page being written, between BeginPage and EndPage

	/// <summary>
	/// Initialises a new instance of the <see cref="PdfDocumentWriter"/> class, and writes the file's header.
	/// </summary>
	/// <param name="output">Where the document goes. It isn't disposed with the writer.</param>
	/// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
	public PdfDocumentWriter(Stream output)
		: this(output, Array.Empty<string>())
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PdfDocumentWriter"/> class with comments after the version line,
	/// such as PCLm's <c>PCLm 1.0</c>, and writes the file's header.
	/// </summary>
	/// <param name="output">Where the document goes. It isn't disposed with the writer.</param>
	/// <param name="headerComments">The comments, without their <c>%</c>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="output"/> or <paramref name="headerComments"/> is
	/// <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">A comment has a line break, or a character outside ASCII.</exception>
	public PdfDocumentWriter(Stream output, IReadOnlyList<string> headerComments)
	{
		this.objects = new PdfObjectWriter(output, headerComments);
		this.catalogNumber = this.objects.ReserveObject();
		this.pagesNumber = this.objects.ReserveObject();
	}

	/// <summary>
	/// Gets how many pages have been ended.
	/// </summary>
	public int PageCount
	{
		get
		{
			return this.pageNumbers.Count;
		}
	}

	/// <summary>
	/// Starts a page.
	/// </summary>
	/// <param name="widthPoints">Its width in points.</param>
	/// <param name="heightPoints">Its height in points.</param>
	/// <exception cref="ArgumentOutOfRangeException">A size isn't positive, or isn't finite.</exception>
	/// <exception cref="InvalidOperationException">A page is started already.</exception>
	public void BeginPage(double widthPoints, double heightPoints)
	{
		PdfDocumentWriter.ThrowIfNotPositive(widthPoints, nameof(widthPoints));
		PdfDocumentWriter.ThrowIfNotPositive(heightPoints, nameof(heightPoints));

		if (this.page is not null)
		{
			throw new InvalidOperationException(ExceptionMessages.PdfPageAlreadyStarted);
		}

		this.page = new PdfPageContent(widthPoints, heightPoints);
	}

	/// <summary>
	/// Draws an image on the page, stretched to fill a rectangle, and writes its samples to the file now.
	/// </summary>
	/// <param name="image">The image.</param>
	/// <param name="x">The rectangle's left, in points.</param>
	/// <param name="y">Its bottom, in points.</param>
	/// <param name="width">Its width, in points.</param>
	/// <param name="height">Its height, in points.</param>
	/// <exception cref="ArgumentException">The image names a filter as well as asking the writer to compress it, or
	/// its size or bits aren't ones PDF allows.</exception>
	/// <exception cref="InvalidOperationException">No page is started.</exception>
	public void DrawImage(PdfImage image, double x, double y, double width, double height)
	{
		this.DrawImage(image, new PdfMatrix(width, 0, 0, height, x, y));
	}

	/// <summary>
	/// Draws an image on the page through a transformation of the unit square it fills, so it can be turned or
	/// mirrored as well as placed, and writes its samples to the file now.
	/// </summary>
	/// <param name="image">The image.</param>
	/// <param name="matrix">Maps the unit square, whose top edge is the image's first row, to the page, in points.</param>
	/// <exception cref="ArgumentException">The image names a filter as well as asking the writer to compress it, or
	/// its size or bits aren't ones PDF allows.</exception>
	/// <exception cref="InvalidOperationException">No page is started.</exception>
	public void DrawImage(PdfImage image, PdfMatrix matrix)
	{
		ArgumentNullException.ThrowIfNull(image);

		PdfPageContent current = this.RequirePage();

		if ((image.Filter is not null && image.Encoding != PdfStreamEncoding.None)
			|| image.Width < 1 || image.Height < 1
			|| image.BitsPerComponent is not (1 or 2 or 4 or 8 or 16))
		{
			throw new ArgumentException(ExceptionMessages.PdfImageInvalid, nameof(image));
		}

		string colorSpace = image.ColorSpace switch
		{
			PdfColorSpace.Rgb  => "/DeviceRGB",
			PdfColorSpace.Cmyk => "/DeviceCMYK",
			_                  => "/DeviceGray",
		};

		StringBuilder entries = new StringBuilder()
			.Append("/Type /XObject /Subtype /Image /Width ").Append(image.Width.ToString(CultureInfo.InvariantCulture))
			.Append(" /Height ").Append(image.Height.ToString(CultureInfo.InvariantCulture))
			.Append(" /ColorSpace ").Append(colorSpace)
			.Append(" /BitsPerComponent ").Append(image.BitsPerComponent.ToString(CultureInfo.InvariantCulture));

		if (image.Filter is not null)
		{
			entries.Append(" /Filter /").Append(image.Filter);
		}

		if (image.DecodeParameters is not null)
		{
			entries.Append(" /DecodeParms ").Append(image.DecodeParameters);
		}

		if (image.Decode is not null)
		{
			entries.Append(" /Decode ").Append(image.Decode);
		}

		int number = this.objects.ReserveObject();
		this.objects.WriteStream(number, entries.ToString(), image.Encoding, image.WriteData);

		string name = "Im" + current.Images.Count.ToString(CultureInfo.InvariantCulture);
		current.Images.Add((name, number));

		// Map the unit square the image fills to the page, then draw it (8.9.4)
		current.Content
			.Append("q ").Append(PdfSyntax.FormatNumber(matrix.A))
			.Append(' ').Append(PdfSyntax.FormatNumber(matrix.B))
			.Append(' ').Append(PdfSyntax.FormatNumber(matrix.C))
			.Append(' ').Append(PdfSyntax.FormatNumber(matrix.D))
			.Append(' ').Append(PdfSyntax.FormatNumber(matrix.E))
			.Append(' ').Append(PdfSyntax.FormatNumber(matrix.F))
			.Append(" cm /").Append(name).Append(" Do Q\n");
	}

	/// <summary>
	/// Draws a line of text in Courier on the page.
	/// </summary>
	/// <param name="text">The text, in characters <see cref="PdfWinAnsiEncoding"/> encodes.</param>
	/// <param name="x">Its left, in points.</param>
	/// <param name="y">Its baseline, in points.</param>
	/// <param name="fontSize">Its size, in points.</param>
	/// <exception cref="ArgumentException">A character isn't one Courier shows without being embedded.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The size isn't positive, or isn't finite.</exception>
	/// <exception cref="InvalidOperationException">No page is started.</exception>
	public void DrawText(string text, double x, double y, double fontSize)
	{
		ArgumentNullException.ThrowIfNull(text);
		PdfDocumentWriter.ThrowIfNotPositive(fontSize, nameof(fontSize));

		PdfPageContent current = this.RequirePage();

		if (!PdfWinAnsiEncoding.TryEncode(text, out byte[] bytes, out int unsupportedIndex))
		{
			string code = "U+" + ((int)text[unsupportedIndex]).ToString("X4", CultureInfo.InvariantCulture);

			throw new ArgumentException(ExceptionMessages.FormatPdfTextCharacterUnsupported(code, unsupportedIndex), nameof(text));
		}

		current.UsesFont = true;
		current.Content
			.Append("BT /").Append(PdfDocumentWriter.FontName).Append(' ').Append(PdfSyntax.FormatNumber(fontSize))
			.Append(" Tf ").Append(PdfSyntax.FormatNumber(x)).Append(' ').Append(PdfSyntax.FormatNumber(y))
			.Append(" Td ").Append(PdfSyntax.FormatString(bytes)).Append(" Tj ET\n");
	}

	/// <summary>
	/// Fills a rectangle on the page in a colour: in DeviceGray when its components are equal, so a printer prints a
	/// grey with black alone, and in DeviceRGB otherwise.
	/// </summary>
	/// <param name="x">Its left, in points.</param>
	/// <param name="y">Its bottom, in points.</param>
	/// <param name="width">Its width, in points.</param>
	/// <param name="height">Its height, in points.</param>
	/// <param name="red">The colour's red, from 0 to 1.</param>
	/// <param name="green">Its green, from 0 to 1.</param>
	/// <param name="blue">Its blue, from 0 to 1.</param>
	/// <exception cref="ArgumentOutOfRangeException">A size isn't positive and finite, or a component isn't from 0 to
	/// 1.</exception>
	/// <exception cref="InvalidOperationException">No page is started.</exception>
	public void FillRectangle(double x, double y, double width, double height, double red, double green, double blue)
	{
		PdfDocumentWriter.ThrowIfNotPositive(width, nameof(width));
		PdfDocumentWriter.ThrowIfNotPositive(height, nameof(height));
		PdfDocumentWriter.ThrowIfNotComponent(red, nameof(red));
		PdfDocumentWriter.ThrowIfNotComponent(green, nameof(green));
		PdfDocumentWriter.ThrowIfNotComponent(blue, nameof(blue));

		PdfPageContent current = this.RequirePage();

		// The fill colour, then the rectangle filled (8.6.4, 8.5.2)
		string color = red == green && green == blue
			? PdfSyntax.FormatNumber(red) + " g"
			: PdfSyntax.FormatNumber(red) + " " + PdfSyntax.FormatNumber(green) + " " + PdfSyntax.FormatNumber(blue) + " rg";

		current.Content
			.Append("q ").Append(color)
			.Append(' ').Append(PdfDocumentWriter.FormatRectangle(x, y, width, height))
			.Append(" f Q\n");
	}

	/// <summary>
	/// Draws a rectangle's outline on the page in black.
	/// </summary>
	/// <param name="x">Its left, in points.</param>
	/// <param name="y">Its bottom, in points.</param>
	/// <param name="width">Its width, in points.</param>
	/// <param name="height">Its height, in points.</param>
	/// <param name="lineWidth">The line's width, in points, centred on the rectangle's edge.</param>
	/// <exception cref="ArgumentOutOfRangeException">A size or the line's width isn't positive and finite.</exception>
	/// <exception cref="InvalidOperationException">No page is started.</exception>
	public void StrokeRectangle(double x, double y, double width, double height, double lineWidth)
	{
		PdfDocumentWriter.ThrowIfNotPositive(width, nameof(width));
		PdfDocumentWriter.ThrowIfNotPositive(height, nameof(height));
		PdfDocumentWriter.ThrowIfNotPositive(lineWidth, nameof(lineWidth));

		PdfPageContent current = this.RequirePage();

		current.Content
			.Append("q ").Append(PdfSyntax.FormatNumber(lineWidth)).Append(" w 0 G ")
			.Append(PdfDocumentWriter.FormatRectangle(x, y, width, height))
			.Append(" S Q\n");
	}

	/// <summary>
	/// Draws a straight line on the page in black.
	/// </summary>
	/// <param name="x1">Its start's distance from the left, in points.</param>
	/// <param name="y1">Its start's distance from the bottom, in points.</param>
	/// <param name="x2">Its end's distance from the left, in points.</param>
	/// <param name="y2">Its end's distance from the bottom, in points.</param>
	/// <param name="lineWidth">The line's width, in points.</param>
	/// <exception cref="ArgumentOutOfRangeException">The line's width isn't positive and finite.</exception>
	/// <exception cref="InvalidOperationException">No page is started.</exception>
	public void DrawLine(double x1, double y1, double x2, double y2, double lineWidth)
	{
		PdfDocumentWriter.ThrowIfNotPositive(lineWidth, nameof(lineWidth));

		PdfPageContent current = this.RequirePage();

		current.Content
			.Append("q ").Append(PdfSyntax.FormatNumber(lineWidth)).Append(" w 0 G ")
			.Append(PdfSyntax.FormatNumber(x1)).Append(' ').Append(PdfSyntax.FormatNumber(y1)).Append(" m ")
			.Append(PdfSyntax.FormatNumber(x2)).Append(' ').Append(PdfSyntax.FormatNumber(y2)).Append(" l S Q\n");
	}

	/// <summary>
	/// Ends the page: writes its content and the page object.
	/// </summary>
	/// <exception cref="InvalidOperationException">No page is started.</exception>
	public void EndPage()
	{
		PdfPageContent current = this.RequirePage();

		byte[] content = Encoding.ASCII.GetBytes(current.Content.ToString());
		int contentNumber = this.objects.ReserveObject();
		this.objects.WriteStream(contentNumber, string.Empty, PdfStreamEncoding.Flate, stream => stream.Write(content));

		StringBuilder resources = new StringBuilder("<< ");

		if (current.UsesFont)
		{
			this.fontNumber ??= this.objects.ReserveObject();
			resources.Append("/Font << /").Append(PdfDocumentWriter.FontName).Append(' ').Append(PdfDocumentWriter.Reference(this.fontNumber.Value)).Append(" >> ");
		}

		if (current.Images.Count > 0)
		{
			resources.Append("/XObject << ");

			foreach ((string name, int number) in current.Images)
			{
				resources.Append('/').Append(name).Append(' ').Append(PdfDocumentWriter.Reference(number)).Append(' ');
			}

			resources.Append(">> ");
		}

		resources.Append(">>");

		int pageNumber = this.objects.ReserveObject();

		this.objects.WriteObject
		(
			pageNumber,
			"<< /Type /Page /Parent " + PdfDocumentWriter.Reference(this.pagesNumber)
			+ " /MediaBox [0 0 " + PdfSyntax.FormatNumber(current.Width) + " " + PdfSyntax.FormatNumber(current.Height) + "]"
			+ " /Resources " + resources
			+ " /Contents " + PdfDocumentWriter.Reference(contentNumber) + " >>"
		);

		this.pageNumbers.Add(pageNumber);
		this.page = null;
	}

	/// <summary>
	/// Ends the document: writes the font, the page tree, the catalog and the cross-reference table.
	/// </summary>
	/// <exception cref="InvalidOperationException">A page is still started, or no page was written.</exception>
	public void Complete()
	{
		if (this.page is not null)
		{
			throw new InvalidOperationException(ExceptionMessages.PdfPageNotEnded);
		}

		if (this.pageNumbers.Count == 0)
		{
			throw new InvalidOperationException(ExceptionMessages.PdfDocumentWithoutPages);
		}

		// The standard Courier needn't be embedded, and shows WinAnsiEncoding's characters (9.6.2.2)
		if (this.fontNumber is not null)
		{
			this.objects.WriteObject(this.fontNumber.Value, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");
		}

		string kids = string.Join(' ', this.pageNumbers.Select(PdfDocumentWriter.Reference));

		this.objects.WriteObject
		(
			this.pagesNumber,
			"<< /Type /Pages /Kids [" + kids + "] /Count " + this.pageNumbers.Count.ToString(CultureInfo.InvariantCulture) + " >>"
		);

		this.objects.WriteObject(this.catalogNumber, "<< /Type /Catalog /Pages " + PdfDocumentWriter.Reference(this.pagesNumber) + " >>");

		int infoNumber = this.objects.ReserveObject();
		this.objects.WriteObject(infoNumber, "<< /Producer (Latiprimere) >>");
		this.objects.Finish(this.catalogNumber, infoNumber);
	}

	/// <summary>
	/// Releases the writer. The output isn't closed: it belongs to the caller.
	/// </summary>
	public void Dispose()
	{
		this.objects.Dispose();
	}

	/// <summary>
	/// Writes a reference to an object.
	/// </summary>
	/// <param name="number">The object's number.</param>
	/// <returns>The reference, such as <c>7 0 R</c>.</returns>
	private static string Reference(int number)
	{
		return number.ToString(CultureInfo.InvariantCulture) + " 0 R";
	}

	/// <summary>
	/// Refuses a size that isn't positive and finite.
	/// </summary>
	/// <param name="value">The size.</param>
	/// <param name="name">The parameter's name.</param>
	/// <exception cref="ArgumentOutOfRangeException">It isn't.</exception>
	private static void ThrowIfNotPositive(double value, string name)
	{
		if (!double.IsFinite(value) || value <= 0)
		{
			throw new ArgumentOutOfRangeException(name, value, null);
		}
	}

	/// <summary>
	/// Refuses a colour component that isn't from 0 to 1.
	/// </summary>
	/// <param name="value">The component.</param>
	/// <param name="name">The parameter's name.</param>
	/// <exception cref="ArgumentOutOfRangeException">It isn't.</exception>
	private static void ThrowIfNotComponent(double value, string name)
	{
		if (!double.IsFinite(value) || value < 0 || value > 1)
		{
			throw new ArgumentOutOfRangeException(name, value, null);
		}
	}

	/// <summary>
	/// Writes a rectangle's <c>re</c> operator.
	/// </summary>
	/// <param name="x">Its left, in points.</param>
	/// <param name="y">Its bottom, in points.</param>
	/// <param name="width">Its width, in points.</param>
	/// <param name="height">Its height, in points.</param>
	/// <returns>The operator and its operands, such as <c>10 20 30 40 re</c>.</returns>
	private static string FormatRectangle(double x, double y, double width, double height)
	{
		return PdfSyntax.FormatNumber(x) + " " + PdfSyntax.FormatNumber(y) + " "
			   + PdfSyntax.FormatNumber(width) + " " + PdfSyntax.FormatNumber(height) + " re";
	}

	/// <summary>
	/// Gets the page being written.
	/// </summary>
	/// <returns>The page.</returns>
	/// <exception cref="InvalidOperationException">No page is started.</exception>
	private PdfPageContent RequirePage()
	{
		return this.page ?? throw new InvalidOperationException(ExceptionMessages.PdfPageNotStarted);
	}
}
