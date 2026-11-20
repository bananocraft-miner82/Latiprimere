using Latiprimere.Ipp.Documents;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

public sealed class RunLengthEncoderStreamTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(127)]
	[InlineData(128)]
	[InlineData(129)]
	[InlineData(300)]
	public void GivenRunOfOneByteWhenEncodedThenDecodesToTheSameBytes(int length)
	{
		// Arrange — runs on either side of the shortest repeat and the longest (provided by InlineData above)
		byte[] data = Enumerable.Repeat((byte)0xAB, length).ToArray();

		// Act
		byte[] decoded = RunLengthEncoderStreamTests.EncodeAndDecode(data);

		// Assert
		Assert.Equal(data, decoded);
	}

	[Fact]
	public void GivenRandomBytesWithRunsWhenEncodedThenDecodesToTheSameBytes()
	{
		// Arrange — 200 buffers of random runs and random bytes, the same on every run
		Random random = new Random(1234);

		for (int round = 0; round < 200; round++)
		{
			List<byte> data = new List<byte>();

			while (data.Count < random.Next(1, 2000))
			{
				byte value = (byte)random.Next(256);
				int run = random.Next(3) == 0 ? random.Next(1, 300) : 1;
				data.AddRange(Enumerable.Repeat(value, run));
			}

			byte[] bytes = data.ToArray();

			// Act
			byte[] encoded = RunLengthEncoderStream.Encode(bytes);
			byte[] decoded = RunLengthEncoderStreamTests.Decode([.. encoded, RunLengthEncoderStream.EndOfData]);

			// Assert — and never much longer than the bytes themselves
			Assert.Equal(bytes, decoded);
			Assert.True(encoded.Length <= bytes.Length + (bytes.Length / 128) + 2, $"{encoded.Length} for {bytes.Length}");
		}
	}

	[Fact]
	public void GivenLongRunWhenEncodedThenTakesTwoBytesForEach128()
	{
		// Arrange
		byte[] data = new byte[256];

		// Act
		byte[] encoded = RunLengthEncoderStream.Encode(data);

		// Assert — 257 - 128 = 129 repeats the next byte 128 times
		Assert.Equal(new byte[] { 129, 0, 129, 0 }, encoded);
	}

	[Fact]
	public void GivenSeveralWritesWhenDisposedThenEndOfDataIsWrittenOnceAtTheEnd()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();

		// Act
		using (RunLengthEncoderStream stream = new RunLengthEncoderStream(output))
		{
			stream.Write([1, 2, 3]);
			stream.Write(new byte[10]);
			stream.Write(Array.Empty<byte>());
		}

		// Assert
		byte[] encoded = output.ToArray();
		Assert.Equal(RunLengthEncoderStream.EndOfData, encoded[^1]);
		Assert.Single(encoded, value => value == RunLengthEncoderStream.EndOfData);
		Assert.Equal(new byte[] { 1, 2, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RunLengthEncoderStreamTests.Decode(encoded));
	}

	/// <summary>
	/// Encodes bytes through the stream, then decodes them.
	/// </summary>
	/// <param name="data">The bytes.</param>
	/// <returns>The decoded bytes.</returns>
	private static byte[] EncodeAndDecode(byte[] data)
	{
		using MemoryStream output = new MemoryStream();

		using (RunLengthEncoderStream stream = new RunLengthEncoderStream(output))
		{
			stream.Write(data);
		}

		return RunLengthEncoderStreamTests.Decode(output.ToArray());
	}

	/// <summary>
	/// Decodes as PDF's <c>RunLengthDecode</c> filter does (ISO 32000-1, 7.4.5), written apart from the encoder.
	/// </summary>
	/// <param name="encoded">The encoded bytes, ending with the end of data.</param>
	/// <returns>The decoded bytes.</returns>
	private static byte[] Decode(byte[] encoded)
	{
		List<byte> decoded = new List<byte>();
		int index = 0;

		while (true)
		{
			byte length = encoded[index++];

			if (length == 128)
			{
				Assert.Equal(encoded.Length, index);

				return decoded.ToArray();
			}

			if (length < 128)
			{
				decoded.AddRange(encoded.AsSpan(index, length + 1).ToArray());
				index += length + 1;
			}
			else
			{
				decoded.AddRange(Enumerable.Repeat(encoded[index++], 257 - length));
			}
		}
	}
}
