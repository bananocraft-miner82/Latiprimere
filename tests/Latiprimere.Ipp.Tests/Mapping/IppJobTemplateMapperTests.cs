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
