using System.Buffers;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// Writes a raster document: a file header, then each page's header and compressed lines (ADR 0034).
/// </summary>
/// <remarks>
/// Lines are encoded into a buffer and written to the stream in large pieces, so the stream sees few writes even
/// though each line is small.
/// </remarks>
internal abstract class RasterWriter
{
	/// <summary>How many encoded bytes to gather before writing them to the stream.</summary>
	private const int WriteThreshold = 64 * 1024;

	private readonly Stream output;                                         // Where the document goes
	private readonly ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>(RasterWriter.WriteThreshold * 2); // Bytes not yet written
	private int bytesPerUnit = 1;                                           // The current page's compression unit
	private int bytesPerLine;                                               // The current page's line length

	/// <summary>
	/// Initialises a new instance of the <see cref="RasterWriter"/> class.
	/// </summary>
	/// <param name="output">Where the document goes.</param>
	protected RasterWriter(Stream output)
	{
		ArgumentNullException.ThrowIfNull(output);
		this.output = output;
	}

	/// <summary>
	/// Creates the writer for a format.
	/// </summary>
	/// <param name="settings">The settings, with the format, and for PCLm its strips' height and compression.</param>
	/// <param name="output">Where the document goes.</param>
	/// <returns>The writer.</returns>
	internal static RasterWriter Create(RasterSettings settings, Stream output)
	{
		ArgumentNullException.ThrowIfNull(settings);

		return settings.Format switch
		{
			RasterFormat.AppleRaster => new AppleRasterWriter(output),
			RasterFormat.Pclm        => new PclmWriter(output, settings.PclmStripHeight, settings.PclmCompression),
			_                        => new PwgRasterWriter(output),
		};
	}

	/// <summary>
	/// Writes what comes before the first page.
	/// </summary>
	/// <param name="pageCount">The number of pages to follow.</param>
	internal abstract void WriteStart(int pageCount);

	/// <summary>
	/// Starts a page, writing its header. Its lines follow, top to bottom as they are to be printed.
	/// </summary>
	/// <param name="header">The page.</param>
	internal void WritePage(RasterPageHeader header)
	{
		ArgumentNullException.ThrowIfNull(header);

		this.bytesPerUnit = RasterPixels.BytesPerUnit(header.ColorSpace);
		this.bytesPerLine = RasterPixels.BytesPerLine(header.ColorSpace, header.Width);
		this.WriteHeader(header);
	}

	/// <summary>
	/// Writes a line, and how many times it repeats.
	/// </summary>
	/// <param name="line">The line, in the page's colour space.</param>
	/// <param name="repeat">How many consecutive lines are this one, 1 to 256.</param>
	internal virtual void WriteLine(ReadOnlySpan<byte> line, int repeat)
	{
		ArgumentOutOfRangeException.ThrowIfNotEqual(line.Length, this.bytesPerLine);

		RasterLineEncoder.Encode(line, this.bytesPerUnit, repeat, this.buffer);

		if (this.buffer.WrittenCount >= RasterWriter.WriteThreshold)
		{
			this.FlushBuffer();
		}
	}

	/// <summary>
	/// Writes what's still buffered, and flushes the stream.
	/// </summary>
	internal virtual void Flush()
	{
		this.FlushBuffer();
		this.output.Flush();
	}

	/// <summary>
	/// Writes a page header.
	/// </summary>
	/// <param name="header">The page.</param>
	protected abstract void WriteHeader(RasterPageHeader header);

	/// <summary>
	/// Writes bytes after anything still buffered, keeping their order.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	protected void WriteRaw(ReadOnlySpan<byte> bytes)
	{
		this.buffer.Write(bytes);
	}

	/// <summary>
	/// Writes the buffered bytes to the stream.
	/// </summary>
	private void FlushBuffer()
	{
		if (this.buffer.WrittenCount > 0)
		{
			this.output.Write(this.buffer.WrittenSpan);
			this.buffer.ResetWrittenCount();
		}
	}
}
