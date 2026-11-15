using Latiprimere.Rendering;
using Xunit;

namespace Latiprimere.Abstractions.Tests.Rendering;

public sealed class RasterBandTests
{
	[Fact]
	public void GivenPageWidthWhenCreateThenStrideIsFourBytesPerPixel()
	{
		// Arrange
		const int pageWidth = 4961; // A4 width at 600 DPI

		// Act
		RasterBand band = RasterBand.Create(pageWidth, 7016, 100, 50);

		// Assert
		Assert.Equal(pageWidth * 4, band.Stride);
		Assert.Equal(4961, band.PageWidth);
		Assert.Equal(7016, band.PageHeight);
		Assert.Equal(100, band.Top);
		Assert.Equal(50, band.Height);
	}

	[Fact]
	public void GivenBandWhenByteCountReadThenIsStrideTimesHeight()
	{
		// Arrange
		RasterBand band = new RasterBand(PageWidth: 10, PageHeight: 20, Top: 0, Height: 5, Stride: 48);

		// Act
		long byteCount = band.ByteCount;

		// Assert
		Assert.Equal(240L, byteCount);
	}

	[Fact]
	public void GivenBandLargerThanTwoGigabytesWhenByteCountReadThenDoesNotOverflow()
	{
		// Arrange — int arithmetic would overflow at this size
		RasterBand band = new RasterBand(PageWidth: 40000, PageHeight: 20000, Top: 0, Height: 20000, Stride: 160000);

		// Act
		long byteCount = band.ByteCount;

		// Assert
		Assert.Equal(3_200_000_000L, byteCount);
	}
}
