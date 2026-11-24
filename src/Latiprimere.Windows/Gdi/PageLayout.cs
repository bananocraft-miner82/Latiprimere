using Latiprimere.Rendering;

namespace Latiprimere.Windows.Gdi;

/// <summary>
/// Where and how large a document page is drawn on the paper, and the pixel size it is rendered at.
/// </summary>
/// <param name="X">The left edge in device coordinates (negative when it starts in the unprintable margin).</param>
/// <param name="Y">The top edge in device coordinates.</param>
/// <param name="Width">The drawn width in device pixels.</param>
/// <param name="Height">The drawn height in device pixels.</param>
/// <param name="RenderWidth">The width the page is rendered at; less than <paramref name="Width"/> when rendering below
/// device resolution.</param>
/// <param name="RenderHeight">The height the page is rendered at.</param>
internal readonly record struct PageLayout(int X, int Y, int Width, int Height, int RenderWidth, int RenderHeight)
{
	/// <summary>
	/// Places a page on the paper: actual size if it fits, otherwise scaled down to fit, and centred. A page the same
	/// size as the paper lines up with the paper's edges, so content in the unprintable margin is clipped, as with
	/// any printing application.
	/// </summary>
	/// <param name="page">The page size in points.</param>
	/// <param name="area">The printer's geometry.</param>
	/// <param name="maxRenderDpi">The highest resolution to render at; GDI scales the result up to the
	/// device's.</param>
	/// <returns>The layout.</returns>
	internal static PageLayout Fit(PageSize page, PrintableArea area, int maxRenderDpi)
	{
		// The page's size at the printer's resolution
		double pageWidth = page.WidthPoints / PageSize.PointsPerInch * area.DpiX;
		double pageHeight = page.HeightPoints / PageSize.PointsPerInch * area.DpiY;

		// Shrink to fit the paper, never enlarge
		double scale = Math.Min(1.0, Math.Min(area.PaperWidth / pageWidth, area.PaperHeight / pageHeight));
		int width = Math.Max(1, (int)Math.Round(pageWidth * scale));
		int height = Math.Max(1, (int)Math.Round(pageHeight * scale));

		// Centre on the paper, then convert from paper to device coordinates
		int x = ((area.PaperWidth - width) / 2) - area.OffsetX;
		int y = ((area.PaperHeight - height) / 2) - area.OffsetY;

		// Render at the device's resolution, or the cap if lower
		int renderWidth = PageLayout.ScaleToResolution(width, area.DpiX, maxRenderDpi);
		int renderHeight = PageLayout.ScaleToResolution(height, area.DpiY, maxRenderDpi);

		return new PageLayout(x, y, width, height, renderWidth, renderHeight);
	}

	/// <summary>
	/// Works out how many rendered rows to produce at a time so each band stays within a memory budget.
	/// </summary>
	/// <param name="maxBandBytes">The largest band, in bytes.</param>
	/// <param name="pixelFormat">The format bands are rendered in.</param>
	/// <returns>The number of rows per band: at least one, at most the rendered height.</returns>
	internal int GetRowsPerBand(long maxBandBytes, RasterPixelFormat pixelFormat = RasterPixelFormat.Bgra32)
	{
		long rows = maxBandBytes / this.GetStride(pixelFormat);

		return (int)Math.Clamp(rows, 1, this.RenderHeight);
	}

	/// <summary>
	/// Gets the bytes from one rendered row to the next, as a device-independent bitmap needs them: a whole number of
	/// 4-byte words, which 32-bit pixels always are and 8-bit grey rows are padded to.
	/// </summary>
	/// <param name="pixelFormat">The format bands are rendered in.</param>
	/// <returns>The stride, in bytes.</returns>
	internal int GetStride(RasterPixelFormat pixelFormat)
	{
		int bytes = this.RenderWidth * RasterBand.GetBytesPerPixel(pixelFormat);

		return (bytes + 3) & ~3;
	}

	/// <summary>
	/// Maps a band of rendered rows to device rows, so consecutive bands meet without gaps or overlaps when scaled.
	/// </summary>
	/// <param name="top">The band's first rendered row.</param>
	/// <param name="height">The band's number of rendered rows.</param>
	/// <returns>The band's top edge and height on the device.</returns>
	internal (int DeviceTop, int DeviceHeight) MapBandToDevice(int top, int height)
	{
		int deviceTop = (int)((long)top * this.Height / this.RenderHeight);
		int deviceBottom = (int)((long)(top + height) * this.Height / this.RenderHeight);

		return (this.Y + deviceTop, deviceBottom - deviceTop);
	}

	/// <summary>
	/// Scales a device-pixel length down to a capped resolution.
	/// </summary>
	/// <param name="devicePixels">The length in device pixels.</param>
	/// <param name="deviceDpi">The device's resolution.</param>
	/// <param name="maxDpi">The highest resolution to render at.</param>
	/// <returns>The length to render at; at least one pixel.</returns>
	private static int ScaleToResolution(int devicePixels, int deviceDpi, int maxDpi)
	{
		if (deviceDpi <= maxDpi)
		{
			return devicePixels;
		}

		return Math.Max(1, (int)Math.Round((double)devicePixels * maxDpi / deviceDpi));
	}
}
