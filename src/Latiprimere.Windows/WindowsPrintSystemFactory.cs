using System.Diagnostics.CodeAnalysis;
using System.Net;
using Latiprimere.Ipp;
using Latiprimere.Ipp.Connections;
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
	/// <exception cref="ArgumentException">With <see cref="WindowsOptions.PrintPdfOverIpp"/> on: a URI in
	/// <see cref="WindowsOptions.IppPrinterUris"/> isn't an absolute <c>ipp</c>, <c>ipps</c>, <c>http</c> or <c>https</c>
	/// URI; a fingerprint in <see cref="WindowsOptions.IppTrustedCertificateFingerprints"/> or
	/// <see cref="WindowsOptions.IppPrinterCertificateFingerprints"/> isn't 32 bytes in hexadecimal; a credential has
	/// no user name, or one containing a colon; or a URI in <see cref="WindowsOptions.IppPrinterCredentials"/> or
	/// <see cref="WindowsOptions.IppPrinterCertificateFingerprints"/> isn't an absolute <c>ipp</c> or <c>ipps</c>
	/// URI.</exception>
	[SuppressMessage
	(
		"ApiDesign",
		"RS0027:API with optional parameter(s) should have the most parameters amongst its public overloads",
		Justification = "Shipped before the overload with a logger factory, whose parameters are all required so that Create() and Create(pdfRasterizer) stay unambiguous."
	)]
	public static IPrintSystem Create(IPdfRasterizer? pdfRasterizer = null, WindowsOptions? options = null)
	{
		return WindowsPrintSystemFactory.Create(pdfRasterizer, options, loggerFactory: null);
	}

	/// <summary>
	/// Creates the Windows print system, which logs what it carries on without, such as vendor options it couldn't
	/// read.
	/// </summary>
	/// <param name="pdfRasterizer">
	/// The rasteriser used to print PDF documents, such as <c>PdfiumRasterizer</c> from Latiprimere.Rendering.Pdfium,
	/// or <see langword="null"/> to support raw printer data only.
	/// </param>
	/// <param name="options">How to print, or <see langword="null"/> for the defaults.</param>
	/// <param name="loggerFactory">
	/// The application's logger factory, or <see langword="null"/> to log nothing. The print system logs under the
	/// category <c>Latiprimere.Windows</c>, and PDF jobs sent straight to IPP printers under <c>Latiprimere.Ipp</c>.
	/// </param>
	/// <returns>
	/// The print system. On platforms other than Windows it reports itself as unsupported, so it can be registered
	/// unconditionally in cross-platform applications.
	/// </returns>
	/// <exception cref="ArgumentException">With <see cref="WindowsOptions.PrintPdfOverIpp"/> on: a URI in
	/// <see cref="WindowsOptions.IppPrinterUris"/> isn't an absolute <c>ipp</c>, <c>ipps</c>, <c>http</c> or <c>https</c>
	/// URI; a fingerprint in <see cref="WindowsOptions.IppTrustedCertificateFingerprints"/> or
	/// <see cref="WindowsOptions.IppPrinterCertificateFingerprints"/> isn't 32 bytes in hexadecimal; a credential has
	/// no user name, or one containing a colon; or a URI in <see cref="WindowsOptions.IppPrinterCredentials"/> or
	/// <see cref="WindowsOptions.IppPrinterCertificateFingerprints"/> isn't an absolute <c>ipp</c> or <c>ipps</c>
	/// URI.</exception>
	public static IPrintSystem Create(IPdfRasterizer? pdfRasterizer, WindowsOptions? options, ILoggerFactory? loggerFactory)
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
	/// <exception cref="ArgumentException">On Windows with <see cref="WindowsOptions.PrintPdfOverIpp"/> on, a URI,
	/// fingerprint or credential for routed jobs is one IPP can't use.</exception>
	internal static void ValidateOptions(WindowsOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (OperatingSystem.IsWindows() && options.PrintPdfOverIpp)
		{
			WindowsPrintSystemFactory.ValidateIppPrinterUris(options);
			WindowsPrintSystemFactory.ValidateIppCredentials(options);

			// Each pinned fingerprint is checked as the trust is made
			_ = new IppCertificateTrust(options.IppTrustedCertificateFingerprints, options.IppPrinterCertificateFingerprints, callback: null, systemTrustHosts: null);
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
	/// <exception cref="ArgumentException">A configured URI, fingerprint or credential isn't one IPP can use.</exception>
	private static WindowsIppRoute CreateIppRoute(WindowsOptions options, IPdfRasterizer? pdfRasterizer, ILoggerFactory? loggerFactory, ILogger logger)
	{
		// Checked before the connection pool exists, so a bad URI leaves nothing to dispose
		WindowsPrintSystemFactory.ValidateIppPrinterUris(options);
		WindowsPrintSystemFactory.ValidateIppCredentials(options);

		// Certificates and passwords as for printers printed to directly (ADR 0037, ADR 0042)
		IppCertificateTrust trust = IppCertificateTrust.CreateForPrinters
		(
			options.IppTrustedCertificateFingerprints,
			options.IppPrinterCertificateFingerprints,
			options.IppServerCertificateValidationCallback,
			options.IppCredentials,
			options.IppPrinterUris.Values.Concat(options.IppPrinterCredentials.Keys)
		);

		IppConnectionSecurity security = IppConnectionSecurity.CreateForPrinters(options.IppCredentials, options.IppPrinterCredentials, trust);

		// A printer that stops reading partway fails the job, as for a printer printed to directly; it isn't sent
		// through the spooler as well, as some of it may already have printed
		HttpClient httpClient = IppHttpClientFactory.CreateForTcp
		(
			IppNetworkOptions.DefaultConnectTimeout,
			security,
			IppNetworkOptions.DefaultUploadStallTimeout
		);

		IppPrintContext ippContext = new IppPrintContext
		{
			Client             = new SharpIppClientAdapter(httpClient)
			{
				Security         = security,
				CertificateTrust = trust
			},
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
			RasterFormats        = IppNetworkPrintSystemFactory.ToRasterFormats(options.IppRasterFormats),
			Logger               = IppLog.CreateLogger(loggerFactory)
		};

		// A WSD queue's printer is found by the UUID its port records, among printers announced over mDNS
		return new WindowsIppRoute(ippContext, options.IppPrinterUris, new MdnsIppPrinterDiscovery(), logger)
		{
			Security    = security,
			// As for printers printed to directly: IPPS first once trust beyond the system's is configured (D4)
			PrefersIpps = trust.TrustsBeyondSystem
		};
	}

	/// <summary>
	/// Checks the credentials for routed jobs can be sent with HTTP Basic authentication, and that each printer's is
	/// keyed by an IPP URI, with messages naming <see cref="WindowsOptions"/>.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <exception cref="ArgumentException">A credential has no user name, or one containing a colon; or a key of
	/// <see cref="WindowsOptions.IppPrinterCredentials"/> isn't an absolute ipp or ipps URI.</exception>
	private static void ValidateIppCredentials(WindowsOptions options)
	{
		if (options.IppCredentials is not null)
		{
			WindowsPrintSystemFactory.ValidateCredentials(options.IppCredentials);
		}

		foreach (KeyValuePair<Uri, NetworkCredential> printer in options.IppPrinterCredentials)
		{
			if (!IppConnectionSecurity.IsPrinterCredentialsUri(printer.Key))
			{
				throw new ArgumentException(Resources.ExceptionMessages.FormatIppPrinterCredentialsUriInvalid(printer.Key), nameof(options));
			}

			ArgumentNullException.ThrowIfNull(printer.Value, nameof(options));
			WindowsPrintSystemFactory.ValidateCredentials(printer.Value);
		}
	}

	/// <summary>
	/// Checks one set of credentials can be sent with HTTP Basic authentication.
	/// </summary>
	/// <param name="credentials">The credentials.</param>
	/// <exception cref="ArgumentException">They have no user name, or one containing a colon.</exception>
	private static void ValidateCredentials(NetworkCredential credentials)
	{
		BasicAuthentication.ValidateCredentials
		(
			credentials,
			Resources.ExceptionMessages.IppCredentialsWithoutUserName,
			Resources.ExceptionMessages.IppCredentialsUserNameHasColon
		);
	}
}
