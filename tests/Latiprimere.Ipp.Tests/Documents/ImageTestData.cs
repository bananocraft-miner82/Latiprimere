using System.Buffers.Binary;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// Test images and the pieces to change them: the 64 × 48 quadrant JPEG, EXIF data, and segments inserted into a
/// JPEG.
/// </summary>
internal static class ImageTestData
{
	/// <summary>
	/// Reads the quadrant JPEG: red top left, green top right, blue bottom left, white bottom right, with a JFIF
	/// header.
	/// </summary>
	/// <returns>The JPEG file.</returns>
	public static byte[] QuadrantsJpeg()
	{
		return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", "quadrants-64x48.jpg"));
	}

	/// <summary>
	/// The colours of a 64 × 48 PNG in the same quadrants as the JPEG, as stored 8-bit colour.
	/// </summary>
	/// <param name="x">The column.</param>
	/// <param name="y">The row.</param>
	/// <returns>Red, green and blue.</returns>
	public static int[] Quadrant(int x, int y)
	{
		return (x < 32, y < 24) switch
		{
			(true, true)  => [255, 0, 0],
			(false, true) => [0, 255, 0],
			(true, false) => [0, 0, 255],
			_             => [255, 255, 255],
		};
	}

	/// <summary>
	/// Inserts a segment straight after a JPEG's start-of-image marker.
	/// </summary>
	/// <param name="jpeg">The JPEG.</param>
	/// <param name="marker">The segment's marker, such as 0xE1 for APP1.</param>
	/// <param name="payload">Its data, after its length.</param>
	/// <returns>The new JPEG.</returns>
	public static byte[] InsertSegment(byte[] jpeg, byte marker, byte[] payload)
	{
		byte[] length = new byte[2];
		BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(payload.Length + 2));

		return [.. jpeg.AsSpan(0, 2), 0xFF, marker, .. length, .. payload, .. jpeg.AsSpan(2)];
	}

	/// <summary>
	/// Builds EXIF data: a TIFF header and one directory with an orientation and, if given, a resolution.
	/// </summary>
	/// <param name="orientation">The orientation, 1 to 8.</param>
	/// <param name="resolution">The resolution in both directions, or <see langword="null"/> for none.</param>
	/// <param name="unit">The resolution unit: 2 inches, 3 centimetres.</param>
	/// <param name="littleEndian">Whether to write it Intel's way, II, rather than Motorola's, MM.</param>
	/// <returns>The EXIF data, from the byte order mark.</returns>
	public static byte[] Exif(int orientation, int? resolution = null, int unit = 2, bool littleEndian = true)
	{
		List<(int Tag, int Type, uint Value)> entries = [(0x0112, 3, (uint)orientation)];
		int entryCount = resolution is null ? 1 : 4;
		int rationalOffset = 8 + 2 + (entryCount * 12) + 4;

		if (resolution is not null)
		{
			entries.Add((0x011A, 5, (uint)rationalOffset));
			entries.Add((0x011B, 5, (uint)rationalOffset + 8));
			entries.Add((0x0128, 3, (uint)unit));
		}

		byte[] data = new byte[rationalOffset + (resolution is null ? 0 : 16)];
		data[0] = data[1] = littleEndian ? (byte)'I' : (byte)'M';
		ImageTestData.Write16(data, 2, 42, littleEndian);
		ImageTestData.Write32(data, 4, 8, littleEndian);
		ImageTestData.Write16(data, 8, entryCount, littleEndian);

		for (int index = 0; index < entries.Count; index++)
		{
			int at = 10 + (index * 12);
			(int tag, int type, uint value) = entries[index];
			ImageTestData.Write16(data, at, tag, littleEndian);
			ImageTestData.Write16(data, at + 2, type, littleEndian);
			ImageTestData.Write32(data, at + 4, 1, littleEndian);

			// A SHORT sits in the first two bytes of the value field; a RATIONAL's field is its offset
			if (type == 3)
			{
				ImageTestData.Write16(data, at + 8, (int)value, littleEndian);
			}
			else
			{
				ImageTestData.Write32(data, at + 8, value, littleEndian);
			}
		}

		if (resolution is not null)
		{
			ImageTestData.Write32(data, rationalOffset, (uint)resolution.Value, littleEndian);
			ImageTestData.Write32(data, rationalOffset + 4, 1, littleEndian);
			ImageTestData.Write32(data, rationalOffset + 8, (uint)resolution.Value, littleEndian);
			ImageTestData.Write32(data, rationalOffset + 12, 1, littleEndian);
		}

		return data;
	}

	/// <summary>
	/// Writes a 16-bit value.
	/// </summary>
	/// <param name="data">Where.</param>
	/// <param name="offset">The offset.</param>
	/// <param name="value">The value.</param>
	/// <param name="littleEndian">The byte order.</param>
	private static void Write16(byte[] data, int offset, int value, bool littleEndian)
	{
		if (littleEndian)
		{
			BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), (ushort)value);
		}
		else
		{
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset), (ushort)value);
		}
	}

	/// <summary>
	/// Writes a 32-bit value.
	/// </summary>
	/// <param name="data">Where.</param>
	/// <param name="offset">The offset.</param>
	/// <param name="value">The value.</param>
	/// <param name="littleEndian">The byte order.</param>
	private static void Write32(byte[] data, int offset, uint value, bool littleEndian)
	{
		if (littleEndian)
		{
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
		}
		else
		{
			BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset), value);
		}
	}
}
