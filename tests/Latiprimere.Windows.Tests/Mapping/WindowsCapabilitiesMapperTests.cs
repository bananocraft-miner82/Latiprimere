using Latiprimere.Windows.Mapping;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Mapping;

public sealed class WindowsCapabilitiesMapperTests
{
	[Fact]
	public void GivenColourDuplexPrinterWhenMappedThenReadsAllCoreCapabilities()
	{
		// Arrange — values as an inkjet driver reports them (DMPAPER 1 = Letter, 9 = A4; DMBIN 7 = auto)
		WindowsCapabilitiesRecord record = new WindowsCapabilitiesRecord
		{
			Papers = new[]
			{
				WindowsCapabilitiesMapperTests.Paper(1, "Letter", 2159, 2794),
				WindowsCapabilitiesMapperTests.Paper(9, "A4", 2100, 2970),
			},
			Bins = new[]
			{
				new WindowsBinRecord { Id = 7, Name = "Automatically Select" },
				new WindowsBinRecord { Id = 4, Name = "Manual Feed" },
			},
			CanDuplex         = true,
			IsColorDevice     = true,
			MaxCopies         = 99,
			CanPrintLandscape = true,
			DefaultPaperId    = 9,
			DefaultDuplex     = 2,
			DefaultColor      = 2
		};

		// Act
		PrinterCapabilities capabilities = WindowsCapabilitiesMapper.ToCapabilities(record);

		// Assert
		Assert.Equal
		(
			new[] { "na_letter_8.5x11in", "iso_a4_210x297mm" },
			capabilities.MediaSizes.Select(size => size.Name)
		);

		Assert.Equal("A4", capabilities.DefaultMediaSize?.DisplayName);

		Assert.Equal
		(
			new[] { DuplexMode.OneSided, DuplexMode.LongEdge, DuplexMode.ShortEdge },
			capabilities.DuplexModes
		);

		Assert.Equal(DuplexMode.LongEdge, capabilities.DefaultDuplexMode);
		Assert.Equal(new[] { ColorMode.Monochrome, ColorMode.Color }, capabilities.ColorModes);
		Assert.Equal(ColorMode.Color, capabilities.DefaultColorMode);
		Assert.Equal(99, capabilities.MaxCopies);
		Assert.Equal(new[] { PageOrientation.Portrait, PageOrientation.Landscape }, capabilities.Orientations);
		Assert.Equal(new[] { "7", "4" }, capabilities.InputTrays.Select(tray => tray.Id));
		Assert.Equal("Manual Feed", capabilities.InputTrays[1].Name);
	}

	[Theory]
	[InlineData(1, DuplexMode.OneSided)]
	[InlineData(2, DuplexMode.LongEdge)]
	[InlineData(3, DuplexMode.ShortEdge)]
	[InlineData(0, null)]
	[InlineData(4, null)]
	public void GivenDriverDuplexDefaultWhenMappedThenMapsDefaultDuplexMode(short defaultDuplex, DuplexMode? expected)
	{
		// Arrange — DEVMODE's dmDuplex (DMDUP_SIMPLEX, _VERTICAL, _HORIZONTAL, or unset or unknown) and the mode
		// expected (provided by InlineData above)
		WindowsCapabilitiesRecord record = new WindowsCapabilitiesRecord { CanDuplex = true, DefaultDuplex = defaultDuplex };

		// Act
		PrinterCapabilities capabilities = WindowsCapabilitiesMapper.ToCapabilities(record);

		// Assert
		Assert.Equal(expected, capabilities.DefaultDuplexMode);
	}

	[Theory]
	[InlineData(1, ColorMode.Monochrome)]
	[InlineData(2, ColorMode.Color)]
	[InlineData(0, null)]
	[InlineData(3, null)]
	public void GivenDriverColourDefaultWhenMappedThenMapsDefaultColourMode(short defaultColor, ColorMode? expected)
	{
		// Arrange — DEVMODE's dmColor (DMCOLOR_MONOCHROME, _COLOR, or unset or unknown) and the mode expected
		// (provided by InlineData above)
		WindowsCapabilitiesRecord record = new WindowsCapabilitiesRecord { IsColorDevice = true, DefaultColor = defaultColor };

		// Act
		PrinterCapabilities capabilities = WindowsCapabilitiesMapper.ToCapabilities(record);

		// Assert
		Assert.Equal(expected, capabilities.DefaultColorMode);
	}

	[Fact]
	public void GivenSimplePrinterWhenMappedThenOffersOnlyOneSidedMonochromePortrait()
	{
		// Arrange — a driver that can't duplex, colour or rotate, and sets no defaults
		WindowsCapabilitiesRecord record = new WindowsCapabilitiesRecord();

		// Act
		PrinterCapabilities capabilities = WindowsCapabilitiesMapper.ToCapabilities(record);

		// Assert
		Assert.Equal(new[] { DuplexMode.OneSided }, capabilities.DuplexModes);
		Assert.Equal(new[] { ColorMode.Monochrome }, capabilities.ColorModes);
		Assert.Equal(new[] { PageOrientation.Portrait }, capabilities.Orientations);
		Assert.Null(capabilities.DefaultMediaSize);
		Assert.Null(capabilities.DefaultDuplexMode);
		Assert.Null(capabilities.DefaultColorMode);
		Assert.Null(capabilities.MaxCopies);
	}

	[Fact]
	public void GivenRotatedAndUnusualSizesWhenMappedThenNamesThemInPortraitAndDropsDuplicates()
	{
		// Arrange — "Letter Rotated" is Letter in landscape; 100 x 200 mm has no standard name
		WindowsCapabilitiesRecord record = new WindowsCapabilitiesRecord
		{
			Papers = new[]
			{
				WindowsCapabilitiesMapperTests.Paper(1, "Letter", 2159, 2794),
				WindowsCapabilitiesMapperTests.Paper(75, "Letter Rotated", 2794, 2159),
				WindowsCapabilitiesMapperTests.Paper(256, "Label", 2000, 1000),
				WindowsCapabilitiesMapperTests.Paper(70, "A6", 1050, 1480),
			}
		};

		// Act
		PrinterCapabilities capabilities = WindowsCapabilitiesMapper.ToCapabilities(record);

		// Assert
		Assert.Equal
		(
			new[] { "na_letter_8.5x11in", "custom_100x200mm_100x200mm", "iso_a6_105x148mm" },
			capabilities.MediaSizes.Select(size => size.Name)
		);

		MediaSize label = capabilities.MediaSizes[1];
		Assert.Equal("Label", label.DisplayName);
		Assert.Equal(100.0, label.WidthMillimetres);
		Assert.Equal(200.0, label.HeightMillimetres);
	}

	/// <summary>
	/// Creates a driver paper size.
	/// </summary>
	/// <param name="id">The DMPAPER_* identifier.</param>
	/// <param name="name">The driver's name.</param>
	/// <param name="width">The width in tenths of a millimetre.</param>
	/// <param name="height">The height in tenths of a millimetre.</param>
	/// <returns>The paper.</returns>
	private static WindowsPaperRecord Paper(short id, string name, int width, int height)
	{
		return new WindowsPaperRecord
		{
			Id                       = id,
			Name                     = name,
			WidthTenthsOfMillimetre  = width,
			HeightTenthsOfMillimetre = height
		};
	}
}
