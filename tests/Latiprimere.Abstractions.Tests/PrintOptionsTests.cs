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
		Assert.Equal(ColorMode.PrinterDefault, options.ColorMode);
		Assert.Equal(ScalingMode.PrinterDefault, options.Scaling);
		Assert.Null(options.MediaSize);
		Assert.Null(options.InputTray);
		Assert.Empty(options.VendorOptions);
		Assert.Null(options.JobName);
	}

	[Fact]
	public void GivenVendorOptionsWhenInitialisedThenKeepsACopy()
	{
		// Arrange
		Dictionary<string, string> settings = new Dictionary<string, string>
		{
			["ipp:print-quality"]  = "5",
			["cups:fit-to-page"]   = "true",
			["windows:devmode"]    = "AAAA"
		};

		// Act
		PrintOptions options = new PrintOptions { VendorOptions = settings };
		settings["ipp:print-quality"] = "3";
		settings["ipp:finishings"] = "4";

		// Assert — later changes to the caller's dictionary change nothing
		Assert.Equal(3, options.VendorOptions.Count);
		Assert.Equal("5", options.VendorOptions["ipp:print-quality"]);
		Assert.False(options.VendorOptions.ContainsKey("ipp:finishings"));
	}

	[Theory]
	[InlineData("print-quality")]
	[InlineData("ipps:print-quality")]
	[InlineData("IPP:print-quality")]
	[InlineData("ipp:")]
	[InlineData("")]
	public void GivenVendorOptionKeyWithoutKnownPrefixAndNameWhenInitialisedThenThrowsArgumentException(string key)
	{
		// Arrange — keys no print system would read (provided by InlineData above)
		Dictionary<string, string> settings = new Dictionary<string, string> { [key] = "5" };

		// Act
		Func<PrintOptions> createOptions = () => new PrintOptions { VendorOptions = settings };

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(createOptions);
		Assert.Equal(nameof(PrintOptions.VendorOptions), exception.ParamName);
		Assert.Contains($"'{key}'", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenNullVendorOptionsWhenInitialisedThenThrowsArgumentNullException()
	{
		// Arrange
		IReadOnlyDictionary<string, string>? settings = null;

		// Act
		Func<PrintOptions> createOptions = () => new PrintOptions { VendorOptions = settings! }; // Deliberately null

		// Assert
		Assert.Throws<ArgumentNullException>(createOptions);
	}

	[Fact]
	public void GivenVendorOptionWithNullValueWhenInitialisedThenThrowsArgumentNullException()
	{
		// Arrange
		Dictionary<string, string> settings = new Dictionary<string, string> { ["ipp:print-quality"] = null! }; // Deliberately null

		// Act
		Func<PrintOptions> createOptions = () => new PrintOptions { VendorOptions = settings };

		// Assert
		Assert.Throws<ArgumentNullException>(createOptions);
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
