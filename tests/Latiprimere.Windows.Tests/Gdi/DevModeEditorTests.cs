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
		DevModeEditor.Apply(ref devMode, PrintOptions.Default, isFirstPageLandscape: false);

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
		DevModeEditor.Apply(ref devMode, options, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(3, devMode.Copies);
		Assert.Equal(DevModeConstants.CollateTrue, devMode.Collate);
		Assert.Equal(DevModeConstants.DuplexHorizontal, devMode.Duplex);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldCollate);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldDuplex);
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
