using Latiprimere.Ipp;
using Latiprimere.Ipp.Discovery;
using Latiprimere.Ipp.Mapping;
using Latiprimere.Rendering;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Spooler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Latiprimere.Windows;

/// <summary>
/// Creates the Windows print system, for applications that don't use dependency injection.
/// </summary>
public static class WindowsPrintSystemFactory
{
	/// <summary>The category the Windows print system logs under.</summary>
	private const string LoggerCategory = "Latiprimere.Windows";

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
		return WindowsPrintSystemFactory.Create(pdfRasterizer, options, loggerFactory: null);
	}

	/// <summary>
	/// Creates the Windows print system, logging through a logger factory when there is one.
	/// </summary>
	/// <param name="pdfRasterizer">The PDF rasteriser, or <see langword="null"/> for raw data only.</param>
	/// <param name="options">How to print, or <see langword="null"/> for the defaults.</param>
	/// <param name="loggerFactory">The application's logger factory, or <see langword="null"/> to log nothing.</param>
	/// <returns>The print system.</returns>
	/// <exception cref="ArgumentException">A URI in <see cref="WindowsOptions.IppPrinterUris"/> isn't one IPP can
	/// use.</exception>
	internal static IPrintSystem Create(IPdfRasterizer? pdfRasterizer, WindowsOptions? options, ILoggerFactory? loggerFactory)
	{
		options ??= new WindowsOptions();
		ILogger logger = loggerFactory?.CreateLogger(WindowsPrintSystemFactory.LoggerCategory) ?? NullLogger.Instance;

		// The analyzer checks this guard: the Win32 implementations are only constructed on Windows
		if (OperatingSystem.IsWindows())
		{
			WindowsPrintContext windowsContext = new WindowsPrintContext
			{
				Spool                = new WinSpoolApi(),
				Gdi                  = new GdiRasterPrinter(options.MaxRenderDpi ?? GdiRenderSettings.UnlimitedRenderDpi),
				PdfRasterizer        = pdfRasterizer,
				AllowsAnyPrinterName = options.AllowAnyPrinterName,
				IppRoute             = options.PrintPdfOverIpp ? WindowsPrintSystemFactory.CreateIppRoute(options, pdfRasterizer, loggerFactory, logger) : null,
				Logger               = logger
			};

			return new WindowsPrintSystem(windowsContext, isSupported: true);
		}

		WindowsPrintContext unavailableContext = new WindowsPrintContext
		{
			Spool                = new UnavailableWinSpool(),
			Gdi                  = new UnavailableGdiPrinter(),
			AllowsAnyPrinterName = options.AllowAnyPrinterName,
			Logger               = logger
		};

		return new WindowsPrintSystem(unavailableContext, isSupported: false);
	}

	/// <summary>
	/// Makes the checks <see cref="Create(IPdfRasterizer?, WindowsOptions?, ILoggerFactory?)"/> makes of the options,
	/// under the same conditions, without creating anything; so dependency injection can report a mistake when it is
	/// registered rather than when the catalog is first resolved.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <exception cref="ArgumentException">A URI in <see cref="WindowsOptions.IppPrinterUris"/> isn't one IPP can
	/// use, on Windows with <see cref="WindowsOptions.PrintPdfOverIpp"/> on.</exception>
	internal static void ValidateOptions(WindowsOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (OperatingSystem.IsWindows() && options.PrintPdfOverIpp)
		{
			WindowsPrintSystemFactory.ValidateIppPrinterUris(options);
		}
	}

	/// <summary>
	/// Checks every URI in <see cref="WindowsOptions.IppPrinterUris"/> is one IPP can use.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <exception cref="ArgumentException">A URI isn't an absolute ipp, ipps, http or https URI.</exception>
	private static void ValidateIppPrinterUris(WindowsOptions options)
	{
		foreach (KeyValuePair<string, Uri> configured in options.IppPrinterUris)
		{
			_ = WindowsIppRoute.ToIppUri(configured.Value)
				?? throw new ArgumentException
				(
					Resources.ExceptionMessages.FormatIppPrinterUriInvalid(configured.Key, configured.Value),
					nameof(options)
				);
		}
	}

	/// <summary>
	/// Creates the route for PDF jobs straight to IPP printers, with the same settings as direct IPP printing.
	/// </summary>
	/// <param name="options">The options, with the configured URIs.</param>
	/// <param name="pdfRasterizer">Renders PDFs as raster for printers that can't take PDF, or <see langword="null"/>.</param>
	/// <param name="loggerFactory">The application's logger factory, or <see langword="null"/>.</param>
	/// <param name="logger">The Windows print system's logger.</param>
	/// <returns>The route, which owns its HTTP connections.</returns>
	/// <exception cref="ArgumentException">A configured URI isn't one IPP can use.</exception>
	private static WindowsIppRoute CreateIppRoute(WindowsOptions options, IPdfRasterizer? pdfRasterizer, ILoggerFactory? loggerFactory, ILogger logger)
	{
		// Checked before the connection pool exists, so a bad URI leaves nothing to dispose
		WindowsPrintSystemFactory.ValidateIppPrinterUris(options);

		IppPrintContext ippContext = new IppPrintContext
		{
			Client             = new SharpIppClientAdapter(IppHttpClientFactory.CreateForTcp(IppNetworkOptions.DefaultConnectTimeout)),
			RequestingUserName = Environment.UserName,
			DocumentFormats    = IppDocumentFormats.Standard,
			BusyRetryDuration  = IppNetworkOptions.DefaultBusyRetryDuration,
			// ipp: keys reach routed jobs, as they would reach the printer printed to directly (ADR 0027)
			VendorOptionPrefixes = IppVendorAttributeMapper.NetworkPrefixes,
			// As for a printer printed to directly: a refusal comes before the upload (ADR 0033)
			ValidateJobs         = true,
			// A PDF the printer can't take goes as raster, rendered as for the driver (ADR 0036)
			PdfRasterizer        = pdfRasterizer,
			MaxRenderDpi         = options.MaxRenderDpi,
			Logger               = IppLog.CreateLogger(loggerFactory)
		};

		// A WSD queue's printer is found by the UUID its port records, among printers announced over mDNS
		return new WindowsIppRoute(ippContext, options.IppPrinterUris, new MdnsIppPrinterDiscovery(), logger);
	}
}
