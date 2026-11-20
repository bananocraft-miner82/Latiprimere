using Latiprimere.Ipp.Mapping;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class IppJobTemplateMapperTests
{
	[Fact]
	public void GivenDefaultOptionsWhenToJobTemplateAttributesThenSendsNothing()
	{
		// Arrange
		PrintOptions options = PrintOptions.Default;

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		Assert.Null(attributes.Copies);
		Assert.Null(attributes.Sides);
		Assert.Null(attributes.OrientationRequested);
		Assert.Null(attributes.PrintColorMode);
		Assert.Null(attributes.Media);
		Assert.Null(attributes.MediaCol);
	}

	[Theory]
	[InlineData(ColorMode.Monochrome, "monochrome")]
	[InlineData(ColorMode.Color, "color")]
	public void GivenColourModeWhenToJobTemplateAttributesThenSendsPrintColorMode(ColorMode colorMode, string expectedKeyword)
	{
		// Arrange — each colour mode and its IPP print-color-mode keyword (provided by InlineData above)
		PrintOptions options = new PrintOptions { ColorMode = colorMode };

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		Assert.Equal(expectedKeyword, attributes.PrintColorMode?.Value.Value);
	}

	[Fact]
	public void GivenPrinterDefaultColourWhenToPrintColorModeThenReturnsNull()
	{
		// Arrange
		ColorMode colorMode = ColorMode.PrinterDefault;

		// Act
		PrintColorMode? printColorMode = IppJobTemplateMapper.ToPrintColorMode(colorMode);

		// Assert
		Assert.Null(printColorMode);
	}

	[Fact]
	public void GivenSizeWithoutTrayWhenToJobTemplateAttributesThenSendsMediaKeyword()
	{
		// Arrange
		PrintOptions options = new PrintOptions
		{
			MediaSize = new MediaSize
			{
				Name              = "iso_a4_210x297mm",
				DisplayName       = "A4",
				WidthMillimeters  = 210,
				HeightMillimeters = 297
			}

		};

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		Assert.Equal("iso_a4_210x297mm", attributes.Media?.Value.Value);
		Assert.True(attributes.Media?.Value.IsKeyword);
		Assert.Null(attributes.MediaCol);
	}

	[Fact]
	public void GivenTrayWithoutSizeWhenToJobTemplateAttributesThenSendsMediaColWithSourceOnly()
	{
		// Arrange
		PrintOptions options = new PrintOptions { InputTray = new InputTray { Id = "by-pass-tray", Name = "Bypass Tray" } };

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		MediaCol mediaCol = Assert.IsType<MediaCol>(attributes.MediaCol?.Value);
		Assert.Equal("by-pass-tray", mediaCol.MediaSource?.Value.Value);
		Assert.Null(mediaCol.MediaSize);
		Assert.Null(attributes.Media);
	}

	[Theory]
	[InlineData("iso_a4_210x297mm", 210.0, 297.0, 21000, 29700)]
	[InlineData("na_letter_8.5x11in", 215.9, 279.4, 21590, 27940)]
	[InlineData("na_number-10_4.125x9.5in", 104.78, 241.3, 10478, 24130)]
	public void GivenTrayAndSizeWhenToJobTemplateAttributesThenSendsBothInMediaColInHundredthsOfMillimeters
	(
		string name,
		double width,
		double height,
		int expectedX,
		int expectedY
	)
	{
		// Arrange — sizes in millimetres, as the capabilities report them (provided by InlineData above)
		PrintOptions options = new PrintOptions
		{
			MediaSize = new MediaSize { Name = name, DisplayName = name, WidthMillimeters = width, HeightMillimeters = height },
			InputTray = new InputTray { Id = "tray-2", Name = "Tray 2" }
		};

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert — media and media-col can't both be sent
		MediaCol mediaCol = Assert.IsType<MediaCol>(attributes.MediaCol?.Value);
		Assert.Equal("tray-2", mediaCol.MediaSource?.Value.Value);
		Assert.Equal(expectedX, mediaCol.MediaSize?.Value.XDimension?.Value.Lower);
		Assert.Equal(expectedX, mediaCol.MediaSize?.Value.XDimension?.Value.Upper);
		Assert.Equal(expectedY, mediaCol.MediaSize?.Value.YDimension?.Value.Lower);
		Assert.Equal(expectedY, mediaCol.MediaSize?.Value.YDimension?.Value.Upper);
		Assert.Null(attributes.Media);
	}

	[Fact]
	public void GivenChosenOptionsWhenToJobTemplateAttributesThenSendsEach()
	{
		// Arrange
		PrintOptions options = new PrintOptions
		{
			Copies      = 3,
			Duplex      = DuplexMode.LongEdge,
			Orientation = PageOrientation.Landscape
		};

		// Act
		JobTemplateAttributes attributes = IppJobTemplateMapper.ToJobTemplateAttributes(options);

		// Assert
		Assert.Equal(3, attributes.Copies?.Value);
		Assert.Equal(Sides.TwoSidedLongEdge, attributes.Sides?.Value);
		Assert.Equal(Orientation.Landscape, attributes.OrientationRequested?.Value);
	}

	[Theory]
	[InlineData(DuplexMode.OneSided, "one-sided")]
	[InlineData(DuplexMode.LongEdge, "two-sided-long-edge")]
	[InlineData(DuplexMode.ShortEdge, "two-sided-short-edge")]
	public void GivenDuplexModeWhenToSidesThenReturnsIppKeyword(DuplexMode duplex, string expectedKeyword)
	{
		// Arrange — each duplex mode and its IPP sides keyword (provided by InlineData above)
		DuplexMode mode = duplex;

		// Act
		Sides? sides = IppJobTemplateMapper.ToSides(mode);

		// Assert
		Assert.Equal(expectedKeyword, sides?.Value);
	}

	[Fact]
	public void GivenPrinterDefaultDuplexWhenToSidesThenReturnsNull()
	{
		// Arrange
		DuplexMode mode = DuplexMode.PrinterDefault;

		// Act
		Sides? sides = IppJobTemplateMapper.ToSides(mode);

		// Assert
		Assert.Null(sides);
	}

	[Theory]
	[InlineData(PageOrientation.Portrait, Orientation.Portrait)]
	[InlineData(PageOrientation.Landscape, Orientation.Landscape)]
	public void GivenPageOrientationWhenToOrientationThenReturnsIppEnum
	(
		PageOrientation orientation,
		Orientation expectedOrientation
	)
	{
		// Arrange — each orientation and its IPP orientation-requested value (provided by InlineData above)
		PageOrientation pageOrientation = orientation;

		// Act
		Orientation? mappedOrientation = IppJobTemplateMapper.ToOrientation(pageOrientation);

		// Assert
		Assert.Equal(expectedOrientation, mappedOrientation);
	}

	[Fact]
	public void GivenPrinterDefaultOrientationWhenToOrientationThenReturnsNull()
	{
		// Arrange
		PageOrientation pageOrientation = PageOrientation.PrinterDefault;

		// Act
		Orientation? mappedOrientation = IppJobTemplateMapper.ToOrientation(pageOrientation);

		// Assert
		Assert.Null(mappedOrientation);
	}
}
