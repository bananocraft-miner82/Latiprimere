using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// Writes PCLm (Mopria's <c>application/PCLm</c>, ADR 0046): a PDF whose second line is <c>%PCLm 1.0</c>, each page
/// the paper's whole size at the resolution, drawn as image strips of the printer's preferred height from the top
/// down, each compressed on its own.
/// </summary>
/// <remarks>
/// <para>
/// Lines come as the other formats get them, top to bottom, already laid out, converted and turned for back sides by
/// <see cref="RasterDocumentEncoder"/>. They're gathered into a strip, which goes to the file once full, so a page is
/// never held whole: a strip of 32 lines of A4 at 600 dpi in colour is under half a megabyte.
/// </para>
/// <para>
/// PCLm pages are 8-bit grey (<c>DeviceGray</c>) or 24-bit colour (<c>DeviceRGB</c>); it has no 1-bit form.
/// </para>
/// </remarks>
internal sealed class PclmWriter : RasterWriter, IDisposable
{
	/// <summary>The comment that marks a PDF as PCLm, on its second line.</summary>
	internal const string HeaderComment = "PCLm 1.0";

	private readonly Stream output;                  // Where the document goes, not owned
	private readonly int stripHeight;                // The lines in each strip, the last on a page excepted
	private readonly PdfStreamEncoding compression;  // How each strip is compressed
	private PdfDocumentWriter? pdf;                  // The document, from WriteStart
	private RasterPageHeader? page;                  // The page being written, or null between pages
	private byte[] strip = [];                       // The current strip's lines
	private int bytesPerLine;                        // The current page's line length
	private int stripLines;                          // The lines in the current strip so far
	private int stripTop;                            // The current strip's first line on the page

	/// <summary>
	/// Initialises a new instance of the <see cref="PclmWriter"/> class.
	/// </summary>
	/// <param name="output">Where the document goes. It isn't disposed with the writer.</param>
	/// <param name="stripHeight">The lines in each strip, at least 1.</param>
	/// <param name="compression">How each strip is compressed: Flate or run-length.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="stripHeight"/> is less than 1, or
	/// <paramref name="compression"/> is neither.</exception>
	internal PclmWriter(Stream output, int stripHeight, PdfStreamEncoding compression)
		: base(output)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(stripHeight, 1);

		if (compression is not (PdfStreamEncoding.Flate or PdfStreamEncoding.RunLength))
		{
			throw new ArgumentOutOfRangeException(nameof(compression), compression, null);
		}

		this.output = output;
		this.stripHeight = stripHeight;
		this.compression = compression;
	}

	/// <inheritdoc/>
	internal override void WriteStart(int pageCount)
	{
		this.pdf = new PdfDocumentWriter(this.output, [PclmWriter.HeaderComment]);
	}

	/// <inheritdoc/>
	internal override void WriteLine(ReadOnlySpan<byte> line, int repeat)
	{
		ArgumentOutOfRangeException.ThrowIfNotEqual(line.Length, this.bytesPerLine);
		ArgumentOutOfRangeException.ThrowIfLessThan(repeat, 1);

		for (int count = 0; count < repeat; count++)
		{
			line.CopyTo(this.strip.AsSpan(this.stripLines * this.bytesPerLine));
			this.stripLines++;

			if (this.stripLines == this.stripHeight)
			{
				this.WriteStrip();
			}
		}
	}

	/// <summary>
	/// Ends the last page and the document, then flushes the stream.
	/// </summary>
	internal override void Flush()
	{
		this.EndPage();
		this.RequireDocument().Complete();
		base.Flush();
	}

	/// <summary>
	/// Releases the document writer. The output isn't closed: it belongs to the caller.
	/// </summary>
	public void Dispose()
	{
		this.pdf?.Dispose();
	}

	/// <summary>
	/// Starts a page, ending the one before: the paper's size in points, from its size in pixels at the resolution.
	/// </summary>
	/// <param name="header">The page.</param>
	/// <exception cref="ArgumentException">The page is 1-bit, which PCLm has no form for.</exception>
	protected override void WriteHeader(RasterPageHeader header)
	{
		if (header.ColorSpace == RasterColorSpace.Black1)
		{
			throw new ArgumentException(ExceptionMessages.PclmNotOneBit, nameof(header));
		}

		this.EndPage();

		this.page = header;
		this.bytesPerLine = RasterPixels.BytesPerLine(header.ColorSpace, header.Width);
		this.strip = new byte[this.bytesPerLine * this.stripHeight];
		this.stripLines = 0;
		this.stripTop = 0;

		this.RequireDocument().BeginPage(PclmWriter.ToPoints(header.Width, header.Dpi), PclmWriter.ToPoints(header.Height, header.Dpi));
	}

	/// <summary>
	/// Converts pixels at a resolution to points.
	/// </summary>
	/// <param name="pixels">The pixels.</param>
	/// <param name="dpi">The resolution.</param>
	/// <returns>The points, 72 to an inch.</returns>
	private static double ToPoints(int pixels, int dpi)
	{
		return pixels * 72.0 / dpi;
	}

	/// <summary>
	/// Writes the current strip, if it has any lines, as an image drawn at its place on the page.
	/// </summary>
	private void WriteStrip()
	{
		RasterPageHeader current = this.page!;

		if (this.stripLines == 0)
		{
			return;
		}

		int lines = this.stripLines;
		byte[] data = this.strip;
		int length = lines * this.bytesPerLine;

		PdfImage image = new PdfImage
		{
			Width      = current.Width,
			Height     = lines,
			ColorSpace = current.ColorSpace == RasterColorSpace.Srgb8 ? PdfColorSpace.Rgb : PdfColorSpace.Gray,
			Encoding   = this.compression,
			WriteData  = stream => stream.Write(data, 0, length)
		};

		// PDF measures up from the bottom, so a strip's place is the lines below it
		double bottom = PclmWriter.ToPoints(current.Height - this.stripTop - lines, current.Dpi);

		this.RequireDocument().DrawImage
		(
			image,
			0,
			bottom,
			PclmWriter.ToPoints(current.Width, current.Dpi),
			PclmWriter.ToPoints(lines, current.Dpi)
		);

		this.stripTop += lines;
		this.stripLines = 0;
	}

	/// <summary>
	/// Ends the page being written, if any: its last strip, which may be shorter, then the page.
	/// </summary>
	private void EndPage()
	{
		if (this.page is null)
		{
			return;
		}

		this.WriteStrip();
		this.RequireDocument().EndPage();
		this.page = null;
	}

	/// <summary>
	/// Gets the document, started by <see cref="WriteStart"/>.
	/// </summary>
	/// <returns>The document.</returns>
	/// <exception cref="InvalidOperationException">It isn't started.</exception>
	private PdfDocumentWriter RequireDocument()
	{
		return this.pdf ?? throw new InvalidOperationException(ExceptionMessages.PdfPageNotStarted);
	}
}
