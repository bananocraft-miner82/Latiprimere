using System.Runtime.Versioning;
using Latiprimere.Rendering;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.PrintTickets;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows.Gdi;

/// <summary>
/// Prints rendered documents by drawing each page, band by band, onto a printer device context.
/// </summary>
/// <remarks>
/// For a monochrome job, each band is turned grey (<see cref="GreyscaleConverter"/>) before it is drawn, as well as
/// the DEVMODE asking the driver for monochrome: some drivers ignore that setting.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed unsafe class GdiRasterPrinter : IGdiPrinter
{
	private readonly int maxRenderDpi; // Highest resolution pages are rendered at before GDI scales them to the device

	/// <summary>
	/// Initialises a new instance of the <see cref="GdiRasterPrinter"/> class.
	/// </summary>
	/// <param name="maxRenderDpi">
	/// The highest resolution to render pages at. Lower values send fewer pixels to the driver, which scales them up.
	/// </param>
	public GdiRasterPrinter(int maxRenderDpi = GdiRenderSettings.UnlimitedRenderDpi)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(maxRenderDpi, 1);
		this.maxRenderDpi = maxRenderDpi;
	}

	/// <summary>
	/// Gets the highest resolution pages are rendered at, or <see cref="GdiRenderSettings.UnlimitedRenderDpi"/> for
	/// the device's own.
	/// </summary>
	internal int MaxRenderDpi
	{
		get
		{
			return this.maxRenderDpi;
		}
	}

	/// <inheritdoc/>
	public int PrintRasterDocument(GdiPrintRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		// Settings go in the DEVMODE before the device context exists; the driver may adjust them
		bool isFirstPageLandscape = request.Document.PageCount > 0 && request.Document.GetPageSize(0).IsLandscape;

		byte[] devModeBuffer = GdiRasterPrinter.CreateDevMode(request, isFirstPageLandscape);

		fixed (byte* devModeData = devModeBuffer)
		{
			DevMode* devMode = (DevMode*)devModeData;

			// Before the device context exists, so a driver that capped the count is set to one copy when the
			// copies are rendered here
			int manualCopies = DevModeEditor.TakeOverCopies(ref *devMode, request.Options.Copies);

			using SafeDeviceContextHandle deviceContext = GdiNative.CreateDC
			(
				GdiConstants.PrinterDriverName,
				request.PrinterName,
				IntPtr.Zero,
				devMode
			);

			SpoolerErrors.ThrowIfFailed(!deviceContext.IsInvalid, nameof(GdiNative.CreateDC), request.PrinterName);

			return this.PrintJob(deviceContext, request, manualCopies, cancellationToken);
		}
	}

	/// <summary>
	/// Runs the job on an open device context, abandoning it if anything fails.
	/// </summary>
	/// <param name="deviceContext">The printer device context.</param>
	/// <param name="request">What to print.</param>
	/// <param name="copies">How many times to render the document.</param>
	/// <param name="cancellationToken">A token to abandon spooling.</param>
	/// <returns>The spooler's job ID.</returns>
	private int PrintJob(SafeDeviceContextHandle deviceContext, GdiPrintRequest request, int copies, CancellationToken cancellationToken)
	{
		PrintableArea area = GdiRasterPrinter.ReadPrintableArea(deviceContext);
		int jobId = GdiRasterPrinter.StartDocument(deviceContext, request);

		try
		{
			// Copies outermost, so the output is collated
			BandBuffer buffer = new BandBuffer();

			for (int copy = 0; copy < copies; copy++)
			{
				for (int pageIndex = 0; pageIndex < request.Document.PageCount; pageIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					this.PrintPage(deviceContext, request, pageIndex, area, buffer);
				}
			}

			SpoolerErrors.ThrowIfFailed
			(
				GdiNative.EndDoc(deviceContext) > 0,
				nameof(GdiNative.EndDoc),
				request.PrinterName
			);
		}
		catch
		{
			// Remove the partial job from the queue, then report the original failure
			GdiNative.AbortDoc(deviceContext);

			throw;
		}

		return jobId;
	}

	/// <summary>
	/// Renders one page and draws it, band by band.
	/// </summary>
	/// <param name="deviceContext">The printer device context.</param>
	/// <param name="request">What to print.</param>
	/// <param name="pageIndex">The zero-based page index.</param>
	/// <param name="area">The printer's geometry.</param>
	/// <param name="buffer">The reusable band buffer.</param>
	private void PrintPage(SafeDeviceContextHandle deviceContext, GdiPrintRequest request, int pageIndex, PrintableArea area, BandBuffer buffer)
	{
		SpoolerErrors.ThrowIfFailed
		(
			GdiNative.StartPage(deviceContext) > 0,
			nameof(GdiNative.StartPage),
			request.PrinterName
		);

		PageLayout layout = PageLayout.Fit(request.Document.GetPageSize(pageIndex), area, this.maxRenderDpi);
		int rowsPerBand = layout.GetRowsPerBand(GdiRenderSettings.MaxBandBytes);
		byte[] pixels = buffer.Get((long)rowsPerBand * layout.RenderWidth * RasterBand.BytesPerPixel);

		int top = 0;

		while (top < layout.RenderHeight)
		{
			RasterBand band = RasterBand.Create(layout.RenderWidth, layout.RenderHeight, top, Math.Min(rowsPerBand, layout.RenderHeight - top));
			request.Document.RenderBand(pageIndex, band, pixels);

			// Grey before the driver sees it, since drivers may ignore the DEVMODE's colour setting
			if (request.Options.ColorMode == ColorMode.Monochrome)
			{
				GreyscaleConverter.ToGrey(pixels.AsSpan(0, (int)band.ByteCount));
			}

			GdiRasterPrinter.DrawBand(deviceContext, request.PrinterName, layout, band, pixels);
			top += band.Height;
		}

		SpoolerErrors.ThrowIfFailed
		(
			GdiNative.EndPage(deviceContext) > 0,
			nameof(GdiNative.EndPage),
			request.PrinterName
		);
	}

	/// <summary>
	/// Draws a rendered band at its place on the page.
	/// </summary>
	/// <param name="deviceContext">The printer device context.</param>
	/// <param name="printerName">The printer name, for error messages.</param>
	/// <param name="layout">Where the page goes.</param>
	/// <param name="band">The band that was rendered.</param>
	/// <param name="pixels">The band's pixels.</param>
	private static void DrawBand(SafeDeviceContextHandle deviceContext, string printerName, PageLayout layout, RasterBand band, byte[] pixels)
	{
		// Where this band's rows land on the device, after any scaling
		(int deviceTop, int deviceHeight) = layout.MapBandToDevice(band.Top, band.Height);

		// A negative height marks the bitmap as top-down, matching the rasteriser's row order
		BitmapInfoHeader header = new BitmapInfoHeader
		{
			Size        = (uint)sizeof(BitmapInfoHeader),
			Width       = band.PageWidth,
			Height      = -band.Height,
			Planes      = 1,
			BitCount    = GdiConstants.BitsPerPixel,
			Compression = GdiConstants.BitmapUncompressed
		};

		fixed (byte* bits = pixels)
		{
			int rowsDrawn = GdiNative.StretchDIBits
			(
				deviceContext,
				layout.X, deviceTop, layout.Width, deviceHeight,
				0, 0, band.PageWidth, band.Height,
				bits, &header, GdiConstants.DibRgbColors, GdiConstants.SourceCopy
			);

			SpoolerErrors.ThrowIfFailed(rowsDrawn > 0, nameof(GdiNative.StretchDIBits), printerName);
		}
	}

	/// <summary>
	/// Starts the GDI job.
	/// </summary>
	/// <param name="deviceContext">The printer device context.</param>
	/// <param name="request">What to print.</param>
	/// <returns>The spooler's job ID.</returns>
	private static int StartDocument(SafeDeviceContextHandle deviceContext, GdiPrintRequest request)
	{
		fixed (char* documentName = request.DocumentName)
		fixed (char* outputFile = request.OutputFile)
		{
			GdiDocInfo docInfo = new GdiDocInfo
			{
				Size         = sizeof(GdiDocInfo),
				DocumentName = (IntPtr)documentName,
				Output       = (IntPtr)outputFile
			};

			int jobId = GdiNative.StartDoc(deviceContext, &docInfo);
			SpoolerErrors.ThrowIfFailed(jobId > 0, nameof(GdiNative.StartDoc), request.PrinterName);

			return jobId;
		}
	}

	/// <summary>
	/// Reads the printer's resolution, paper size and unprintable margins.
	/// </summary>
	/// <param name="deviceContext">The printer device context.</param>
	/// <returns>The geometry.</returns>
	private static PrintableArea ReadPrintableArea(SafeDeviceContextHandle deviceContext)
	{
		return new PrintableArea
		(
			GdiNative.GetDeviceCaps(deviceContext, GdiConstants.LogicalPixelsX),
			GdiNative.GetDeviceCaps(deviceContext, GdiConstants.LogicalPixelsY),
			GdiNative.GetDeviceCaps(deviceContext, GdiConstants.PhysicalWidth),
			GdiNative.GetDeviceCaps(deviceContext, GdiConstants.PhysicalHeight),
			GdiNative.GetDeviceCaps(deviceContext, GdiConstants.PhysicalOffsetX),
			GdiNative.GetDeviceCaps(deviceContext, GdiConstants.PhysicalOffsetY)
		);
	}

	/// <summary>
	/// Starts from a saved DEVMODE or the printer's default one, merges any Print Ticket onto it, applies the
	/// options, and lets the driver validate the result.
	/// </summary>
	/// <param name="request">The printer, the print options, the paper and tray chosen, and any saved DEVMODE and
	/// Print Ticket.</param>
	/// <param name="isFirstPageLandscape">Whether the first page is landscape.</param>
	/// <returns>A buffer holding the DEVMODE and the driver's private data.</returns>
	private static byte[] CreateDevMode(GdiPrintRequest request, bool isFirstPageLandscape)
	{
		string printerName = request.PrinterName;
		using SafePrinterHandle printerHandle = PrinterHandles.Open(printerName);
		int size = WinSpoolNative.DocumentProperties(IntPtr.Zero, printerHandle, printerName, null, null, 0);
		SpoolerErrors.ThrowIfFailed(size > 0, nameof(WinSpoolNative.DocumentProperties), printerName);

		// Start from the saved DEVMODE, or the driver's defaults
		byte[] start = request.SavedDevMode ?? GdiRasterPrinter.ReadDefaultDevMode(printerHandle, printerName, size);

		// Then the Print Tickets, so the typed options still win, as they do over a saved DEVMODE (ADR 0031)
		start = PrintTicketMerger.Merge(printerName, start, request.PrintTickets);

		// A saved or merged DEVMODE may be larger than this driver's, so the buffer holds whichever is bigger
		byte[] requested = new byte[Math.Max(size, start.Length)];
		byte[] merged = new byte[size];
		start.CopyTo(requested, 0);
		int result;

		fixed (byte* requestedData = requested)
		fixed (byte* mergedData = merged)
		{
			DevMode* requestedMode = (DevMode*)requestedData;

			// Merge the changes through the driver, which drops or adjusts anything it can't do
			DevModeEditor.Apply(ref *requestedMode, request.Options, request.PaperId, request.BinId, isFirstPageLandscape);

			result = WinSpoolNative.DocumentProperties
			(
				IntPtr.Zero, printerHandle, printerName, (DevMode*)mergedData, requestedMode,
				DevModeConstants.ModeInBuffer | DevModeConstants.ModeOutBuffer
			);

			SpoolerErrors.ThrowIfFailed(result > 0, nameof(WinSpoolNative.DocumentProperties), printerName);
		}

		return merged;
	}

	/// <summary>
	/// Reads the driver's default DEVMODE.
	/// </summary>
	/// <param name="printerHandle">The open printer.</param>
	/// <param name="printerName">The printer name.</param>
	/// <param name="size">The driver's DEVMODE size, with its private data.</param>
	/// <returns>The DEVMODE.</returns>
	private static byte[] ReadDefaultDevMode(SafePrinterHandle printerHandle, string printerName, int size)
	{
		byte[] defaults = new byte[size];

		fixed (byte* data = defaults)
		{
			int result = WinSpoolNative.DocumentProperties
			(
				IntPtr.Zero, printerHandle, printerName, (DevMode*)data, null, DevModeConstants.ModeOutBuffer
			);

			SpoolerErrors.ThrowIfFailed(result > 0, nameof(WinSpoolNative.DocumentProperties), printerName);
		}

		return defaults;
	}
}
