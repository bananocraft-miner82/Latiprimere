using Latiprimere.Ipp.Documents;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

public sealed class PdfWinAnsiEncodingTests
{
	[Theory]
	[InlineData('A', 0x41)]
	[InlineData('~', 0x7E)]
	[InlineData(' ', 0xA0)]
	[InlineData('é', 0xE9)]
	[InlineData('ÿ', 0xFF)]
	[InlineData('€', 0x80)]
	[InlineData('’', 0x92)]
	[InlineData('—', 0x97)]
	[InlineData('Ÿ', 0x9F)]
	public void GivenWindows1252CharacterWhenEncodedThenGivesItsByte(char character, int expected)
	{
		// Arrange — ASCII, Latin-1, and Windows-1252's own: €, a curly quote, an em dash, Ÿ (provided by InlineData above)

		// Act
		bool isEncoded = PdfWinAnsiEncoding.TryEncode(character, out byte value);

		// Assert
		Assert.True(isEncoded);
		Assert.Equal(expected, value);
	}

	[Theory]
	[InlineData('\t')]
	[InlineData('\n')]
	[InlineData('\u007F')]
	[InlineData('\u0081')]
	[InlineData('Ā')]
	[InlineData('中')]
	[InlineData('\uD83D')]
	[InlineData('\0')]
	public void GivenCharacterOutsideWindows1252WhenEncodedThenRefusesIt(char character)
	{
		// Arrange — control characters, a byte Windows-1252 leaves unused, Latin Extended-A, Chinese, half a surrogate
		// pair (provided by InlineData above)

		// Act
		bool isEncoded = PdfWinAnsiEncoding.TryEncode(character, out byte value);

		// Assert
		Assert.False(isEncoded);
		Assert.Equal(0, value);
	}

	[Fact]
	public void GivenTextWithUnsupportedCharacterWhenEncodedThenGivesItsIndex()
	{
		// Arrange
		string text = "Café Ā";

		// Act
		bool isEncoded = PdfWinAnsiEncoding.TryEncode(text, out byte[] bytes, out int unsupportedIndex);

		// Assert
		Assert.False(isEncoded);
		Assert.Empty(bytes);
		Assert.Equal(5, unsupportedIndex);
	}

	[Fact]
	public void GivenSupportedTextWhenEncodedThenGivesOneByteEach()
	{
		// Arrange
		string text = "“Tea” – €2";

		// Act
		bool isEncoded = PdfWinAnsiEncoding.TryEncode(text, out byte[] bytes, out int unsupportedIndex);

		// Assert
		Assert.True(isEncoded);
		Assert.Equal(new byte[] { 0x93, (byte)'T', (byte)'e', (byte)'a', 0x94, (byte)' ', 0x96, (byte)' ', 0x80, (byte)'2' }, bytes);
		Assert.Equal(-1, unsupportedIndex);
	}
}
