using Latiprimere.Ipp;
using Latiprimere.Ipp.Mapping;
using Latiprimere.Rendering;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows;

/// <summary>
/// Creates the Windows print system, for applications that don't use dependency injection.
/// </summary>
public static class WindowsPrintSystemFactory
{
	/// <summary>
	/// Creates the Windows print system.
	/// </summary>
	/// <param name="pdfRasterizer">
	/// The rasteriser used to print PDF documents, such as <c>PdfiumRasterizer</c> from Latiprimere.Rendering.Pdfium,
	/// or <see langword="null"/> to support raw printer data only.
	/// </param>
	/// <param name="options">How to print, or <see langword="null"/> for the defaults.</param>
	/// <returns>
	/// The print system. On platforms other than Windows it reports itself as unsupported, so it can be registered
	/// unconditionally in cross-platform applications.
	/// </returns>
	/// <exception cref="ArgumentException">A URI in <see cref="WindowsOptions.IppPrinterUris"/> isn't an absolute
	/// <c>ipp</c>, <c>ipps</c>, <c>http</c> or <c>https</c> URI.</exception>
	public static IPrintSystem Create(IPdfRasterizer? pdfRasterizer = null, WindowsOptions? options = null)
	{
		options ??= new WindowsOptions();

		// The analyzer checks this guard: the Win32 implementations are only constructed on Windows
		if (OperatingSystem.IsWindows())
		{
			WindowsPrintContext windowsContext = new WindowsPrintContext
			{
				Spool                = new WinSpoolApi(),
				Gdi                  = new GdiRasterPrinter(options.MaxRenderDpi ?? GdiRenderSettings.UnlimitedRenderDpi),
				PdfRasterizer        = pdfRasterizer,
				AllowsAnyPrinterName = options.AllowAnyPrinterName,
				IppRoute             = options.PrintPdfOverIpp ? WindowsPrintSystemFactory.CreateIppRoute(options) : null
			};

			return new WindowsPrintSystem(windowsContext, isSupported: true);
		}

		WindowsPrintContext unavailableContext = new WindowsPrintContext
		{
			Spool                = new UnavailableWinSpool(),
			Gdi                  = new UnavailableGdiPrinter(),
			AllowsAnyPrinterName = options.AllowAnyPrinterName
		};

		return new WindowsPrintSystem(unavailableContext, isSupported: false);
	}

	/// <summary>
	/// Creates the route for PDF jobs straight to IPP printers, with the same settings as direct IPP printing.
	/// </summary>
	/// <param name="options">The options, with the configured URIs.</param>
	/// <returns>The route, which owns its HTTP connections.</returns>
	/// <exception cref="ArgumentException">A configured URI isn't one IPP can use.</exception>
	private static WindowsIppRoute CreateIppRoute(WindowsOptions options)
	{
		// Checked before the connection pool exists, so a bad URI leaves nothing to dispose
		foreach (KeyValuePair<string, Uri> configured in options.IppPrinterUris)
		{
			_ = WindowsIppRoute.ToIppUri(configured.Value)
				?? throw new ArgumentException
				(
					Resources.ExceptionMessages.FormatIppPrinterUriInvalid(configured.Key, configured.Value),
					nameof(options)
				);
		}

		IppPrintContext ippContext = new IppPrintContext
		{
			Client             = new SharpIppClientAdapter(IppHttpClientFactory.CreateForTcp(IppNetworkOptions.DefaultConnectTimeout)),
			RequestingUserName = Environment.UserName,
			DocumentFormats    = IppDocumentFormats.Standard,
			BusyRetryDuration  = IppNetworkOptions.DefaultBusyRetryDuration,
			// ipp: keys reach routed jobs, as they would reach the printer printed to directly (ADR 0027)
			VendorOptionPrefixes = IppVendorAttributeMapper.NetworkPrefixes
		};

		return new WindowsIppRoute(ippContext, options.IppPrinterUris);
	}
}
