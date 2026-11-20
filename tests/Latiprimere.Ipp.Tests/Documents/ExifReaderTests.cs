using Latiprimere.Ipp.Documents;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

public sealed class ExifReaderTests
{
	[Theory]
	[InlineData(1, true)]
	[InlineData(3, false)]
	[InlineData(6, true)]
	[InlineData(8, false)]
	public void GivenAnOrientationWhenReadThenItIsGiven(int orientation, bool littleEndian)
	{
		// Arrange — in Intel's and Motorola's byte order (provided by InlineData above)
		byte[] exif = ImageTestData.Exif(orientation, littleEndian: littleEndian);

		// Act
		ExifInfo info = ExifReader.Read(exif);

		// Assert
		Assert.Equal(new ExifInfo(orientation, null, null), info);
	}

	[Theory]
	[InlineData(2, 300, 300.0)]
	[InlineData(3, 118, 299.72)]
	public void GivenAResolutionWhenReadThenItIsInDotsPerInch(int unit, int resolution, double expected)
	{
		// Arrange — per inch, or per centimetre (provided by InlineData above)
		byte[] exif = ImageTestData.Exif(6, resolution, unit, littleEndian: false);

		// Act
		ExifInfo info = ExifReader.Read(exif);

		// Assert
		Assert.Equal(6, info.Orientation);
		Assert.Equal(expected, info.DpiX!.Value, 6);
		Assert.Equal(expected, info.DpiY!.Value, 6);
	}

	[Fact]
	public void GivenAResolutionWithoutAUnitWhenReadThenNoneIsGiven()
	{
		// Arrange — unit 1 says the numbers are only an aspect ratio
		byte[] exif = ImageTestData.Exif(1, 72, unit: 1);

		// Act
		ExifInfo info = ExifReader.Read(exif);

		// Assert
		Assert.Equal((null, null), (info.DpiX, info.DpiY));
	}

	[Theory]
	[InlineData(new byte[0])]
	[InlineData(new byte[] { (byte)'X', (byte)'X', 42, 0, 8, 0, 0, 0 })]
	[InlineData(new byte[] { (byte)'I', (byte)'I', 43, 0, 8, 0, 0, 0 })]
	[InlineData(new byte[] { (byte)'I', (byte)'I', 42, 0, 255, 255, 255, 255 })]
	[InlineData(new byte[] { (byte)'I', (byte)'I', 42, 0, 8, 0, 0, 0, 50, 0, 0x12, 0x01 })]
	public void GivenDataThatIsNotExifOrEndsEarlyWhenReadThenNothingIsGiven(byte[] data)
	{
		// Arrange — empty, a wrong byte order mark, a wrong magic number, a directory past the end, and a directory
		// whose entries run past the end (provided by InlineData above)

		// Act
		ExifInfo info = ExifReader.Read(data);

		// Assert
		Assert.Equal(ExifInfo.None, info);
	}

	[Fact]
	public void GivenAnOrientationOutOfRangeAndAResolutionPastTheEndWhenReadThenNeitherIsUsed()
	{
		// Arrange — orientation 9, and the resolution's offset moved past the data
		byte[] exif = ImageTestData.Exif(9, 300);
		exif[10 + 12 + 8] = 0xF0;

		// Act
		ExifInfo info = ExifReader.Read(exif);

		// Assert
		Assert.Equal(1, info.Orientation);
		Assert.Null(info.DpiX);
		Assert.Equal(300, info.DpiY);
	}
}
