using Xunit;

namespace Latiprimere.Windows.Tests;

public sealed class WindowsOptionsTests
{
	[Fact]
	public void GivenNewOptionsWhenReadThenRendersAtPrinterResolutionAndChecksNames()
	{
		// Arrange
		WindowsOptions options = new WindowsOptions();

		// Act
		int? maxRenderDpi = options.MaxRenderDpi;

		// Assert
		Assert.Null(maxRenderDpi);
		Assert.False(options.AllowAnyPrinterName);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(300)]
	[InlineData(int.MaxValue)]
	public void GivenPositiveRenderDpiWhenSetThenKeepsIt(int dpi)
	{
		// Arrange — dpi is the value under test (provided by InlineData above)
		WindowsOptions options = new WindowsOptions();

		// Act
		options.MaxRenderDpi = dpi;

		// Assert
		Assert.Equal(dpi, options.MaxRenderDpi);
	}

	[Fact]
	public void GivenRenderDpiWhenSetBackToNullThenRendersAtPrinterResolutionAgain()
	{
		// Arrange
		WindowsOptions options = new WindowsOptions { MaxRenderDpi = 300 };

		// Act
		options.MaxRenderDpi = null;

		// Assert
		Assert.Null(options.MaxRenderDpi);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-300)]
	public void GivenRenderDpiBelowOneWhenSetThenThrowsAndKeepsThePreviousValue(int dpi)
	{
		// Arrange — dpi is the value under test (provided by InlineData above)
		WindowsOptions options = new WindowsOptions { MaxRenderDpi = 300 };

		// Act
		Action set = () => options.MaxRenderDpi = dpi;

		// Assert
		ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(set);
		Assert.Equal(nameof(WindowsOptions.MaxRenderDpi), exception.ParamName);
		Assert.Equal(300, options.MaxRenderDpi);
	}
}
