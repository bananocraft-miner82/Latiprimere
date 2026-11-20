using System.Buffers.Binary;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Reads the orientation and resolution from EXIF data: a TIFF header and its first directory (EXIF 2.32, 4.6), as
/// a JPEG's APP1 segment and a PNG's <c>eXIf</c> chunk carry them.
/// </summary>
/// <remarks>
/// EXIF only helps show an image, so data that can't be read is ignored rather than refused: whatever can be read
/// safely is used, and the rest left as if not given.
/// </remarks>
internal static class ExifReader
{
	private const int OrientationTag = 0x0112;     // SHORT, 1 to 8
	private const int XResolutionTag = 0x011A;     // RATIONAL
	private const int YResolutionTag = 0x011B;     // RATIONAL
	private const int ResolutionUnitTag = 0x0128;  // SHORT: 2 inches, 3 centimetres
	private const int ShortType = 3;               // A 16-bit unsigned value
	private const int RationalType = 5;            // Two 32-bit unsigned values, a fraction
	private const int EntrySize = 12;              // Tag, type, count and value or offset
	private const int MaxEntries = 1000;           // More than any real directory, to bound the work

	/// <summary>
	/// Reads EXIF data.
	/// </summary>
	/// <param name="tiff">The data, from the TIFF header's byte order mark.</param>
	/// <returns>What it says; <see cref="ExifInfo.None"/>'s values for what it doesn't.</returns>
	public static ExifInfo Read(ReadOnlySpan<byte> tiff)
	{
		if (tiff.Length < 8)
		{
			return ExifInfo.None;
		}

		bool littleEndian;

		if (tiff[0] == (byte)'I' && tiff[1] == (byte)'I')
		{
			littleEndian = true;
		}
		else if (tiff[0] == (byte)'M' && tiff[1] == (byte)'M')
		{
			littleEndian = false;
		}
		else
		{
			return ExifInfo.None;
		}

		if (ExifReader.ReadUInt16(tiff, 2, littleEndian) != 42)
		{
			return ExifInfo.None;
		}

		long directory = ExifReader.ReadUInt32(tiff, 4, littleEndian);

		if (directory < 8 || directory + 2 > tiff.Length)
		{
			return ExifInfo.None;
		}

		int count = Math.Min(ExifReader.ReadUInt16(tiff, (int)directory, littleEndian), ExifReader.MaxEntries);
		int orientation = 1;
		double? xResolution = null;
		double? yResolution = null;
		int unit = 2;

		for (int index = 0; index < count; index++)
		{
			long entry = directory + 2 + ((long)index * ExifReader.EntrySize);

			if (entry + ExifReader.EntrySize > tiff.Length)
			{
				break;
			}

			int at = (int)entry;
			int tag = ExifReader.ReadUInt16(tiff, at, littleEndian);
			int type = ExifReader.ReadUInt16(tiff, at + 2, littleEndian);

			switch (tag)
			{
				case ExifReader.OrientationTag when type == ExifReader.ShortType:
					int value = ExifReader.ReadUInt16(tiff, at + 8, littleEndian);
					orientation = value is >= 1 and <= 8 ? value : 1;
					break;

				case ExifReader.ResolutionUnitTag when type == ExifReader.ShortType:
					unit = ExifReader.ReadUInt16(tiff, at + 8, littleEndian);
					break;

				case ExifReader.XResolutionTag when type == ExifReader.RationalType:
					xResolution = ExifReader.ReadRational(tiff, ExifReader.ReadUInt32(tiff, at + 8, littleEndian), littleEndian);
					break;

				case ExifReader.YResolutionTag when type == ExifReader.RationalType:
					yResolution = ExifReader.ReadRational(tiff, ExifReader.ReadUInt32(tiff, at + 8, littleEndian), littleEndian);
					break;
			}
		}

		// Resolutions are per inch, or per centimetre; anything else, such as "none", gives no size
		double perInch = unit switch
		{
			2 => 1.0,
			3 => 2.54,
			_ => 0.0,
		};

		return new ExifInfo
		(
			orientation,
			perInch > 0 && xResolution is not null ? xResolution * perInch : null,
			perInch > 0 && yResolution is not null ? yResolution * perInch : null
		);
	}

	/// <summary>
	/// Reads a fraction of two 32-bit unsigned values.
	/// </summary>
	/// <param name="tiff">The data.</param>
	/// <param name="offset">Where the fraction is.</param>
	/// <param name="littleEndian">The byte order.</param>
	/// <returns>The value, or <see langword="null"/> if it is outside the data or divides by zero.</returns>
	private static double? ReadRational(ReadOnlySpan<byte> tiff, long offset, bool littleEndian)
	{
		if (offset < 8 || offset + 8 > tiff.Length)
		{
			return null;
		}

		long numerator = ExifReader.ReadUInt32(tiff, (int)offset, littleEndian);
		long denominator = ExifReader.ReadUInt32(tiff, (int)offset + 4, littleEndian);

		return denominator == 0 ? null : (double)numerator / denominator;
	}

	/// <summary>
	/// Reads a 16-bit unsigned value.
	/// </summary>
	/// <param name="tiff">The data.</param>
	/// <param name="offset">Where it is.</param>
	/// <param name="littleEndian">The byte order.</param>
	/// <returns>The value.</returns>
	private static int ReadUInt16(ReadOnlySpan<byte> tiff, int offset, bool littleEndian)
	{
		ReadOnlySpan<byte> bytes = tiff.Slice(offset, 2);

		return littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(bytes) : BinaryPrimitives.ReadUInt16BigEndian(bytes);
	}

	/// <summary>
	/// Reads a 32-bit unsigned value.
	/// </summary>
	/// <param name="tiff">The data.</param>
	/// <param name="offset">Where it is.</param>
	/// <param name="littleEndian">The byte order.</param>
	/// <returns>The value.</returns>
	private static long ReadUInt32(ReadOnlySpan<byte> tiff, int offset, bool littleEndian)
	{
		ReadOnlySpan<byte> bytes = tiff.Slice(offset, 4);

		return littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(bytes) : BinaryPrimitives.ReadUInt32BigEndian(bytes);
	}
}
