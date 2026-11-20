using System.Text;
using Latiprimere.Ipp.Documents;
using Latiprimere.Ipp.Tests.Fakes;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// Text re-encoded as UTF-8 as it is read, for CUPS (ADR 0048).
/// </summary>
public sealed class Utf8TextStreamTests
{
	[Fact]
	public void GivenUtf16WithAByteOrderMarkWhenReadThenItIsUtf8WithoutOne()
	{
		// Arrange — the mark overrides the encoding named
		byte[] utf16 = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("naïve Привет")];

		// Act
		byte[] utf8 = Utf8TextStreamTests.ReadAll(new Utf8TextStream(new MemoryStream(utf16), Encoding.UTF8), bufferSize: 3);

		// Assert
		Assert.Equal(Encoding.UTF8.GetBytes("naïve Привет"), utf8);
	}

	[Fact]
	public async Task GivenLongTextWithSurrogatePairsAcrossItsBuffersWhenReadThenEveryCharacterSurvives()
	{
		// Arrange — 4,095 letters put an emoji's halves either side of the 4,096-character buffer's end
		string text = new string('a', 4095) + "😀" + new string('b', 10_000) + "😀";
		using Utf8TextStream stream = new Utf8TextStream(new NonSeekableStream(Encoding.UTF32.GetBytes(text)), Encoding.UTF32);

		// Act
		using MemoryStream read = new MemoryStream();
		await stream.CopyToAsync(read, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(Encoding.UTF8.GetBytes(text), read.ToArray());
		Assert.False(stream.CanSeek);
	}

	[Fact]
	public void GivenEmptyTextWhenReadThenNothingIsGiven()
	{
		// Arrange
		Utf8TextStream stream = new Utf8TextStream(new MemoryStream(), Encoding.Latin1);

		// Act
		byte[] read = Utf8TextStreamTests.ReadAll(stream, bufferSize: 16);

		// Assert
		Assert.Empty(read);
	}

	/// <summary>
	/// Reads a stream to its end a few bytes at a time, then disposes it.
	/// </summary>
	/// <param name="stream">The stream.</param>
	/// <param name="bufferSize">How many bytes each read asks for.</param>
	/// <returns>What was read.</returns>
	private static byte[] ReadAll(Stream stream, int bufferSize)
	{
		using (stream)
		{
			List<byte> read = [];
			byte[] buffer = new byte[bufferSize];
			int count;

			while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				read.AddRange(buffer.AsSpan(0, count).ToArray());
			}

			return [.. read];
		}
	}
}
