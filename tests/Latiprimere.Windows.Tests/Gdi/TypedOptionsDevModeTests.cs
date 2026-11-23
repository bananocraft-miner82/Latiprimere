using System.Runtime.Versioning;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Interop;
using Xunit;

namespace Latiprimere.Windows.Tests.Gdi;

/// <summary>
/// The options added before 1.0 in the DEVMODE and the order pages print in (ADR 0056).
/// </summary>
public sealed class TypedOptionsDevModeTests
{
	[Theory]
	[InlineData(PrintQuality.Draft, DevModeConstants.QualityDraft)]
	[InlineData(PrintQuality.Normal, DevModeConstants.QualityMedium)]
	[InlineData(PrintQuality.High, DevModeConstants.QualityHigh)]
	public void GivenQualityWhenApplyThenSetsDmPrintQuality(PrintQuality quality, short expected)
	{
		// Arrange (provided by InlineData above)
		DevMode devMode = new DevMode();

		// Act
		DevModeEditor.Apply(ref devMode, new PrintOptions { Quality = quality }, paperId: null, binId: null, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(expected, devMode.PrintQuality);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldPrintQuality);
	}

	[Fact]
	public void GivenDefaultQualityWhenApplyThenLeavesDmPrintQualityAlone()
	{
		// Arrange
		DevMode devMode = new DevMode { PrintQuality = 600 };

		// Act
		DevModeEditor.Apply(ref devMode, PrintOptions.Default, paperId: null, binId: null, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(600, devMode.PrintQuality);
		Assert.Equal(0u, devMode.Fields & DevModeConstants.FieldPrintQuality);
	}

	[Fact]
	public void GivenCopiesNotCollatedWhenApplyThenSetsCollateFalse()
	{
		// Arrange
		DevMode devMode = new DevMode();

		// Act
		DevModeEditor.Apply(ref devMode, new PrintOptions { Copies = 2, Collate = false }, paperId: null, binId: null, isFirstPageLandscape: false);

		// Assert
		Assert.Equal(DevModeConstants.CollateFalse, devMode.Collate);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldCollate);
	}

	[Fact]
	public void GivenACustomSizeWhenApplyThenSetsDmPaperUserWithItsWidthAndLength()
	{
		// Arrange: a form name left set would override the size
		DevMode devMode = new DevMode { Fields = DevModeConstants.FieldFormName };

		// Act
		DevModeEditor.Apply(ref devMode, PrintOptions.Default, paperId: null, binId: null, isFirstPageLandscape: false, customPaper: (800, 1205));

		// Assert
		Assert.Equal((DevModeConstants.PaperUser, (short)800, (short)1205), (devMode.PaperSize, devMode.PaperWidth, devMode.PaperLength));
		uint sizeFields = DevModeConstants.FieldPaperSize | DevModeConstants.FieldPaperWidth | DevModeConstants.FieldPaperLength;
		Assert.Equal(sizeFields, devMode.Fields & sizeFields);
		Assert.Equal(0u, devMode.Fields & DevModeConstants.FieldFormName);
	}

	[Fact]
	public void GivenAMediaTypeWhenApplyThenSetsDmMediaType()
	{
		// Arrange
		DevMode devMode = new DevMode();

		// Act
		DevModeEditor.Apply(ref devMode, PrintOptions.Default, paperId: null, binId: null, isFirstPageLandscape: false, mediaTypeId: 258);

		// Assert
		Assert.Equal(258u, devMode.MediaType);
		Assert.NotEqual(0u, devMode.Fields & DevModeConstants.FieldMediaType);
	}

	[Theory]
	[InlineData(true, new[] { 1, 3, 1, 3 })]
	[InlineData(false, new[] { 1, 1, 3, 3 })]
	[SupportedOSPlatform("windows")] // Skipped elsewhere
	public void GivenChosenPagesAndCopiesWhenPrintOrderIsListedThenCollationDecidesTheOrder(bool collate, int[] expected)
	{
		// Arrange (provided by InlineData above): pages 2 and 4 of a document, two copies made here
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The GDI printer is Windows only.");
		int[] pages = [1, 3];

		// Act
		int[] order = GdiRasterPrinter.PrintOrder(pages, copies: 2, collate).ToArray();

		// Assert
		Assert.Equal(expected, order);
	}
}
