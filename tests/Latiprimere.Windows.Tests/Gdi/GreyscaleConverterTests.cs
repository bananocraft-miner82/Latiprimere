using Latiprimere.Windows.Gdi;
using Xunit;

namespace Latiprimere.Windows.Tests.Gdi;

public sealed class GreyscaleConverterTests
{
	[Theory]
	[InlineData(255, 255, 255, 255)] // White stays white
	[InlineData(0, 0, 0, 0)]         // Black stays black
	[InlineData(128, 128, 128, 128)] // Grey stays the same grey
	[InlineData(0, 0, 255, 77)]      // Red: 0.299 of full
	[InlineData(0, 255, 0, 149)]     // Green: 0.587, as 150/256
	[InlineData(255, 0, 0, 29)]      // Blue: 0.114
	public void GivenPixelWhenToGreyThenEveryChannelBecomesItsLuma(byte blue, byte green, byte red, byte expected)
	{
		// Arrange — one pixel in the rasteriser's blue, green, red, unused order (provided by InlineData above)
		byte[] pixels = [blue, green, red, 0xAB];

		// Act
		GreyscaleConverter.ToGrey(pixels);

		// Assert — the unused byte is left alone
		Assert.Equal(new byte[] { expected, expected, expected, 0xAB }, pixels);
	}

	[Fact]
	public void GivenBandWhenToGreyThenEveryPixelHasNoColourLeft()
	{
		// Arrange — a row of colours
		byte[] pixels =
		[
			10, 200, 30, 0,
			250, 5, 90, 0,
			0, 128, 255, 0
		];

		// Act
		GreyscaleConverter.ToGrey(pixels);

		// Assert
		for (int offset = 0; offset < pixels.Length; offset += 4)
		{
			Assert.Equal(pixels[offset], pixels[offset + 1]);
			Assert.Equal(pixels[offset], pixels[offset + 2]);
		}
	}

	[Fact]
	public void GivenSpanShorterThanBufferWhenToGreyThenBytesAfterItAreUnchanged()
	{
		// Arrange — the last band of a page uses only the start of the shared buffer
		byte[] buffer = [0, 0, 255, 0, 0, 0, 255, 0];

		// Act
		GreyscaleConverter.ToGrey(buffer.AsSpan(0, 4));

		// Assert
		Assert.Equal(new byte[] { 77, 77, 77, 0, 0, 0, 255, 0 }, buffer);
	}

	[Fact]
	public void GivenPartialPixelAtTheEndWhenToGreyThenItIsLeftUnchanged()
	{
		// Arrange
		byte[] pixels = [0, 0, 255, 0, 9, 9];

		// Act
		GreyscaleConverter.ToGrey(pixels);

		// Assert
		Assert.Equal(new byte[] { 77, 77, 77, 0, 9, 9 }, pixels);
	}
}
