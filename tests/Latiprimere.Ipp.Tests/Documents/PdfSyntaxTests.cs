using System.Globalization;
using Latiprimere.Ipp.Documents;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

public sealed class PdfSyntaxTests
{
	[Theory]
	[InlineData(1.0, "1")]
	[InlineData(0.5, "0.5")]
	[InlineData(595.27559, "595.2756")]
	[InlineData(1e10, "10000000000")]
	[InlineData(0.00001, "0")]
	[InlineData(-0.00001, "0")]
	[InlineData(-12.25, "-12.25")]
	public void GivenNumberWhenFormattedThenWritesItAsPdfDoes(double value, string expected)
	{
		// Arrange — whole, fractional, rounded, large without an exponent, tiny, a rounded negative zero, negative
		// (provided by InlineData above)

		// Act
		string text = PdfSyntax.FormatNumber(value);

		// Assert
		Assert.Equal(expected, text);
	}

	[Fact]
	public void GivenCultureWithDecimalCommaWhenFormattedThenStillWritesAFullStop()
	{
		// Arrange
		CultureInfo original = CultureInfo.CurrentCulture;
		CultureInfo.CurrentCulture = new CultureInfo("de-DE");

		try
		{
			// Act
			string text = PdfSyntax.FormatNumber(1.5);

			// Assert
			Assert.Equal("1.5", text);
		}
		finally
		{
			CultureInfo.CurrentCulture = original;
		}
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	public void GivenNumberThatIsntFiniteWhenFormattedThenThrowsArgumentOutOfRangeException(double value)
	{
		// Arrange (provided by InlineData above)

		// Act
		Func<string> format = () => PdfSyntax.FormatNumber(value);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(format);
	}

	[Fact]
	public void GivenBytesWithDelimitersAndHighBytesWhenFormattedAsStringThenEscapesThem()
	{
		// Arrange — parentheses, a backslash, € in WinAnsiEncoding, and a line feed
		byte[] bytes = [(byte)'(', (byte)'a', (byte)')', (byte)'\\', 0x80, 0x0A];

		// Act
		string text = PdfSyntax.FormatString(bytes);

		// Assert
		Assert.Equal(@"(\(a\)\\\200\012)", text);
	}
}
