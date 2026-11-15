using Latiprimere.Rendering;
using Latiprimere.Windows.Gdi;
using Xunit;

namespace Latiprimere.Windows.Tests.Gdi;

public sealed class PageLayoutTests
{
	/// <summary>A4 paper at 600 DPI with quarter-inch margins, like Microsoft Print to PDF.</summary>
	private static readonly PrintableArea A4At600Dpi = new PrintableArea(600, 600, 4960, 7016, 150, 150);

	/// <summary>An A4 page in points.</summary>
	private static readonly PageSize A4Page = new PageSize(595, 842);

	[Fact]
	public void GivenA4PageOnA4PaperWhenFitThenActualSizeAlignedWithPaperEdges()
	{
		// Arrange
		PrintableArea area = PageLayoutTests.A4At600Dpi;

		// Act
		PageLayout layout = PageLayout.Fit(PageLayoutTests.A4Page, area, GdiRenderSettings.UnlimitedRenderDpi);

		// Assert — 842 pt is 7016.7 px, a fraction taller than the 7016 px paper, so it shrinks by a hair; the width
		// stays 4958 px, centring leaves 1 px each side, and the margin shifts it left
		Assert.Equal(4958, layout.Width);
		Assert.Equal(7016, layout.Height);
		Assert.Equal(1 - 150, layout.X);
		Assert.Equal(layout.Width, layout.RenderWidth);
		Assert.Equal(layout.Height, layout.RenderHeight);
	}

	[Fact]
	public void GivenPageLargerThanPaperWhenFitThenShrinksToFitKeepingAspectRatio()
	{
		// Arrange — an A3 page (842 x 1191 pt) on A4 paper
		PageSize a3Page = new PageSize(842, 1191);

		// Act
		PageLayout layout = PageLayout.Fit(a3Page, PageLayoutTests.A4At600Dpi, GdiRenderSettings.UnlimitedRenderDpi);

		// Assert
		Assert.True(layout.Width <= PageLayoutTests.A4At600Dpi.PaperWidth);
		Assert.True(layout.Height <= PageLayoutTests.A4At600Dpi.PaperHeight);
		Assert.Equal(842.0 / 1191.0, (double)layout.Width / layout.Height, 0.002);
	}

	[Fact]
	public void GivenPageSmallerThanPaperWhenFitThenKeepsActualSizeAndCentres()
	{
		// Arrange — an A6 page (298 x 420 pt)
		PageSize a6Page = new PageSize(298, 420);

		// Act
		PageLayout layout = PageLayout.Fit(a6Page, PageLayoutTests.A4At600Dpi, GdiRenderSettings.UnlimitedRenderDpi);

		// Assert
		Assert.Equal(2483, layout.Width);
		Assert.Equal(((4960 - 2483) / 2) - 150, layout.X);
	}

	[Fact]
	public void GivenRenderCapBelowDeviceResolutionWhenFitThenRendersSmallerButDrawsFullSize()
	{
		// Arrange
		const int maxRenderDpi = 300;

		// Act
		PageLayout layout = PageLayout.Fit(PageLayoutTests.A4Page, PageLayoutTests.A4At600Dpi, maxRenderDpi);

		// Assert
		Assert.Equal(4958, layout.Width);
		Assert.Equal(2479, layout.RenderWidth);
		Assert.Equal(3508, layout.RenderHeight);
	}

	[Fact]
	public void GivenBandBudgetWhenGetRowsPerBandThenFitsBudgetAndPage()
	{
		// Arrange
		PageLayout layout = PageLayout.Fit
		(
			PageLayoutTests.A4Page,
			PageLayoutTests.A4At600Dpi,
			GdiRenderSettings.UnlimitedRenderDpi
		);

		// Act
		int rows = layout.GetRowsPerBand(GdiRenderSettings.MaxBandBytes);

		// Assert
		Assert.True((long)rows * layout.RenderWidth * RasterBand.BytesPerPixel <= GdiRenderSettings.MaxBandBytes);
		Assert.InRange(rows, 1, layout.RenderHeight);
	}

	[Theory]
	[InlineData(1L)]
	[InlineData(long.MaxValue)]
	public void GivenExtremeBudgetWhenGetRowsPerBandThenClampsToOneRowOrWholePage(long maxBandBytes)
	{
		// Arrange — a budget smaller than one row, or larger than the page (provided by InlineData above)
		PageLayout layout = new PageLayout(0, 0, 100, 200, 100, 200);

		// Act
		int rows = layout.GetRowsPerBand(maxBandBytes);

		// Assert
		Assert.Equal(maxBandBytes == 1 ? 1 : 200, rows);
	}

	[Fact]
	public void GivenScaledLayoutWhenBandsMappedToDeviceThenTheyMeetWithoutGapsOrOverlaps()
	{
		// Arrange — 333 rendered rows drawn over 1000 device rows, in bands of 50
		PageLayout layout = new PageLayout(0, 10, 300, 1000, 100, 333);
		int expectedDeviceTop = 10;

		// Act and Assert — each band starts where the previous one ended, and the last ends at the bottom
		for (int top = 0; top < layout.RenderHeight; top += 50)
		{
			(int deviceTop, int deviceHeight) = layout.MapBandToDevice(top, Math.Min(50, layout.RenderHeight - top));
			Assert.Equal(expectedDeviceTop, deviceTop);
			expectedDeviceTop = deviceTop + deviceHeight;
		}

		Assert.Equal(10 + 1000, expectedDeviceTop);
	}
}
