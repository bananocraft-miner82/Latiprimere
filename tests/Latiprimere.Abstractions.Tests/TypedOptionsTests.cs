using Xunit;

namespace Latiprimere.Abstractions.Tests;

/// <summary>
/// The options added before 1.0: page ranges, collation, quality, finishings, output bins, media types and custom
/// sizes (ADR 0056).
/// </summary>
public sealed class TypedOptionsTests
{
	[Theory]
	[InlineData(0, 1)]
	[InlineData(3, 2)]
	public void GivenFirstBelowOneOrLastBeforeFirstWhenPageRangeMadeThenThrowsArgumentOutOfRangeException(int first, int last)
	{
		// Arrange (provided by InlineData above)

		// Act
		Func<object> create = () => new PageRange(first, last);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(create);
	}

	[Fact]
	public void GivenRangesWhenWrittenAsTextThenTheyReadAsPeopleWriteThem()
	{
		// Arrange
		PageRange one = new PageRange(3, 3);
		PageRange many = new PageRange(2, 5);

		// Act
		(string single, string run) = (one.ToString(), many.ToString());

		// Assert
		Assert.Equal(("3", "2-5"), (single, run));
	}

	[Fact]
	public void GivenRangesInOrderWhenSetThenTheyAreKeptAsACopy()
	{
		// Arrange
		PageRange[] ranges = [new PageRange(1, 2), new PageRange(4, 4), new PageRange(6, 9)];

		// Act
		PrintOptions options = new PrintOptions { PageRanges = ranges };
		ranges[0] = new PageRange(7, 7);

		// Assert
		Assert.Equal([new PageRange(1, 2), new PageRange(4, 4), new PageRange(6, 9)], options.PageRanges);
	}

	[Theory]
	[InlineData(3, 5, 4, 6)]
	[InlineData(5, 6, 1, 2)]
	[InlineData(2, 3, 3, 4)]
	public void GivenRangesOverlappingOrOutOfOrderWhenSetThenThrowsArgumentExceptionListingThem(int first1, int last1, int first2, int last2)
	{
		// Arrange (provided by InlineData above)
		PageRange[] ranges = [new PageRange(first1, last1), new PageRange(first2, last2)];

		// Act
		Func<PrintOptions> create = () => new PrintOptions { PageRanges = ranges };

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(create);
		Assert.Equal(nameof(PrintOptions.PageRanges), exception.ParamName);
		Assert.Contains(ranges[1].ToString(), exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenADefaultRangeWhenSetThenThrowsArgumentException()
	{
		// Arrange: default(PageRange) has no pages
		PageRange[] ranges = [default];

		// Act
		Func<PrintOptions> create = () => new PrintOptions { PageRanges = ranges };

		// Assert
		Assert.Throws<ArgumentException>(create);
	}

	[Fact]
	public void GivenDefaultOptionsWhenTheNewSettingsAreReadThenEachIsThePrintersDefault()
	{
		// Arrange
		PrintOptions options = PrintOptions.Default;

		// Act and assert
		Assert.Empty(options.PageRanges);
		Assert.Null(options.Collate);
		Assert.Equal(PrintQuality.PrinterDefault, options.Quality);
		Assert.Empty(options.Finishings);
		Assert.Null(options.OutputBin);
		Assert.Null(options.MediaType);
	}

	[Fact]
	public void GivenRepeatedFinishingsWhenSetThenEachIsKeptOnce()
	{
		// Arrange
		Finishing[] finishings = [Finishing.StapleTopLeft, Finishing.PunchDualLeft, Finishing.StapleTopLeft];

		// Act
		PrintOptions options = new PrintOptions { Finishings = finishings };

		// Assert
		Assert.Equal([Finishing.StapleTopLeft, Finishing.PunchDualLeft], options.Finishings);
	}

	[Theory]
	[InlineData(102, 152, "custom_102x152mm_102x152mm", "102 \u00d7 152 mm")]
	[InlineData(79.5, 200.125, "custom_79.5x200.13mm_79.5x200.13mm", "79.5 \u00d7 200.13 mm")]
	public void GivenDimensionsWhenCustomSizeMadeThenItIsNamedByThem(double width, double height, string name, string displayName)
	{
		// Arrange (provided by InlineData above)

		// Act
		MediaSize size = MediaSize.Custom(width, height);

		// Assert
		Assert.Equal((name, displayName), (size.Name, size.DisplayName));
		Assert.Equal((Math.Round(width, 2, MidpointRounding.AwayFromZero), Math.Round(height, 2, MidpointRounding.AwayFromZero)), (size.WidthMillimeters, size.HeightMillimeters));
	}

	[Theory]
	[InlineData(0, 100)]
	[InlineData(100, -1)]
	[InlineData(double.NaN, 100)]
	[InlineData(100, double.PositiveInfinity)]
	public void GivenDimensionsNotAboveZeroWhenCustomSizeMadeThenThrowsArgumentOutOfRangeException(double width, double height)
	{
		// Arrange (provided by InlineData above)

		// Act
		Func<MediaSize> create = () => MediaSize.Custom(width, height);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(create);
	}

	[Theory]
	[InlineData(25.4, 25.4, true)]
	[InlineData(104, 3000, true)]
	[InlineData(80, 297, true)]
	[InlineData(104.2, 100, false)]
	[InlineData(80, 10, false)]
	public void GivenARollsRangeWhenAskedWhetherItContainsASizeThenOnlyThoseInsideAreTaken(double width, double height, bool expected)
	{
		// Arrange: a 4-inch roll printer's range
		MediaSizeRange range = new MediaSizeRange
		{
			MinWidthMillimeters  = 25.4,
			MaxWidthMillimeters  = 104,
			MinHeightMillimeters = 25.4,
			MaxHeightMillimeters = 3000,
			IsRoll               = true
		};

		// Act
		bool contains = range.Contains(MediaSize.Custom(width, height));

		// Assert
		Assert.Equal(expected, contains);
	}
}
