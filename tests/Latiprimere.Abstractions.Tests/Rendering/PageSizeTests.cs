using Latiprimere.Rendering;
using Xunit;

namespace Latiprimere.Abstractions.Tests.Rendering;

public sealed class PageSizeTests
{
	[Theory]
	[InlineData(842.0, 595.0, true)]
	[InlineData(595.0, 842.0, false)]
	[InlineData(600.0, 600.0, false)]
	public void GivenPageDimensionsWhenIsLandscapeReadThenTrueOnlyWhenWiderThanTall(double width, double height, bool expected)
	{
		// Arrange — A4 landscape, A4 portrait, and a square page (provided by InlineData above)
		PageSize size = new PageSize(width, height);

		// Act
		bool isLandscape = size.IsLandscape;

		// Assert
		Assert.Equal(expected, isLandscape);
	}

	[Fact]
	public void GivenPointsPerInchWhenReadThenIsSeventyTwo()
	{
		// Arrange
		double expected = 72.0;

		// Act
		double pointsPerInch = PageSize.PointsPerInch;

		// Assert
		Assert.Equal(expected, pointsPerInch);
	}
}
