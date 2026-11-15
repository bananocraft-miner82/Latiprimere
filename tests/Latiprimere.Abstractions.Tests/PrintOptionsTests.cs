using Xunit;

namespace Latiprimere.Abstractions.Tests;

public sealed class PrintOptionsTests
{
	[Fact]
	public void GivenDefaultOptionsWhenReadThenRequestsOneCopyAndPrinterDefaults()
	{
		// Arrange
		PrintOptions options = PrintOptions.Default;

		// Act
		int copies = options.Copies;

		// Assert
		Assert.Equal(1, copies);
		Assert.Equal(DuplexMode.PrinterDefault, options.Duplex);
		Assert.Equal(PageOrientation.PrinterDefault, options.Orientation);
		Assert.Null(options.JobName);
	}

	[Fact]
	public void GivenNewOptionsWhenCreatedThenEqualsDefaultOptions()
	{
		// Arrange
		PrintOptions options = new PrintOptions();

		// Act
		bool isEqual = options.Equals(PrintOptions.Default);

		// Assert
		Assert.True(isEqual);
	}

	[Fact]
	public void GivenValidSettingsWhenInitialisedThenKeepsThem()
	{
		// Arrange
		const int expectedCopies = 3;

		// Act
		PrintOptions options = new PrintOptions
		{
			JobName     = "Invoice 42",
			Copies      = expectedCopies,
			Duplex      = DuplexMode.LongEdge,
			Orientation = PageOrientation.Landscape
		};

		// Assert
		Assert.Equal("Invoice 42", options.JobName);
		Assert.Equal(expectedCopies, options.Copies);
		Assert.Equal(DuplexMode.LongEdge, options.Duplex);
		Assert.Equal(PageOrientation.Landscape, options.Orientation);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void GivenCopiesBelowMinimumWhenInitialisedThenThrowsArgumentOutOfRangeException(int copies)
	{
		// Arrange — copies is zero or negative (provided by InlineData above)
		Func<PrintOptions> createOptions = () => new PrintOptions { Copies = copies };

		// Act
		ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(createOptions);

		// Assert
		Assert.Equal(nameof(PrintOptions.Copies), exception.ParamName);
	}

	[Fact]
	public void GivenExistingOptionsWhenCopiedWithInvalidCopiesThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		PrintOptions options = PrintOptions.Default;

		// Act
		Func<PrintOptions> copyOptions = () => options with { Copies = 0 };

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(copyOptions);
	}
}
