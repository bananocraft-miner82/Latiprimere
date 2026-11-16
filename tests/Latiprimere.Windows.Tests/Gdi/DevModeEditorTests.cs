using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Interop;
using Xunit;

namespace Latiprimere.Windows.Tests.Gdi;

public sealed class DevModeEditorTests
{
	[Fact]
	public void GivenDefaultOptionsWhenApplyThenSetsOneCopyAndPortraitAndLeavesDuplexAlone()
	{
		// Arrange
		DevMode devMode = new DevMode { Duplex = DevModeConstants.DuplexVertical };

		// Act
		DevModeEditor.Apply(ref devMode, PrintOptions.Default, paperId: null, binId: null, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(1, devMode.Copies);
		Assert.Equal(DevModeConstants.OrientationPortrait, devMode.Orientation);
		Assert.Equal(DevModeConstants.DuplexVertical, devMode.Duplex);
		Assert.Equal(DevModeConstants.FieldCopies | DevModeConstants.FieldOrientation, devMode.Fields);
	}

	[Fact]
	public void GivenCopiesAndDuplexWhenApplyThenSetsThemCollatedWithFieldFlags()
	{
		// Arrange
		DevMode devMode = new DevMode();
		PrintOptions options = new PrintOptions { Copies = 3, Duplex = DuplexMode.ShortEdge };

		// Act
		DevModeEditor.Apply(ref devMode, options, paperId: null, binId: null, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(3, devMode.Copies);
		Assert.Equal(DevModeConstants.CollateTrue, devMode.Collate);
		Assert.Equal(DevModeConstants.DuplexHorizontal, devMode.Duplex);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldCollate);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldDuplex);
	}

	[Fact]
	public void GivenColourChosenWhenApplyThenSetsItWithFieldFlag()
	{
		// Arrange
		DevMode devMode = new DevMode { Color = DevModeConstants.ColorColor };
		PrintOptions options = new PrintOptions { ColorMode = ColorMode.Monochrome };

		// Act
		DevModeEditor.Apply(ref devMode, options, paperId: null, binId: null, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(DevModeConstants.ColorMonochrome, devMode.Color);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldColor);
	}

	[Fact]
	public void GivenPaperChosenWhenApplyThenSetsItAndClearsWhatWouldOverrideIt()
	{
		// Arrange — a driver default with a custom length, width and form name set, all of which beat dmPaperSize
		DevMode devMode = new DevMode
		{
			PaperSize   = 1,
			PaperLength = 1000,
			PaperWidth  = 500,
			Fields      = DevModeConstants.FieldPaperSize | DevModeConstants.FieldPaperLength
						| DevModeConstants.FieldPaperWidth | DevModeConstants.FieldFormName
		};

		// Act — DMPAPER 9 is A4
		DevModeEditor.Apply(ref devMode, PrintOptions.Default, paperId: 9, binId: null, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(9, devMode.PaperSize);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldPaperSize);
		Assert.Equal(0u, devMode.Fields & DevModeConstants.FieldPaperLength);
		Assert.Equal(0u, devMode.Fields & DevModeConstants.FieldPaperWidth);
		Assert.Equal(0u, devMode.Fields & DevModeConstants.FieldFormName);
	}

	[Fact]
	public void GivenTrayChosenWhenApplyThenSetsDefaultSourceWithFieldFlag()
	{
		// Arrange — DMBIN 257 is a driver's own tray number
		DevMode devMode = new DevMode { DefaultSource = 7 };

		// Act
		DevModeEditor.Apply(ref devMode, PrintOptions.Default, paperId: null, binId: 257, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(257, devMode.DefaultSource);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldDefaultSource);
	}

	[Fact]
	public void GivenNothingChosenWhenApplyThenLeavesColourPaperAndTrayAlone()
	{
		// Arrange — the driver's own defaults
		DevMode devMode = new DevMode { Color = DevModeConstants.ColorColor, PaperSize = 1, DefaultSource = 7 };

		// Act
		DevModeEditor.Apply(ref devMode, PrintOptions.Default, paperId: null, binId: null, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(DevModeConstants.ColorColor, devMode.Color);
		Assert.Equal(1, devMode.PaperSize);
		Assert.Equal(7, devMode.DefaultSource);
		Assert.Equal(0u, devMode.Fields & (DevModeConstants.FieldColor | DevModeConstants.FieldPaperSize | DevModeConstants.FieldDefaultSource));
	}

	[Theory]
	[InlineData(ColorMode.Monochrome, DevModeConstants.ColorMonochrome)]
	[InlineData(ColorMode.Color, DevModeConstants.ColorColor)]
	public void GivenColourModeWhenToColorThenReturnsDevModeValue(ColorMode colorMode, short expectedColor)
	{
		// Arrange — each colour mode and its DMCOLOR_* value (provided by InlineData above)
		ColorMode mode = colorMode;

		// Act
		short? devModeColor = DevModeEditor.ToColor(mode);

		// Assert
		Assert.Equal(expectedColor, devModeColor);
	}

	[Fact]
	public void GivenPrinterDefaultColourWhenToColorThenReturnsNull()
	{
		// Arrange
		ColorMode mode = ColorMode.PrinterDefault;

		// Act
		short? devModeColor = DevModeEditor.ToColor(mode);

		// Assert
		Assert.Null(devModeColor);
	}

	[Fact]
	public void GivenDriverKeptTheCopiesWhenTakeOverCopiesThenDriverMakesThem()
	{
		// Arrange — the merged DEVMODE still asks for all 3
		DevMode devMode = new DevMode { Copies = 3 };

		// Act
		int renderedCopies = DevModeEditor.TakeOverCopies(ref devMode, 3);

		// Assert
		Assert.Equal(1, renderedCopies);
		Assert.Equal(3, devMode.Copies);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(99)]
	public void GivenDriverReducedTheCopiesWhenTakeOverCopiesThenRendersThemAllWithDriverAtOne(short mergedCopies)
	{
		// Arrange — mergedCopies is what a driver that can't make 150 left (provided by InlineData above): reset to
		// 1, or capped at its maximum of 99
		DevMode devMode = new DevMode { Copies = mergedCopies };

		// Act
		int renderedCopies = DevModeEditor.TakeOverCopies(ref devMode, 150);

		// Assert — 150 printed in all, not 150 × 99
		Assert.Equal(150, renderedCopies);
		Assert.Equal(1, devMode.Copies);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldCopies);
	}

	[Theory]
	[InlineData(PageOrientation.PrinterDefault, false, DevModeConstants.OrientationPortrait)]
	[InlineData(PageOrientation.PrinterDefault, true, DevModeConstants.OrientationLandscape)]
	[InlineData(PageOrientation.Portrait, true, DevModeConstants.OrientationPortrait)]
	[InlineData(PageOrientation.Landscape, false, DevModeConstants.OrientationLandscape)]
	public void GivenOrientationChoiceWhenToOrientationThenFollowsChoiceOrFirstPage
	(
		PageOrientation orientation,
		bool isFirstPageLandscape,
		short expectedOrientation
	)
	{
		// Arrange — the requested orientation and the first page's shape (provided by InlineData above)
		PageOrientation requested = orientation;

		// Act
		short devModeOrientation = DevModeEditor.ToOrientation(requested, isFirstPageLandscape);

		// Assert
		Assert.Equal(expectedOrientation, devModeOrientation);
	}

	[Theory]
	[InlineData(DuplexMode.OneSided, DevModeConstants.DuplexSimplex)]
	[InlineData(DuplexMode.LongEdge, DevModeConstants.DuplexVertical)]
	[InlineData(DuplexMode.ShortEdge, DevModeConstants.DuplexHorizontal)]
	public void GivenDuplexModeWhenToDuplexThenReturnsDevModeValue(DuplexMode duplex, short expectedDuplex)
	{
		// Arrange — each duplex mode and its DMDUP_* value (provided by InlineData above)
		DuplexMode mode = duplex;

		// Act
		short? devModeDuplex = DevModeEditor.ToDuplex(mode);

		// Assert
		Assert.Equal(expectedDuplex, devModeDuplex);
	}
}
