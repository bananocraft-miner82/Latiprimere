using System.Buffers.Binary;
using System.Text;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// Writes PWG raster (<c>image/pwg-raster</c>, PWG 5102.4): the sync word <c>RaS2</c>, then for each page a
/// 1,796-byte header and its compressed lines (ADR 0034).
/// </summary>
/// <remarks>
/// The header is the CUPS raster version 2 page header, big-endian, with the values PWG 5102.4 requires: its media
/// class is <c>PwgRaster</c>, chunky pixels, no compression flag (the line compression is always used), and the PWG
/// fields in <c>cupsInteger</c>. Fields PWG raster doesn't use are zero.
/// </remarks>
internal sealed class PwgRasterWriter : RasterWriter
{
	/// <summary>The size of a page header.</summary>
	internal const int HeaderLength = 1796;

	/// <summary>The sync word, version 2, written big-endian.</summary>
	internal static readonly byte[] SyncWord = "RaS2"u8.ToArray();

	// Offsets into the header, from PWG 5102.4 section 4.3 (the CUPS page header version 2)
	private const int MediaClassOffset = 0;            // char[64]: "PwgRaster"
	private const int DuplexOffset = 272;              // Duplex: 1 for two-sided
	private const int ResolutionOffset = 276;          // HWResolution[2]: dots per inch, across and down
	private const int NumCopiesOffset = 340;           // NumCopies: 1, as the printer makes the copies
	private const int PageSizeOffset = 352;            // PageSize[2]: the paper in points
	private const int TumbleOffset = 368;              // Tumble: 1 for short-edge two-sided
	private const int WidthOffset = 372;               // cupsWidth: pixels across
	private const int HeightOffset = 376;              // cupsHeight: lines
	private const int BitsPerColorOffset = 384;        // cupsBitsPerColor
	private const int BitsPerPixelOffset = 388;        // cupsBitsPerPixel
	private const int BytesPerLineOffset = 392;        // cupsBytesPerLine
	private const int ColorOrderOffset = 396;          // cupsColorOrder: 0, chunky
	private const int ColorSpaceOffset = 400;          // cupsColorSpace
	private const int NumColorsOffset = 420;           // cupsNumColors
	private const int IntegerOffset = 452;             // cupsInteger[16]: PWG's own fields
	private const int PageSizeNameOffset = 1732;       // cupsPageSizeName char[64]: the PWG media name
	private const int StringLength = 64;               // The length of each char[64] field

	// PWG's fields in cupsInteger[16], by index
	private const int TotalPageCountIndex = 0;         // The pages in the document
	private const int CrossFeedTransformIndex = 1;     // 1, or -1 for a page mirrored left to right
	private const int FeedTransformIndex = 2;          // 1, or -1 for a page whose lines run bottom to top
	private const int ImageBoxLeftIndex = 3;           // Where the content is, in pixels
	private const int ImageBoxTopIndex = 4;
	private const int ImageBoxRightIndex = 5;
	private const int ImageBoxBottomIndex = 6;
	private const int AlternatePrimaryIndex = 7;       // The colour of "no ink" for printers that fill it: white
	private const int PrintQualityIndex = 8;           // IPP print-quality, or 0 for the default

	// Colour spaces (the CUPS cups_cspace_t numbers PWG 5102.4 uses)
	private const int ColorSpaceBlack = 3;
	private const int ColorSpaceSGray = 18;
	private const int ColorSpaceSRgb = 19;

	private const uint Reversed = 0xFFFFFFFF;          // -1 as an unsigned integer: a transform that reverses
	private const int White = 0x00FFFFFF;              // AlternatePrimary's white
	private const long HundredthsPerInch = 2540;        // IPP's unit of paper size, in an inch
	private static readonly byte[] MediaClass = "PwgRaster"u8.ToArray();

	/// <summary>
	/// Initialises a new instance of the <see cref="PwgRasterWriter"/> class.
	/// </summary>
	/// <param name="output">Where the document goes.</param>
	internal PwgRasterWriter(Stream output)
		: base(output)
	{
	}

	/// <inheritdoc/>
	internal override void WriteStart(int pageCount)
	{
		this.WriteRaw(PwgRasterWriter.SyncWord);
	}

	/// <inheritdoc/>
	protected override void WriteHeader(RasterPageHeader header)
	{
		byte[] bytes = new byte[PwgRasterWriter.HeaderLength];
		Span<byte> span = bytes;

		PwgRasterWriter.WriteString(span, PwgRasterWriter.MediaClassOffset, PwgRasterWriter.MediaClass);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.DuplexOffset, header.Duplex ? 1u : 0u);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.ResolutionOffset, (uint)header.Dpi);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.ResolutionOffset + 4, (uint)header.Dpi);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.NumCopiesOffset, 1);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.PageSizeOffset, PwgRasterWriter.ToPoints(header.Media.WidthMillimeters));
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.PageSizeOffset + 4, PwgRasterWriter.ToPoints(header.Media.HeightMillimeters));
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.TumbleOffset, header.Duplex && header.Tumble ? 1u : 0u);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.WidthOffset, (uint)header.Width);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.HeightOffset, (uint)header.Height);

		int bitsPerPixel = RasterPixels.BitsPerPixel(header.ColorSpace);
		int colors = header.ColorSpace == RasterColorSpace.Srgb8 ? 3 : 1;
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.BitsPerColorOffset, (uint)(bitsPerPixel / colors));
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.BitsPerPixelOffset, (uint)bitsPerPixel);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.BytesPerLineOffset, (uint)RasterPixels.BytesPerLine(header.ColorSpace, header.Width));
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.ColorOrderOffset, 0);
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.ColorSpaceOffset, (uint)PwgRasterWriter.ToColorSpace(header.ColorSpace));
		PwgRasterWriter.WriteUInt(span, PwgRasterWriter.NumColorsOffset, (uint)colors);

		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.TotalPageCountIndex, (uint)header.TotalPages);
		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.CrossFeedTransformIndex, header.FlipX ? PwgRasterWriter.Reversed : 1u);
		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.FeedTransformIndex, header.FlipY ? PwgRasterWriter.Reversed : 1u);
		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.ImageBoxLeftIndex, (uint)header.ImageLeft);
		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.ImageBoxTopIndex, (uint)header.ImageTop);
		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.ImageBoxRightIndex, (uint)header.ImageRight);
		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.ImageBoxBottomIndex, (uint)header.ImageBottom);
		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.AlternatePrimaryIndex, PwgRasterWriter.White);
		PwgRasterWriter.WriteInteger(span, PwgRasterWriter.PrintQualityIndex, (uint)header.PrintQuality);

		PwgRasterWriter.WriteString(span, PwgRasterWriter.PageSizeNameOffset, Encoding.ASCII.GetBytes(header.Media.Name));

		this.WriteRaw(bytes);
	}

	/// <summary>
	/// Maps a colour space to PWG's number for it.
	/// </summary>
	/// <param name="colorSpace">The colour space.</param>
	/// <returns>3 (black), 18 (sGray) or 19 (sRGB).</returns>
	private static int ToColorSpace(RasterColorSpace colorSpace)
	{
		return colorSpace switch
		{
			RasterColorSpace.Black1 => PwgRasterWriter.ColorSpaceBlack,
			RasterColorSpace.Gray8 => PwgRasterWriter.ColorSpaceSGray,
			_ => PwgRasterWriter.ColorSpaceSRgb,
		};
	}

	/// <summary>
	/// Converts a length on paper to whole points as libcups does, from hundredths of a millimetre, rounding down.
	/// </summary>
	/// <param name="millimetres">The length.</param>
	/// <returns>The points.</returns>
	private static uint ToPoints(double millimetres)
	{
		const long pointsPerInch = 72;
		long hundredths = (long)Math.Round(millimetres * 100, MidpointRounding.AwayFromZero);

		return (uint)(pointsPerInch * hundredths / PwgRasterWriter.HundredthsPerInch);
	}

	/// <summary>
	/// Writes an unsigned integer, big-endian.
	/// </summary>
	/// <param name="header">The header.</param>
	/// <param name="offset">Where.</param>
	/// <param name="value">The value.</param>
	private static void WriteUInt(Span<byte> header, int offset, uint value)
	{
		BinaryPrimitives.WriteUInt32BigEndian(header[offset..], value);
	}

	/// <summary>
	/// Writes one of the PWG fields in <c>cupsInteger</c>.
	/// </summary>
	/// <param name="header">The header.</param>
	/// <param name="index">The field's index, 0 to 15.</param>
	/// <param name="value">The value.</param>
	private static void WriteInteger(Span<byte> header, int index, uint value)
	{
		PwgRasterWriter.WriteUInt(header, PwgRasterWriter.IntegerOffset + (index * 4), value);
	}

	/// <summary>
	/// Writes a string into a 64-byte field, padded with zeros and always ending in one.
	/// </summary>
	/// <param name="header">The header.</param>
	/// <param name="offset">Where the field starts.</param>
	/// <param name="value">The string's ASCII bytes.</param>
	private static void WriteString(Span<byte> header, int offset, ReadOnlySpan<byte> value)
	{
		value[..Math.Min(value.Length, PwgRasterWriter.StringLength - 1)].CopyTo(header[offset..]);
	}
}
