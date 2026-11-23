using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Resources;
using Latiprimere.Rendering;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// Renders a document's pages and writes them as a raster document, a band at a time, so a page is never held whole
/// (ADR 0034).
/// </summary>
/// <remarks>
/// <para>
/// <b>Each page</b> is the paper's whole size at the resolution. The document's page is fitted inside it, centred,
/// shrunk if it is larger and never enlarged, as the Windows path fits pages to paper. When the page's orientation,
/// or the one asked for, differs from the paper's, the page is rendered turned a quarter anticlockwise, the way
/// landscape is printed.
/// </para>
/// <para>
/// <b>Back sides</b> of two-sided jobs are turned as the printer asks (<see cref="RasterBackSide"/>), with libcups'
/// transforms: <c>flipped</c> mirrors long-edge back sides top to bottom and short-edge ones left to right;
/// <c>rotated</c> turns long-edge back sides upside down; <c>manual-tumble</c> turns short-edge ones upside down.
/// </para>
/// <para>
/// <b>Repeated lines</b>, such as the white above and below the content, are written once with a count.
/// </para>
/// </remarks>
internal static class RasterDocumentEncoder
{
	/// <summary>Hundredths of a millimetre in an inch, IPP's unit of paper size.</summary>
	private const long HundredthsPerInch = 2540;

	/// <summary>
	/// Encodes a whole document.
	/// </summary>
	/// <param name="document">The document to render.</param>
	/// <param name="settings">The format, colour space, resolution, paper and the rest.</param>
	/// <param name="output">Where the raster document goes.</param>
	/// <param name="cancellationToken">A token to stop between bands.</param>
	/// <exception cref="ArgumentException">The settings ask for something the format can't hold, such as 1-bit
	/// Apple raster or PCLm.</exception>
	/// <exception cref="OperationCanceledException">The token was cancelled.</exception>
	internal static void Encode(IRasterDocument document, RasterSettings settings, Stream output, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentNullException.ThrowIfNull(output);
		ArgumentOutOfRangeException.ThrowIfLessThan(settings.Dpi, 1);

		if (settings.Format == RasterFormat.AppleRaster && settings.ColorSpace == RasterColorSpace.Black1)
		{
			throw new ArgumentException(ExceptionMessages.AppleRasterNotOneBit, nameof(settings));
		}

		if (settings.Format == RasterFormat.Pclm && settings.ColorSpace == RasterColorSpace.Black1)
		{
			throw new ArgumentException(ExceptionMessages.PclmNotOneBit, nameof(settings));
		}

		RasterWriter writer = RasterWriter.Create(settings, output);

		try
		{
			// Only the pages asked for; sides alternate by the pages printed, not by their place in the document
			IReadOnlyList<int> pages = PageSelection.Select(settings.PageRanges, document.PageCount);
			writer.WriteStart(pages.Count);

			int mediaWidth = RasterDocumentEncoder.ToPixels(settings.Media.WidthMillimeters, settings.Dpi);
			int mediaHeight = RasterDocumentEncoder.ToPixels(settings.Media.HeightMillimeters, settings.Dpi);
			PageBuffers buffers = new PageBuffers(mediaWidth, settings.ColorSpace);

			for (int sheetSide = 0; sheetSide < pages.Count; sheetSide++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				PrintedPage page = new PrintedPage(pages[sheetSide], sheetSide, pages.Count);
				RasterDocumentEncoder.EncodePage(document, page, settings, mediaWidth, mediaHeight, writer, buffers, cancellationToken);
			}

			writer.Flush();
		}
		finally
		{
			// PCLm's writer holds a PDF writer; the others hold nothing to release
			(writer as IDisposable)?.Dispose();
		}
	}

	/// <summary>
	/// Converts a length on paper to pixels as libcups does, from whole hundredths of a millimetre and rounding down,
	/// so pages are the size printers get from CUPS and Apple: A4 at 600 dpi is 4,960 pixels wide, not 4,961.
	/// </summary>
	/// <param name="millimetres">The length.</param>
	/// <param name="dpi">The resolution.</param>
	/// <returns>The pixels, at least 1.</returns>
	internal static int ToPixels(double millimetres, int dpi)
	{
		long hundredths = (long)Math.Round(millimetres * 100, MidpointRounding.AwayFromZero);

		return (int)Math.Max(1, hundredths * dpi / RasterDocumentEncoder.HundredthsPerInch);
	}

	/// <summary>
	/// Works out how a page sits on the paper: turned or not, and where.
	/// </summary>
	/// <param name="page">The page's size.</param>
	/// <param name="orientation">The orientation asked for, or <see cref="PageOrientation.PrinterDefault"/> to
	/// follow the page.</param>
	/// <param name="mediaWidth">The paper's width in pixels.</param>
	/// <param name="mediaHeight">The paper's height in pixels.</param>
	/// <param name="dpi">The resolution.</param>
	/// <returns>The rotation, and the content's place and size in pixels.</returns>
	internal static (RasterRotation Rotation, int X, int Y, int Width, int Height) Layout
	(
		PageSize        page,
		PageOrientation orientation,
		int             mediaWidth,
		int             mediaHeight,
		int             dpi
	)
	{
		bool wantsLandscape = orientation switch
		{
			PageOrientation.Landscape => true,
			PageOrientation.Portrait => false,
			_ => page.IsLandscape,
		};

		// Landscape on portrait paper, or the other way round, turns the page a quarter, top to the left
		bool turns = wantsLandscape != (mediaWidth > mediaHeight);
		double widthPoints = turns ? page.HeightPoints : page.WidthPoints;
		double heightPoints = turns ? page.WidthPoints : page.HeightPoints;

		// The page's size at the resolution, shrunk to fit the paper, never enlarged, and centred
		double width = widthPoints / PageSize.PointsPerInch * dpi;
		double height = heightPoints / PageSize.PointsPerInch * dpi;
		double scale = Math.Min(1.0, Math.Min(mediaWidth / width, mediaHeight / height));
		int fittedWidth = Math.Clamp((int)Math.Round(width * scale), 1, mediaWidth);
		int fittedHeight = Math.Clamp((int)Math.Round(height * scale), 1, mediaHeight);

		return
		(
			turns ? RasterRotation.Counterclockwise90 : RasterRotation.None,
			(mediaWidth - fittedWidth) / 2,
			(mediaHeight - fittedHeight) / 2,
			fittedWidth,
			fittedHeight
		);
	}

	/// <summary>
	/// Works out a page's back-side transform: libcups' table for <c>pwg-raster-document-sheet-back</c>.
	/// </summary>
	/// <param name="settings">The settings.</param>
	/// <param name="pageIndex">The page's place among those printed, from 0: odd ones are back sides when two-sided.</param>
	/// <returns>Whether to mirror left to right, and top to bottom.</returns>
	internal static (bool FlipX, bool FlipY) BackSideTransform(RasterSettings settings, int pageIndex)
	{
		bool isTwoSided = settings.Duplex is DuplexMode.LongEdge or DuplexMode.ShortEdge;

		if (!isTwoSided || pageIndex % 2 == 0)
		{
			return (false, false);
		}

		bool tumble = settings.Duplex == DuplexMode.ShortEdge;

		return settings.BackSide switch
		{
			RasterBackSide.Flipped => (tumble, !tumble),
			RasterBackSide.Rotated => (!tumble, !tumble),
			RasterBackSide.ManualTumble => (tumble, tumble),
			_ => (false, false),
		};
	}

	/// <summary>
	/// Renders and writes one page.
	/// </summary>
	/// <param name="document">The document.</param>
	/// <param name="page">The page, and where it falls among those printed.</param>
	/// <param name="settings">The settings.</param>
	/// <param name="mediaWidth">The paper's width in pixels.</param>
	/// <param name="mediaHeight">The paper's height in pixels.</param>
	/// <param name="writer">The writer.</param>
	/// <param name="buffers">Line buffers reused from page to page.</param>
	/// <param name="cancellationToken">A token to stop between bands.</param>
	private static void EncodePage
	(
		IRasterDocument   document,
		PrintedPage       page,
		RasterSettings    settings,
		int               mediaWidth,
		int               mediaHeight,
		RasterWriter      writer,
		PageBuffers       buffers,
		CancellationToken cancellationToken
	)
	{
		(RasterRotation rotation, int x, int y, int width, int height) = RasterDocumentEncoder.Layout
		(
			document.GetPageSize(page.Index), settings.Orientation, mediaWidth, mediaHeight, settings.Dpi
		);

		int pageIndex = page.Index;
		(bool flipX, bool flipY) = RasterDocumentEncoder.BackSideTransform(settings, page.Position);
		int left = flipX ? mediaWidth - (x + width) : x;
		int top = flipY ? mediaHeight - (y + height) : y;

		writer.WritePage(new RasterPageHeader
		{
			Width        = mediaWidth,
			Height       = mediaHeight,
			Dpi          = settings.Dpi,
			ColorSpace   = settings.ColorSpace,
			Media        = settings.Media,
			TotalPages   = page.Count,
			Duplex       = settings.Duplex is DuplexMode.LongEdge or DuplexMode.ShortEdge,
			Tumble       = settings.Duplex == DuplexMode.ShortEdge,
			PrintQuality = settings.PrintQuality,
			FlipX        = flipX,
			FlipY        = flipY,
			ImageLeft    = left,
			ImageTop     = top,
			ImageRight   = left + width,
			ImageBottom  = top + height
		});

		// Bands as tall as the memory limit allows, in grey for grey and black (ADR 0054); back sides mirrored top to
		// bottom are rendered bottom up
		RasterPixelFormat pixelFormat = RasterPixels.RenderFormat(settings.ColorSpace);
		int bytesPerPixel = RasterBand.GetBytesPerPixel(pixelFormat);
		int stride = width * bytesPerPixel;
		int rowsPerBand = (int)Math.Clamp(settings.MaxBandBytes / stride, 1, height);
		byte[] band = buffers.GetBand(rowsPerBand * stride);
		int bandTop = -1;
		int bandRows = 0;

		LineRepeater lines = new LineRepeater(writer, buffers);

		for (int line = 0; line < mediaHeight; line++)
		{
			int mediaRow = flipY ? mediaHeight - 1 - line : line;
			int row = mediaRow - y;

			if (row < 0 || row >= height)
			{
				lines.Add(buffers.WhiteLine);

				continue;
			}

			if (row < bandTop || row >= bandTop + bandRows)
			{
				cancellationToken.ThrowIfCancellationRequested();
				bandTop = flipY ? Math.Max(0, row - rowsPerBand + 1) : row;
				bandRows = Math.Min(rowsPerBand, height - bandTop);
				RasterBand area = new RasterBand(width, height, bandTop, bandRows, stride) { Rotation = rotation, PixelFormat = pixelFormat };
				document.RenderBand(pageIndex, area, band);
			}

			// The content row, on white, at its place across the paper
			Span<byte> pixels = buffers.Pixels;
			pixels.Fill(byte.MaxValue);
			band.AsSpan((row - bandTop) * stride, stride).CopyTo(pixels[(x * bytesPerPixel)..]);

			if (flipX)
			{
				RasterPixels.Mirror(pixels, mediaWidth, pixelFormat);
			}

			RasterPixels.Convert(pixels, mediaWidth, settings.ColorSpace, line, buffers.Converted);
			lines.Add(buffers.Converted);
		}

		lines.Flush();
	}

	/// <summary>
	/// A page to print: its index in the document, and its place among the pages printed, which decides its side of
	/// the sheet.
	/// </summary>
	/// <param name="Index">The page's zero-based index in the document.</param>
	/// <param name="Position">Its zero-based place among the pages printed.</param>
	/// <param name="Count">The pages printed.</param>
	private readonly record struct PrintedPage(int Index, int Position, int Count);

	/// <summary>
	/// Line buffers for one paper width and colour space, reused across pages.
	/// </summary>
	private sealed class PageBuffers
	{
		private byte[] band = [];  // Rendered rows of the current band

		/// <summary>
		/// Initialises a new instance of the <see cref="PageBuffers"/> class.
		/// </summary>
		/// <param name="mediaWidth">The paper's width in pixels.</param>
		/// <param name="colorSpace">The colour space.</param>
		internal PageBuffers(int mediaWidth, RasterColorSpace colorSpace)
		{
			int bytesPerLine = RasterPixels.BytesPerLine(colorSpace, mediaWidth);
			this.Pixels = new byte[mediaWidth * RasterBand.GetBytesPerPixel(RasterPixels.RenderFormat(colorSpace))];
			this.Converted = new byte[bytesPerLine];
			this.Previous = new byte[bytesPerLine];
			this.WhiteLine = new byte[bytesPerLine];
			RasterPixels.FillWhite(colorSpace, this.WhiteLine);
		}

		/// <summary>Gets a line of the paper's width, in the colour space's render format, for composing a line.</summary>
		internal byte[] Pixels { get; }

		/// <summary>Gets a converted line.</summary>
		internal byte[] Converted { get; }

		/// <summary>Gets the last line added, waiting to be written with its repeat count.</summary>
		internal byte[] Previous { get; }

		/// <summary>Gets a white line, converted.</summary>
		internal byte[] WhiteLine { get; }

		/// <summary>
		/// Gets a band buffer of at least a size.
		/// </summary>
		/// <param name="length">The bytes needed.</param>
		/// <returns>The buffer.</returns>
		internal byte[] GetBand(int length)
		{
			if (this.band.Length < length)
			{
				this.band = new byte[length];
			}

			return this.band;
		}
	}

	/// <summary>
	/// Gathers identical consecutive lines and writes each run once, with its count.
	/// </summary>
	private sealed class LineRepeater
	{
		private readonly RasterWriter writer;  // Where lines go
		private readonly PageBuffers buffers;  // Holds the line waiting to be written
		private int repeat;                    // How many times the waiting line has been added

		/// <summary>
		/// Initialises a new instance of the <see cref="LineRepeater"/> class.
		/// </summary>
		/// <param name="writer">Where lines go.</param>
		/// <param name="buffers">The page's buffers.</param>
		internal LineRepeater(RasterWriter writer, PageBuffers buffers)
		{
			this.writer = writer;
			this.buffers = buffers;
		}

		/// <summary>
		/// Adds a line.
		/// </summary>
		/// <param name="line">The line, converted.</param>
		internal void Add(ReadOnlySpan<byte> line)
		{
			if (this.repeat > 0 && this.repeat < RasterLineEncoder.MaxLineRepeat && line.SequenceEqual(this.buffers.Previous))
			{
				this.repeat++;

				return;
			}

			this.Flush();
			line.CopyTo(this.buffers.Previous);
			this.repeat = 1;
		}

		/// <summary>
		/// Writes the waiting line, if any.
		/// </summary>
		internal void Flush()
		{
			if (this.repeat > 0)
			{
				this.writer.WriteLine(this.buffers.Previous, this.repeat);
				this.repeat = 0;
			}
		}
	}
}
