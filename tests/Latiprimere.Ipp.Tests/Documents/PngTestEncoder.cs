using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// Writes PNGs for the decoder's tests, written apart from the decoder from the specification: any colour type and
/// bit depth, rows filtered with each of the five filters in turn, Adam7 interlacing, and the image data split
/// across several chunks.
/// </summary>
internal static class PngTestEncoder
{
	// Adam7's passes: the first column and row each covers, and the steps between them
	private static readonly (int X, int Y, int StepX, int StepY)[] Passes =
	[
		(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2),
	];

	/// <summary>
	/// Writes a PNG.
	/// </summary>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	/// <param name="colorType">The colour type: 0, 2, 3, 4 or 6.</param>
	/// <param name="bitDepth">The bit depth.</param>
	/// <param name="samples">Each pixel's samples, as stored: grey, a palette index, or colour, then any alpha.</param>
	/// <param name="interlaced">Whether to interlace it with Adam7.</param>
	/// <param name="chunks">Chunks to put after the header, such as <c>PLTE</c>, <c>tRNS</c>, <c>pHYs</c> or
	/// <c>eXIf</c>.</param>
	/// <param name="imageChunkSize">The most image data in each <c>IDAT</c> chunk.</param>
	/// <returns>The PNG file.</returns>
	public static byte[] Encode
	(
		int                                 width,
		int                                 height,
		int                                 colorType,
		int                                 bitDepth,
		Func<int, int, int[]>               samples,
		bool                                interlaced = false,
		IEnumerable<(string Type, byte[] Data)>? chunks = null,
		int                                 imageChunkSize = 1000
	)
	{
		int channels = colorType switch
		{
			2 => 3,
			4 => 2,
			6 => 4,
			_ => 1,
		};

		using MemoryStream raw = new MemoryStream();

		if (interlaced)
		{
			foreach ((int startX, int startY, int stepX, int stepY) in PngTestEncoder.Passes)
			{
				int passWidth = width > startX ? (width - startX + stepX - 1) / stepX : 0;
				int passHeight = height > startY ? (height - startY + stepY - 1) / stepY : 0;

				if (passWidth > 0 && passHeight > 0)
				{
					PngTestEncoder.WriteRows(raw, passWidth, passHeight, channels, bitDepth, (x, y) => samples(startX + (x * stepX), startY + (y * stepY)));
				}
			}
		}
		else
		{
			PngTestEncoder.WriteRows(raw, width, height, channels, bitDepth, samples);
		}

		byte[] compressed;

		using (MemoryStream output = new MemoryStream())
		{
			using (ZLibStream zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
			{
				zlib.Write(raw.ToArray());
			}

			compressed = output.ToArray();
		}

		byte[] header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(header, width);
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
		header[8] = (byte)bitDepth;
		header[9] = (byte)colorType;
		header[12] = interlaced ? (byte)1 : (byte)0;

		using MemoryStream file = new MemoryStream();
		file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
		PngTestEncoder.WriteChunk(file, "IHDR", header);

		foreach ((string type, byte[] data) in chunks ?? [])
		{
			PngTestEncoder.WriteChunk(file, type, data);
		}

		for (int offset = 0; offset < compressed.Length; offset += imageChunkSize)
		{
			PngTestEncoder.WriteChunk(file, "IDAT", compressed.AsSpan(offset, Math.Min(imageChunkSize, compressed.Length - offset)).ToArray());
		}

		PngTestEncoder.WriteChunk(file, "IEND", []);

		return file.ToArray();
	}

	/// <summary>
	/// Writes a chunk: its length, type, data and CRC.
	/// </summary>
	/// <param name="output">Where it goes.</param>
	/// <param name="type">Its four-letter type.</param>
	/// <param name="data">Its data.</param>
	public static void WriteChunk(Stream output, string type, byte[] data)
	{
		byte[] length = new byte[4];
		BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
		output.Write(length);

		byte[] typeAndData = [.. Encoding.ASCII.GetBytes(type), .. data];
		output.Write(typeAndData);

		byte[] crc = new byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(crc, PngTestEncoder.Crc(typeAndData));
		output.Write(crc);
	}

	/// <summary>
	/// Packs and filters rows, each with filter type (row number mod 5), so every filter is used.
	/// </summary>
	/// <param name="output">Where the filtered rows go.</param>
	/// <param name="width">The pixels in each row.</param>
	/// <param name="height">The rows.</param>
	/// <param name="channels">The samples in each pixel.</param>
	/// <param name="bitDepth">The bits in each sample.</param>
	/// <param name="samples">Each pixel's samples.</param>
	private static void WriteRows(Stream output, int width, int height, int channels, int bitDepth, Func<int, int, int[]> samples)
	{
		int stride = ((width * channels * bitDepth) + 7) / 8;
		int bytesPerPixel = Math.Max(1, channels * bitDepth / 8);
		byte[] previous = new byte[stride];

		for (int y = 0; y < height; y++)
		{
			byte[] row = new byte[stride];

			for (int x = 0; x < width; x++)
			{
				int[] pixel = samples(x, y);

				for (int channel = 0; channel < channels; channel++)
				{
					int index = (x * channels) + channel;
					int value = pixel[channel];

					if (bitDepth == 16)
					{
						row[index * 2] = (byte)(value >> 8);
						row[(index * 2) + 1] = (byte)value;
					}
					else if (bitDepth == 8)
					{
						row[index] = (byte)value;
					}
					else
					{
						int bit = index * bitDepth;
						row[bit / 8] |= (byte)(value << (8 - bitDepth - (bit % 8)));
					}
				}
			}

			int filter = y % 5;
			output.WriteByte((byte)filter);

			for (int index = 0; index < stride; index++)
			{
				int left = index >= bytesPerPixel ? row[index - bytesPerPixel] : 0;
				int up = previous[index];
				int upperLeft = index >= bytesPerPixel ? previous[index - bytesPerPixel] : 0;

				int prediction = filter switch
				{
					1 => left,
					2 => up,
					3 => (left + up) / 2,
					4 => PngTestEncoder.Paeth(left, up, upperLeft),
					_ => 0,
				};

				output.WriteByte((byte)(row[index] - prediction));
			}

			previous = row;
		}
	}

	/// <summary>
	/// The Paeth predictor, as the specification gives it.
	/// </summary>
	/// <param name="a">Left.</param>
	/// <param name="b">Up.</param>
	/// <param name="c">Upper left.</param>
	/// <returns>The prediction.</returns>
	private static int Paeth(int a, int b, int c)
	{
		int p = a + b - c;
		int pa = Math.Abs(p - a);
		int pb = Math.Abs(p - b);
		int pc = Math.Abs(p - c);

		if (pa <= pb && pa <= pc)
		{
			return a;
		}

		return pb <= pc ? b : c;
	}

	/// <summary>
	/// The CRC-32 of a chunk's type and data, bit by bit.
	/// </summary>
	/// <param name="bytes">The bytes.</param>
	/// <returns>The CRC.</returns>
	private static uint Crc(byte[] bytes)
	{
		uint crc = 0xFFFFFFFF;

		foreach (byte value in bytes)
		{
			crc ^= value;

			for (int bit = 0; bit < 8; bit++)
			{
				crc = (crc >> 1) ^ (0xEDB88320 & (uint)-(int)(crc & 1));
			}
		}

		return ~crc;
	}
}
