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

	[Fact]
	public void GivenGreyWhenCreateThenStrideIsOneBytePerPixelAndTheFormatIsKept()
	{
		// Arrange
		const int pageWidth = 4961; // A4 width at 600 DPI

		// Act
		RasterBand band = RasterBand.Create(pageWidth, 7016, 0, 10, RasterPixelFormat.Gray8);

		// Assert
		Assert.Equal((pageWidth, RasterPixelFormat.Gray8, 1), (band.Stride, band.PixelFormat, band.BytesPerPixel));
		Assert.Equal(pageWidth * 10L, band.ByteCount);
	}

	[Fact]
	public void GivenBandMadeWithoutFormatWhenReadThenItIsColourAtFourBytesPerPixel()
	{
		// Arrange
		RasterBand band = new RasterBand(PageWidth: 10, PageHeight: 20, Top: 0, Height: 5, Stride: 40);

		// Act
		(RasterPixelFormat format, int bytesPerPixel) = (band.PixelFormat, band.BytesPerPixel);

		// Assert
		Assert.Equal((RasterPixelFormat.Bgra32, 4), (format, bytesPerPixel));
	}

	[Fact]
	public void GivenFormatThisVersionDoesNotKnowWhenBytesPerPixelReadThenThrowsNotSupportedException()
	{
		// Arrange: a value a later version may add
		RasterBand band = RasterBand.Create(10, 20, 0, 5) with { PixelFormat = (RasterPixelFormat)99 };

		// Act
		Func<object?> read = () => band.BytesPerPixel;

		// Assert
		Assert.Throws<NotSupportedException>(read);
	}
}
