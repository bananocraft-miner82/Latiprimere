using System.Buffers.Binary;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// Writes Apple raster (<c>image/urf</c>): the magic <c>UNIRAST</c> and a page count, then for each page a 32-byte
/// header and its compressed lines (ADR 0034).
/// </summary>
/// <remarks>
/// Apple hasn't published the format; this follows libcups, whose raster stream writes it, and cups-filters, which
/// read it. The lines are compressed as PWG raster's are. There is no 1-bit form: Apple raster pages are 8-bit grey
/// (<c>W8</c>) or 24-bit colour (<c>SRGB24</c>). Mirrored back sides have no header field: the pixels are written
/// mirrored, as the printer asked through <c>urf-supported</c>'s <c>DM</c> value.
/// </remarks>
internal sealed class AppleRasterWriter : RasterWriter
{
	/// <summary>The size of a page header.</summary>
	internal const int HeaderLength = 32;

	/// <summary>The magic the file starts with: <c>UNIRAST</c> and a zero byte.</summary>
	internal static readonly byte[] Magic = "UNIRAST\0"u8.ToArray();

	// Offsets into the page header, after libcups' Apple raster writer
	private const int BitsPerPixelOffset = 0;     // 8 or 24
	private const int ColorSpaceOffset = 1;       // 0 sGray, 1 sRGB
	private const int DuplexOffset = 2;           // 1 one-sided, 2 short-edge, 3 long-edge
	private const int PrintQualityOffset = 3;     // IPP print-quality, or 0 for the default
	private const int WidthOffset = 12;           // Pixels across, big-endian
	private const int HeightOffset = 16;          // Lines, big-endian
	private const int ResolutionOffset = 20;      // Dots per inch, big-endian

	private const byte ColorSpaceSGray = 0;
	private const byte ColorSpaceSRgb = 1;
	private const byte OneSided = 1;
	private const byte ShortEdge = 2;
	private const byte LongEdge = 3;

	/// <summary>
	/// Initialises a new instance of the <see cref="AppleRasterWriter"/> class.
	/// </summary>
	/// <param name="output">Where the document goes.</param>
	internal AppleRasterWriter(Stream output)
		: base(output)
	{
	}

	/// <inheritdoc/>
	internal override void WriteStart(int pageCount)
	{
		Span<byte> start = stackalloc byte[AppleRasterWriter.Magic.Length + 4];
		AppleRasterWriter.Magic.CopyTo(start);
		BinaryPrimitives.WriteUInt32BigEndian(start[AppleRasterWriter.Magic.Length..], (uint)pageCount);
		this.WriteRaw(start);
	}

	/// <inheritdoc/>
	protected override void WriteHeader(RasterPageHeader header)
	{
		if (header.ColorSpace == RasterColorSpace.Black1)
		{
			throw new ArgumentException(Resources.ExceptionMessages.AppleRasterNotOneBit, nameof(header));
		}

		Span<byte> bytes = stackalloc byte[AppleRasterWriter.HeaderLength];
		bytes.Clear();

		bytes[AppleRasterWriter.BitsPerPixelOffset] = (byte)RasterPixels.BitsPerPixel(header.ColorSpace);
		bytes[AppleRasterWriter.ColorSpaceOffset] = header.ColorSpace == RasterColorSpace.Srgb8 ? AppleRasterWriter.ColorSpaceSRgb : AppleRasterWriter.ColorSpaceSGray;
		bytes[AppleRasterWriter.DuplexOffset] = !header.Duplex ? AppleRasterWriter.OneSided : header.Tumble ? AppleRasterWriter.ShortEdge : AppleRasterWriter.LongEdge;
		bytes[AppleRasterWriter.PrintQualityOffset] = (byte)header.PrintQuality;
		BinaryPrimitives.WriteUInt32BigEndian(bytes[AppleRasterWriter.WidthOffset..], (uint)header.Width);
		BinaryPrimitives.WriteUInt32BigEndian(bytes[AppleRasterWriter.HeightOffset..], (uint)header.Height);
		BinaryPrimitives.WriteUInt32BigEndian(bytes[AppleRasterWriter.ResolutionOffset..], (uint)header.Dpi);

		this.WriteRaw(bytes);
	}
}
