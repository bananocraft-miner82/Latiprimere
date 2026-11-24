using Xunit;

namespace Latiprimere.Rendering.Pdfium.Tests;

public sealed class PdfiumOptionsTests
{
	[Fact]
	public void GivenNewOptionsWhenReadThenTheyHoldTheDefaults()
	{
		// Arrange
		PdfiumOptions options = new PdfiumOptions();

		// Act
		int maxDocumentBytes = options.MaxDocumentBytes;
		TimeSpan renderTimeout = options.RenderTimeout;

		// Assert
		Assert.Equal(int.MaxValue, maxDocumentBytes);
		Assert.Equal(TimeSpan.FromMinutes(2), renderTimeout);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void GivenMaximumSizeThatIsntPositiveWhenSetThenThrowsArgumentOutOfRangeException(int value)
	{
		// Arrange — value provided by InlineData above
		PdfiumOptions options = new PdfiumOptions();

		// Act
		Action set = () => options.MaxDocumentBytes = value;

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1000)]
	[InlineData(366L * 24 * 60 * 60 * 1000)]
	public void GivenRenderTimeoutOutOfRangeWhenSetThenThrowsArgumentOutOfRangeException(long milliseconds)
	{
		// Arrange — zero, negative, or longer than a year (provided by InlineData above)
		PdfiumOptions options = new PdfiumOptions();

		// Act
		Action set = () => options.RenderTimeout = TimeSpan.FromMilliseconds(milliseconds);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(set);
	}

	[Fact]
	public void GivenInfiniteRenderTimeoutWhenSetThenItIsKept()
	{
		// Arrange
		PdfiumOptions options = new PdfiumOptions();

		// Act
		options.RenderTimeout = Timeout.InfiniteTimeSpan;

		// Assert
		Assert.Equal(Timeout.InfiniteTimeSpan, options.RenderTimeout);
	}
}
