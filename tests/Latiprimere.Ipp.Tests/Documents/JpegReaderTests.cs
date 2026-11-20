using Latiprimere.Ipp.Documents;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

public sealed class JpegReaderTests
{
	[Fact]
	public void GivenTheQuadrantJpegWhenReadThenItsSizeAndColourAndItsOwnBytes()
	{
		// Arrange
		byte[] jpeg = ImageTestData.QuadrantsJpeg();

		// Act
		PageImage image = JpegReader.Read(jpeg);

		// Assert — the JPEG goes into the PDF as it is
		Assert.Equal((64, 48, 1), (image.Width, image.Height, image.Orientation));
		Assert.Equal((64, 48, PdfColorSpace.Rgb), (image.Image.Width, image.Image.Height, image.Image.ColorSpace));
		Assert.Equal(("DCTDecode", PdfStreamEncoding.None, null), (image.Image.Filter, image.Image.Encoding, image.Image.Decode));

		using MemoryStream written = new MemoryStream();
		image.Image.WriteData(written);
		Assert.Equal(jpeg, written.ToArray());
	}

	[Fact]
	public void GivenExifWithAnOrientationAndResolutionWhenReadThenTheyAreGivenBeforeJfifs()
	{
		// Arrange — EXIF's resolution wins over JFIF's density
		byte[] jpeg = ImageTestData.InsertSegment
		(
			ImageTestData.QuadrantsJpeg(),
			0xE1,
			[.. "Exif\0\0"u8, .. ImageTestData.Exif(6, 300)]
		);

		// Act
		PageImage image = JpegReader.Read(jpeg);

		// Assert
		Assert.Equal((6, 300.0, 300.0), (image.Orientation, image.DpiX, image.DpiY));
	}

	[Theory]
	[InlineData(1, 200, 200.0)]
	[InlineData(2, 100, 254.0)]
	[InlineData(0, 1, null)]
	public void GivenAJfifDensityWhenReadThenItIsInDotsPerInch(int unit, int density, double? expected)
	{
		// Arrange — per inch, per centimetre, or an aspect ratio only (provided by InlineData above)
		byte[] jfif = [.. "JFIF\0"u8, 1, 2, (byte)unit, 0, (byte)density, 0, (byte)density, 0, 0];
		byte[] jpeg = ImageTestData.InsertSegment(JpegReaderTests.WithoutJfif(ImageTestData.QuadrantsJpeg()), 0xE0, jfif);

		// Act
		PageImage image = JpegReader.Read(jpeg);

		// Assert
		Assert.Equal((expected, expected), (image.DpiX, image.DpiY));
	}

	[Theory]
	[InlineData(1, false, (int)PdfColorSpace.Gray, null)]
	[InlineData(4, false, (int)PdfColorSpace.Cmyk, null)]
	[InlineData(4, true, (int)PdfColorSpace.Cmyk, "[1 0 1 0 1 0 1 0]")]
	public void GivenComponentsWhenReadThenTheColourSpaceFollowsAndAdobesCmykIsInverted(int components, bool adobe, int expected, string? decode)
	{
		// Arrange — headers alone suffice, as the scan isn't read (provided by InlineData above)
		byte[] jpeg = JpegReaderTests.Headers(0xC2, 8, components, adobe);

		// Act
		PageImage image = JpegReader.Read(jpeg);

		// Assert
		Assert.Equal(((PdfColorSpace)expected, decode), (image.Image.ColorSpace, image.Image.Decode));
		Assert.Equal((40, 30), (image.Width, image.Height));
	}

	[Theory]
	[InlineData(0xC3, 8, 3, "lossless")]
	[InlineData(0xC9, 8, 3, "arithmetic")]
	[InlineData(0xC5, 8, 3, "hierarchical")]
	[InlineData(0xC1, 12, 3, "12 bits")]
	[InlineData(0xC0, 8, 2, "2 colour components")]
	public void GivenACodingPdfCantCarryWhenReadThenThrowsPrintException(int marker, int precision, int components, string expected)
	{
		// Arrange (provided by InlineData above)
		byte[] jpeg = JpegReaderTests.Headers((byte)marker, precision, components, adobe: false);

		// Act
		Action read = () => JpegReader.Read(jpeg);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(read);
		Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenAJpegDeclaringMoreThan300MillionPixelsWhenReadThenThrowsPrintException()
	{
		// Arrange — 20,000 × 20,000, never decoded here, but a PDF reader or printer would decode every pixel
		byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8, 0x4E, 0x20, 0x4E, 0x20, 3, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0, 0xFF, 0xDA, 0, 2];

		// Act
		Action read = () => JpegReader.Read(jpeg);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(read);
		Assert.Contains("300 million", exception.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "isn't a JPEG")]
	[InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x40, 0x4A }, "damaged")]
	[InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x02 }, "damaged")]
	[InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x08, 8, 0, 0, 0, 10, 3 }, "damaged")]
	public void GivenDataThatIsNotAWholeJpegWhenReadThenThrowsPrintException(byte[] data, string expected)
	{
		// Arrange — a PNG, a segment past the end, a scan before any frame, and a height of 0 (provided by
		// InlineData above)

		// Act
		Action read = () => JpegReader.Read(data);

		// Assert
		PrintException exception = Assert.Throws<PrintException>(read);
		Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Builds a JPEG's headers: start of image, Adobe's APP14 if asked, a 40 × 30 frame, and the start of a scan.
	/// </summary>
	/// <param name="marker">The frame's SOF marker.</param>
	/// <param name="precision">Its bits per sample.</param>
	/// <param name="components">Its components.</param>
	/// <param name="adobe">Whether to add Adobe's segment.</param>
	/// <returns>The bytes.</returns>
	private static byte[] Headers(byte marker, int precision, int components, bool adobe)
	{
		List<byte> bytes = [0xFF, 0xD8];

		if (adobe)
		{
			bytes.AddRange([0xFF, 0xEE, 0x00, 0x0E, .. "Adobe"u8, 0, 100, 0, 0, 0, 0, 2]);
		}

		int length = 8 + (3 * components);
		bytes.AddRange([0xFF, marker, 0, (byte)length, (byte)precision, 0, 30, 0, 40, (byte)components]);

		for (int component = 1; component <= components; component++)
		{
			bytes.AddRange([(byte)component, 0x11, 0]);
		}

		bytes.AddRange([0xFF, 0xDA, 0x00, 0x02]);

		return [.. bytes];
	}

	/// <summary>
	/// Removes a JPEG's JFIF segment, which follows its start-of-image marker, so another can be put in its place.
	/// </summary>
	/// <param name="jpeg">The JPEG.</param>
	/// <returns>The JPEG without it.</returns>
	private static byte[] WithoutJfif(byte[] jpeg)
	{
		Assert.Equal((0xFF, 0xE0), (jpeg[2], jpeg[3]));
		int length = (jpeg[4] << 8) | jpeg[5];

		return [.. jpeg.AsSpan(0, 2), .. jpeg.AsSpan(4 + length)];
	}
}
