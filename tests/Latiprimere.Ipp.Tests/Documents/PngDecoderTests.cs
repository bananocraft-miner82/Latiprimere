using System.Buffers.Binary;
using Latiprimere.Ipp.Documents;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// The PNG decoder against PNGs written apart from it, of every colour type, bit depth and interlacing, with every
/// row filter: each pixel must be what the specification says it is.
/// </summary>
public sealed class PngDecoderTests
{
	private const int Width = 13;  // Odd, so rows end part-way through a byte and interlacing passes are uneven
	private const int Height = 11; // Odd too

	[Theory]
	[InlineData(0, 1, false)]
	[InlineData(0, 2, true)]
	[InlineData(0, 4, false)]
	[InlineData(0, 8, true)]
	[InlineData(0, 16, false)]
	[InlineData(2, 8, false)]
	[InlineData(2, 16, true)]
	[InlineData(3, 1, true)]
	[InlineData(3, 2, false)]
	[InlineData(3, 4, true)]
	[InlineData(3, 8, false)]
	[InlineData(4, 8, true)]
	[InlineData(4, 16, false)]
	[InlineData(6, 8, false)]
	[InlineData(6, 16, true)]
	public void GivenAPngWhenDecodedThenEveryPixelIsItsValueIn8BitsOverWhite(int colorType, int bitDepth, bool interlaced)
	{
		// Arrange — values spread over the depth's range; alpha fully clear or fully opaque, so the result is the
		// colour or white; a palette whose entries differ in each channel (provided by InlineData above)
		int maximum = (1 << bitDepth) - 1;
		Func<int, int, int> value = (x, y) => ((x * 7) + (y * 13)) % (maximum + 1);
		Func<int, int, bool> clear = (x, y) => (x + y) % 3 == 0;
		byte[] palette = Enumerable.Range(0, 256).SelectMany(index => new[] { (byte)index, (byte)(255 - index), (byte)(index * 3) }).ToArray();

		Func<int, int, int[]> samples = colorType switch
		{
			0 or 3 => (x, y) => [value(x, y)],
			2 => (x, y) => [value(x, y), maximum - value(x, y), value(y, x)],
			4 => (x, y) => [value(x, y), clear(x, y) ? 0 : maximum],
			_ => (x, y) => [value(x, y), maximum - value(x, y), value(y, x), clear(x, y) ? 0 : maximum],
		};

		List<(string, byte[])> chunks = colorType == 3 ? [("PLTE", palette.AsSpan(0, (maximum + 1) * 3).ToArray())] : [];
		byte[] png = PngTestEncoder.Encode(PngDecoderTests.Width, PngDecoderTests.Height, colorType, bitDepth, samples, interlaced, chunks, imageChunkSize: 17);

		// Act
		byte[] decoded = PngDecoderTests.Decode(png);

		// Assert
		int channels = colorType is 0 or 4 ? 1 : 3;
		Assert.Equal(PngDecoderTests.Width * PngDecoderTests.Height * channels, decoded.Length);

		for (int y = 0; y < PngDecoderTests.Height; y++)
		{
			for (int x = 0; x < PngDecoderTests.Width; x++)
			{
				int[] stored = samples(x, y);
				bool white = colorType is 4 or 6 && clear(x, y);

				int[] expected = colorType switch
				{
					3 => [palette[stored[0] * 3], palette[(stored[0] * 3) + 1], palette[(stored[0] * 3) + 2]],
					0 or 4 => [PngDecoderTests.To8Bits(stored[0], bitDepth)],
					_ => [PngDecoderTests.To8Bits(stored[0], bitDepth), PngDecoderTests.To8Bits(stored[1], bitDepth), PngDecoderTests.To8Bits(stored[2], bitDepth)],
				};

				for (int channel = 0; channel < channels; channel++)
				{
					int actual = decoded[(((y * PngDecoderTests.Width) + x) * channels) + channel];
					Assert.True(actual == (white ? 255 : expected[channel]), $"pixel ({x}, {y}) channel {channel}: {actual}");
				}
			}
		}
	}

	[Fact]
	public void GivenPaletteTransparencyWhenDecodedThenEachEntrysAlphaIsLaidOverWhite()
	{
		// Arrange — entry 0 opaque black, 1 half-clear black, 2 clear red; entries past tRNS's end are opaque
		byte[] palette = [0, 0, 0, 0, 0, 0, 255, 0, 0, 0, 0, 255];
		byte[] png = PngTestEncoder.Encode(4, 1, 3, 8, (x, y) => [x], chunks: [("PLTE", palette), ("tRNS", [255, 128, 0])]);

		// Act
		byte[] decoded = PngDecoderTests.Decode(png);

		// Assert — half-clear black over white: (0 × 128 + 255 × 127 + 127) / 255 = 127
		Assert.Equal(new byte[] { 0, 0, 0, 127, 127, 127, 255, 255, 255, 0, 0, 255 }, decoded);
	}

	[Theory]
	[InlineData(0, 8, new byte[] { 0, 7 })]
	[InlineData(0, 16, new byte[] { 0x07, 0x07 })]
	[InlineData(2, 8, new byte[] { 0, 7, 0, 7, 0, 7 })]
	public void GivenAColourKeyWhenDecodedThenThatColourIsWhite(int colorType, int bitDepth, byte[] key)
	{
		// Arrange — a row whose second pixel is the key, 7 (or 0x0707 at 16 bits) in every channel (provided by
		// InlineData above)
		int[] keyed = colorType == 0 ? [(key[0] << 8) | key[1]] : [7, 7, 7];
		int[] other = colorType == 0 ? [bitDepth == 16 ? 0x0808 : 8] : [8, 8, 8];
		byte[] png = PngTestEncoder.Encode(2, 1, colorType, bitDepth, (x, y) => x == 1 ? keyed : other, chunks: [("tRNS", key)]);

		// Act
		byte[] decoded = PngDecoderTests.Decode(png);

		// Assert
		int channels = colorType == 0 ? 1 : 3;
		Assert.All(decoded.AsSpan(0, channels).ToArray(), sample => Assert.Equal(8, sample));
		Assert.All(decoded.AsSpan(channels, channels).ToArray(), sample => Assert.Equal(255, sample));
	}

	[Theory]
	[InlineData(2, 8, false, false, true)]
	[InlineData(0, 8, false, false, true)]
	[InlineData(2, 8, true, false, false)]
	[InlineData(2, 8, false, true, false)]
	[InlineData(2, 16, false, false, false)]
	[InlineData(3, 8, false, false, false)]
	[InlineData(6, 8, false, false, false)]
	public void GivenAPngWhenAskedThenOnlyOpaque8BitGreyOrColourNotInterlacedPassesThrough(int colorType, int bitDepth, bool interlaced, bool keyed, bool expected)
	{
		// Arrange (provided by InlineData above)
		List<(string, byte[])> chunks = [];

		if (colorType == 3)
		{
			chunks.Add(("PLTE", [0, 0, 0]));
		}

		if (keyed)
		{
			chunks.Add(("tRNS", [0, 0, 0, 0, 0, 0]));
		}

		byte[] png = PngTestEncoder.Encode(3, 2, colorType, bitDepth, (x, y) => new int[4], interlaced, chunks);

		// Act
		bool passes = PngDecoder.CanPassThrough(PngReader.Read(png));

		// Assert
		Assert.Equal(expected, passes);
	}

	[Fact]
	public void GivenAPassedThroughPngWhenReadThenItsImageDataGoesInAsItIsWithPngPredictors()
	{
		// Arrange
		byte[] png = PngTestEncoder.Encode(64, 48, 2, 8, ImageTestData.Quadrant, imageChunkSize: 100);
		PngHeader header = PngReader.Read(png);

		// Act
		PageImage image = PngDecoder.Read(png);

		// Assert — every IDAT chunk's data, in order
		Assert.Equal(("FlateDecode", PdfStreamEncoding.None), (image.Image.Filter, image.Image.Encoding));
		Assert.Equal("<< /Predictor 15 /Colors 3 /BitsPerComponent 8 /Columns 64 >>", image.Image.DecodeParameters);

		using MemoryStream written = new MemoryStream();
		image.Image.WriteData(written);
		byte[] expected = header.ImageData.SelectMany(chunk => png.AsSpan(chunk.Offset, chunk.Length).ToArray()).ToArray();
		Assert.Equal(expected, written.ToArray());
		Assert.True(header.ImageData.Count > 1);
	}

	[Fact]
	public void GivenPhysAndExifChunksWhenReadThenTheResolutionAndOrientationAreGiven()
	{
		// Arrange — 11811 pixels a metre is 300 dpi
		byte[] phys = new byte[9];
		BinaryPrimitives.WriteInt32BigEndian(phys, 11811);
		BinaryPrimitives.WriteInt32BigEndian(phys.AsSpan(4), 5906);
		phys[8] = 1;

		byte[] png = PngTestEncoder.Encode(2, 2, 0, 8, (x, y) => [0], chunks: [("pHYs", phys), ("eXIf", ImageTestData.Exif(8))]);

		// Act
		PageImage image = PngDecoder.Read(png);

		// Assert
		Assert.Equal(8, image.Orientation);
		Assert.Equal(300, image.DpiX!.Value, 1);
		Assert.Equal(150, image.DpiY!.Value, 1);
	}

	[Fact]
	public void GivenAnUnknownAncillaryChunkWhenReadThenItIsSkipped()
	{
		// Arrange — lower case first letter: ancillary
		byte[] png = PngTestEncoder.Encode(2, 1, 0, 8, (x, y) => [x * 255], chunks: [("tEXt", "Title\0Test"u8.ToArray()), ("zzZz", [1, 2, 3])]);

		// Act
		byte[] decoded = PngDecoderTests.Decode(png);

		// Assert
		Assert.Equal(new byte[] { 0, 255 }, decoded);
	}

	[Theory]
	[InlineData("signature", "isn't a PNG")]
	[InlineData("crc", "IHDR chunk")]
	[InlineData("critical", "ABCD chunk")]
	[InlineData("palette", "PLTE chunk")]
	[InlineData("depth", "bit depth 16, colour type 3")]
	[InlineData("size", "200000 by 1 pixels")]
	[InlineData("pixels", "20000 by 20000 pixels")]
	[InlineData("no-data", "image data")]
	public void GivenAPngThatCantBeReadWhenReadThenThrowsPrintException(string fault, string expected)
	{
		// Arrange — each fault in turn (provided by InlineData above)
		byte[] png = fault switch
		{
			"signature" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x00],
			"crc"       => PngDecoderTests.WithByteChanged(PngTestEncoder.Encode(2, 2, 0, 8, (x, y) => [0]), 16),
			"critical"  => PngTestEncoder.Encode(2, 2, 0, 8, (x, y) => [0], chunks: [("ABCD", [])]),
			"palette"   => PngTestEncoder.Encode(2, 2, 3, 8, (x, y) => [0]),
			"depth"     => PngTestEncoder.Encode(2, 2, 3, 16, (x, y) => [0]),
			"size"      => PngDecoderTests.Header(200_000, 1),
			"pixels"    => PngDecoderTests.Header(20_000, 20_000),
			_           => PngDecoderTests.Header(2, 2),
		};

		// Act
		Action read = () => PngDecoder.Read(png);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(read);
		Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenImageDataThatEndsEarlyWhenDecodedThenThrowsPrintException()
	{
		// Arrange — the image data of a 2 × 2 image in a PNG that says it is 2 × 9
		byte[] small = PngTestEncoder.Encode(2, 2, 0, 8, (x, y) => [0]);
		PngHeader header = PngReader.Read(small);
		byte[] data = header.ImageData.SelectMany(chunk => small.AsSpan(chunk.Offset, chunk.Length).ToArray()).ToArray();

		using MemoryStream file = new MemoryStream();
		file.Write(PngDecoderTests.Header(2, 9).AsSpan(0, 33));
		PngTestEncoder.WriteChunk(file, "IDAT", data);
		PngTestEncoder.WriteChunk(file, "IEND", []);

		// Act
		Action decode = () => PngDecoderTests.Decode(file.ToArray());

		// Assert
		PrintException exception = Assert.Throws<PrintException>(decode);
		Assert.Contains("ends early", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Decodes a PNG's rows.
	/// </summary>
	/// <param name="png">The PNG.</param>
	/// <returns>Its 8-bit rows, one after another.</returns>
	private static byte[] Decode(byte[] png)
	{
		using MemoryStream output = new MemoryStream();
		PngDecoder.WriteRows(PngReader.Read(png), output);

		return output.ToArray();
	}

	/// <summary>
	/// Scales a sample to 8 bits, as the specification recommends.
	/// </summary>
	/// <param name="value">The sample.</param>
	/// <param name="bitDepth">Its bits.</param>
	/// <returns>The 8-bit value.</returns>
	private static int To8Bits(int value, int bitDepth)
	{
		int maximum = (1 << bitDepth) - 1;

		return (int)Math.Round(value * 255.0 / maximum, MidpointRounding.AwayFromZero);
	}

	/// <summary>
	/// Writes a PNG's signature and header for 8-bit grey of a given size, with no image data.
	/// </summary>
	/// <param name="width">The width.</param>
	/// <param name="height">The height.</param>
	/// <returns>The bytes: 33 of them, then an <c>IEND</c> chunk.</returns>
	private static byte[] Header(int width, int height)
	{
		byte[] header = new byte[13];
		BinaryPrimitives.WriteInt32BigEndian(header, width);
		BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
		header[8] = 8;

		using MemoryStream file = new MemoryStream();
		file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
		PngTestEncoder.WriteChunk(file, "IHDR", header);
		PngTestEncoder.WriteChunk(file, "IEND", []);

		return file.ToArray();
	}

	/// <summary>
	/// Changes one byte of some data.
	/// </summary>
	/// <param name="data">The data.</param>
	/// <param name="index">Which byte.</param>
	/// <returns>A copy with that byte inverted.</returns>
	private static byte[] WithByteChanged(byte[] data, int index)
	{
		byte[] copy = [.. data];
		copy[index] ^= 0xFF;

		return copy;
	}
}
