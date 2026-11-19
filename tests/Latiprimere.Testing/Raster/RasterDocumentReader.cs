using System.Buffers.Binary;
using System.Text;

namespace Latiprimere.Testing.Raster;

/// <summary>
/// Reads PWG raster and Apple raster documents back into pages, so tests can check what a printer would receive.
/// Written from the formats' descriptions (PWG 5102.4; libcups for Apple raster), not from Latiprimere's writer.
/// </summary>
public static class RasterDocumentReader
{
	private const int PwgHeaderLength = 1796;
	private const int AppleHeaderLength = 32;
	private static readonly byte[] PwgSync = "RaS2"u8.ToArray();
	private static readonly byte[] AppleMagic = "UNIRAST\0"u8.ToArray();

	/// <summary>
	/// Reads a document, either format.
	/// </summary>
	/// <param name="document">The bytes.</param>
	/// <returns>The pages, with their lines decompressed.</returns>
	/// <exception cref="InvalidDataException">The bytes aren't a raster document, or are cut short.</exception>
	public static RasterDocument Read(byte[] document)
	{
		ArgumentNullException.ThrowIfNull(document);
		ReadOnlySpan<byte> bytes = document;

		if (bytes.StartsWith(RasterDocumentReader.PwgSync))
		{
			return RasterDocumentReader.ReadPwg(document);
		}

		if (bytes.StartsWith(RasterDocumentReader.AppleMagic))
		{
			return RasterDocumentReader.ReadApple(document);
		}

		throw new InvalidDataException("The document starts with neither RaS2 nor UNIRAST.");
	}

	/// <summary>
	/// Reads PWG raster.
	/// </summary>
	/// <param name="document">The bytes.</param>
	/// <returns>The document.</returns>
	private static RasterDocument ReadPwg(byte[] document)
	{
		List<RasterPage> pages = [];
		int offset = RasterDocumentReader.PwgSync.Length;

		while (offset < document.Length)
		{
			RasterDocumentReader.Require(document, offset, RasterDocumentReader.PwgHeaderLength);
			byte[] header = document.AsSpan(offset, RasterDocumentReader.PwgHeaderLength).ToArray();
			offset += RasterDocumentReader.PwgHeaderLength;

			int width = (int)RasterDocumentReader.UInt(header, 372);
			int height = (int)RasterDocumentReader.UInt(header, 376);
			int bitsPerPixel = (int)RasterDocumentReader.UInt(header, 388);
			int bytesPerLine = (int)RasterDocumentReader.UInt(header, 392);

			RasterPage page = new RasterPage
			{
				Width            = width,
				Height           = height,
				BitsPerPixel     = bitsPerPixel,
				BitsPerColor     = (int)RasterDocumentReader.UInt(header, 384),
				BytesPerLine     = bytesPerLine,
				DpiX             = (int)RasterDocumentReader.UInt(header, 276),
				DpiY             = (int)RasterDocumentReader.UInt(header, 280),
				ColorSpace       = (int)RasterDocumentReader.UInt(header, 400),
				NumColors        = (int)RasterDocumentReader.UInt(header, 420),
				MediaClass       = RasterDocumentReader.String(header, 0),
				PageSizeName     = RasterDocumentReader.String(header, 1732),
				PageSizePoints   = ((int)RasterDocumentReader.UInt(header, 352), (int)RasterDocumentReader.UInt(header, 356)),
				Duplex           = RasterDocumentReader.UInt(header, 272) != 0,
				Tumble           = RasterDocumentReader.UInt(header, 368) != 0,
				TotalPages       = (int)RasterDocumentReader.Integer(header, 0),
				CrossFeedTransform = (int)RasterDocumentReader.Integer(header, 1),
				FeedTransform    = (int)RasterDocumentReader.Integer(header, 2),
				ImageBox         = ((int)RasterDocumentReader.Integer(header, 3), (int)RasterDocumentReader.Integer(header, 4), (int)RasterDocumentReader.Integer(header, 5), (int)RasterDocumentReader.Integer(header, 6)),
				PrintQuality     = (int)RasterDocumentReader.Integer(header, 8),
				RawHeader        = header,
				Lines            = RasterDocumentReader.ReadLines(document, ref offset, height, bytesPerLine, bitsPerPixel == 24 ? 3 : 1, bitsPerPixel == 1 ? (byte)0x00 : (byte)0xFF)
			};

			pages.Add(page);
		}

		return new RasterDocument { Format = "image/pwg-raster", Pages = pages };
	}

	/// <summary>
	/// Reads Apple raster.
	/// </summary>
	/// <param name="document">The bytes.</param>
	/// <returns>The document.</returns>
	private static RasterDocument ReadApple(byte[] document)
	{
		RasterDocumentReader.Require(document, 0, 12);
		int pageCount = (int)BinaryPrimitives.ReadUInt32BigEndian(document.AsSpan(8));
		int offset = 12;
		List<RasterPage> pages = [];

		while (offset < document.Length)
		{
			RasterDocumentReader.Require(document, offset, RasterDocumentReader.AppleHeaderLength);
			byte[] header = document.AsSpan(offset, RasterDocumentReader.AppleHeaderLength).ToArray();
			offset += RasterDocumentReader.AppleHeaderLength;

			int bitsPerPixel = header[0];
			int width = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(12));
			int height = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16));
			int dpi = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20));
			int bytesPerLine = width * bitsPerPixel / 8;

			pages.Add(new RasterPage
			{
				Width          = width,
				Height         = height,
				BitsPerPixel   = bitsPerPixel,
				BitsPerColor   = 8,
				BytesPerLine   = bytesPerLine,
				DpiX           = dpi,
				DpiY           = dpi,
				ColorSpace     = header[1],
				NumColors      = bitsPerPixel / 8,
				Duplex         = header[2] != 1,
				Tumble         = header[2] == 2,
				AppleDuplexMode = header[2],
				PrintQuality   = header[3],
				TotalPages     = pageCount,
				RawHeader      = header,
				Lines          = RasterDocumentReader.ReadLines(document, ref offset, height, bytesPerLine, bitsPerPixel / 8, 0xFF)
			});
		}

		return new RasterDocument { Format = "image/urf", PageCount = pageCount, Pages = pages };
	}

	/// <summary>
	/// Decompresses a page's lines: a repeat byte per line, then runs (PWG 5102.4, section 4.3).
	/// </summary>
	/// <param name="document">The document.</param>
	/// <param name="offset">Where the page's data starts; moved past it.</param>
	/// <param name="height">The lines to read.</param>
	/// <param name="bytesPerLine">The bytes in a line.</param>
	/// <param name="unit">The bytes in a pixel unit.</param>
	/// <param name="white">The byte that 128 fills the rest of a line with.</param>
	/// <returns>The lines, top to bottom as sent.</returns>
	private static byte[][] ReadLines(byte[] document, ref int offset, int height, int bytesPerLine, int unit, byte white)
	{
		byte[][] lines = new byte[height][];
		int y = 0;

		while (y < height)
		{
			RasterDocumentReader.Require(document, offset, 1);
			int repeat = document[offset++] + 1;
			byte[] line = new byte[bytesPerLine];
			int x = 0;

			while (x < bytesPerLine)
			{
				RasterDocumentReader.Require(document, offset, 1);
				int control = document[offset++];

				if (control == 128)
				{
					line.AsSpan(x).Fill(white);
					x = bytesPerLine;
				}
				else if (control < 128)
				{
					RasterDocumentReader.Require(document, offset, unit);
					int count = control + 1;

					for (int i = 0; i < count; i++)
					{
						RasterDocumentReader.CheckLine(x + unit, bytesPerLine);
						document.AsSpan(offset, unit).CopyTo(line.AsSpan(x));
						x += unit;
					}

					offset += unit;
				}
				else
				{
					int count = (257 - control) * unit;
					RasterDocumentReader.Require(document, offset, count);
					RasterDocumentReader.CheckLine(x + count, bytesPerLine);
					document.AsSpan(offset, count).CopyTo(line.AsSpan(x));
					offset += count;
					x += count;
				}
			}

			for (int i = 0; i < repeat; i++)
			{
				if (y >= height)
				{
					throw new InvalidDataException($"A line repeats past the page's {height} lines.");
				}

				lines[y++] = line;
			}
		}

		return lines;
	}

	private static void CheckLine(int end, int bytesPerLine)
	{
		if (end > bytesPerLine)
		{
			throw new InvalidDataException($"A run goes past the line's {bytesPerLine} bytes.");
		}
	}

	private static void Require(byte[] document, int offset, int count)
	{
		if (offset + count > document.Length)
		{
			throw new InvalidDataException($"The document ends at {document.Length} bytes; {count} more were needed at {offset}.");
		}
	}

	private static uint UInt(byte[] header, int offset)
	{
		return BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(offset));
	}

	private static uint Integer(byte[] header, int index)
	{
		return RasterDocumentReader.UInt(header, 452 + (index * 4));
	}

	private static string String(byte[] header, int offset)
	{
		ReadOnlySpan<byte> field = header.AsSpan(offset, 64);
		int end = field.IndexOf((byte)0);

		return Encoding.ASCII.GetString(end < 0 ? field : field[..end]);
	}
}
