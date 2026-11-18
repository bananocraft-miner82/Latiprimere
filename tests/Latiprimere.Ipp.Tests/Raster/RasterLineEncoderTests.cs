using System.Buffers;
using Latiprimere.Ipp.Raster;
using Xunit;

namespace Latiprimere.Ipp.Tests.Raster;

public sealed class RasterLineEncoderTests
{
	[Theory]
	[InlineData(new byte[] { 7 }, 1, 1, new byte[] { 0x00, 0x00, 7 })]
	[InlineData(new byte[] { 7, 7, 7, 1, 2 }, 1, 1, new byte[] { 0x00, 0x02, 7, 0xFF, 1, 2 })]
	[InlineData(new byte[] { 1, 2, 3, 3 }, 1, 4, new byte[] { 0x03, 0xFF, 1, 2, 0x01, 3 })]
	[InlineData(new byte[] { 1, 2, 3, 1, 2, 3, 9, 9, 9 }, 3, 1, new byte[] { 0x00, 0x01, 1, 2, 3, 0x00, 9, 9, 9 })]
	public void GivenLineWhenEncodedThenWritesTheRepeatByteAndRuns(byte[] line, int unit, int repeat, byte[] expected)
	{
		// Arrange — a single pixel is a run of one; two or more different pixels a literal run (provided by InlineData above)
		ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();

		// Act
		RasterLineEncoder.Encode(line, unit, repeat, output);

		// Assert
		Assert.Equal(expected, output.WrittenSpan.ToArray());
	}

	[Fact]
	public void GivenLongRunWhenEncodedThenSplitsItAt128Pixels()
	{
		// Arrange
		byte[] line = Enumerable.Repeat((byte)5, 200).ToArray();
		ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();

		// Act
		RasterLineEncoder.Encode(line, 1, 256, output);

		// Assert — 255 for 256 lines, then 128 and 72 fives
		Assert.Equal(new byte[] { 0xFF, 0x7F, 5, 0x47, 5 }, output.WrittenSpan.ToArray());
	}

	[Fact]
	public void GivenLongLiteralWhenEncodedThenSplitsItAt128PixelsAndNeverWrites128()
	{
		// Arrange — 130 different bytes
		byte[] line = Enumerable.Range(0, 130).Select(value => (byte)value).ToArray();
		ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();

		// Act
		RasterLineEncoder.Encode(line, 1, 1, output);

		// Assert — 129 (257 − 128) and 128 bytes, then 255 (257 − 2) and 2 bytes
		byte[] written = output.WrittenSpan.ToArray();
		Assert.Equal(1 + 1 + 128 + 1 + 2, written.Length);
		Assert.Equal(0x81, written[1]);
		Assert.Equal(0xFF, written[130]);
		Assert.DoesNotContain((byte)0x80, new[] { written[1], written[130] });
	}

	[Fact]
	public void GivenRandomLinesWhenEncodedThenTheReaderGetsThemBack()
	{
		// Arrange — runs, literals and edges at random, through the test project's own decoder
		Random random = new Random(20261118);

		for (int trial = 0; trial < 200; trial++)
		{
			int unit = trial % 2 == 0 ? 1 : 3;
			int pixels = random.Next(1, 700);
			byte[] line = new byte[pixels * unit];

			for (int i = 0; i < pixels; i++)
			{
				// Mostly a few values, so runs happen
				byte value = (byte)(random.Next(4) == 0 ? random.Next(256) : random.Next(3));
				line.AsSpan(i * unit, unit).Fill(value);
			}

			ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();
			RasterLineEncoder.Encode(line, unit, 1, output);

			// Act
			byte[] decoded = RasterLineEncoderTests.Decode(output.WrittenSpan.ToArray(), line.Length, unit);

			// Assert
			Assert.Equal(line, decoded);
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(257)]
	public void GivenRepeatOutsideOneTo256WhenEncodedThenThrows(int repeat)
	{
		// Arrange (provided by InlineData above)
		ArrayBufferWriter<byte> output = new ArrayBufferWriter<byte>();

		// Act
		Action encode = () => RasterLineEncoder.Encode(new byte[] { 1 }, 1, repeat, output);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(encode);
	}

	/// <summary>
	/// Decodes one line through the shared reader, wrapped as a one-line Apple raster page.
	/// </summary>
	/// <param name="encoded">The encoded line.</param>
	/// <param name="length">The line's length in bytes.</param>
	/// <param name="unit">The bytes in a pixel unit.</param>
	/// <returns>The line.</returns>
	private static byte[] Decode(byte[] encoded, int length, int unit)
	{
		byte[] header = new byte[32];
		header[0] = (byte)(unit * 8);
		System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(12), (uint)(length / unit));
		System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), 1);
		byte[] document = [.. "UNIRAST\0"u8.ToArray(), 0, 0, 0, 1, .. header, .. encoded];

		return Latiprimere.Testing.Raster.RasterDocumentReader.Read(document).Pages[0].Lines[0];
	}
}
