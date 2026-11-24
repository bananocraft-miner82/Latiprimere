using System.Diagnostics.CodeAnalysis;
using System.Net;
using Latiprimere.Ipp.Connections;
using Latiprimere.Ipp.Discovery;
using Latiprimere.Ipp.Resources;
using Latiprimere.Rendering;
using Microsoft.Extensions.Logging;

namespace Latiprimere.Ipp;

/// <summary>
/// Creates the IPP network print system, for applications that don't use dependency injection.
/// </summary>
public static class IppNetworkPrintSystemFactory
{
	/// <summary>
	/// Creates a print system that prints directly to IPP printers on the network.
	/// </summary>
	/// <param name="options">The configuration, or <see langword="null"/> for discovery only.</param>
	/// <returns>
	/// The print system. It holds an HTTP connection, so dispose it, for example with <c>using</c>, when it
	/// is no longer needed.
	/// </returns>
	/// <exception cref="ArgumentException">A fingerprint in <see cref="IppNetworkOptions.TrustedCertificateFingerprints"/>
	/// or <see cref="IppNetworkOptions.PrinterCertificateFingerprints"/> isn't 32 bytes in hexadecimal; a credential
	/// has no user name, or one containing a colon; a URI in <see cref="IppNetworkOptions.PrinterCredentials"/> or
	/// <see cref="IppNetworkOptions.PrinterCertificateFingerprints"/> isn't an absolute ipp or ipps URI; or
	/// <see cref="IppNetworkOptions.RequireEncryption"/> is set and <see cref="IppNetworkOptions.PrinterUris"/> has an
	/// ipp URI.</exception>
	[SuppressMessage
	(
		"ApiDesign",
		"RS0027:API with optional parameter(s) should have the most parameters amongst its public overloads",
		Justification = "Shipped before the overload with a rasteriser and a logger factory, whose parameters are all required so that Create(null) stays unambiguous."
	)]
	public static IPrintSystem Create(IppNetworkOptions? options = null)
	{
		return IppNetworkPrintSystemFactory.Create(pdfRasterizer: null, options, loggerFactory: null);
	}

	/// <summary>
	/// Creates a print system that prints directly to IPP printers on the network, and renders PDFs as raster for
	/// printers that can't take PDF.
	/// </summary>
	/// <param name="pdfRasterizer">
	/// The rasteriser that renders PDFs for printers that don't take PDF, such as <c>PdfiumRasterizer</c> from
	/// Latiprimere.Rendering.Pdfium; or <see langword="null"/> to refuse PDFs to such printers.
	/// </param>
	/// <param name="options">The configuration, or <see langword="null"/> for discovery only.</param>
	/// <returns>
	/// The print system. It holds an HTTP connection, so dispose it, for example with <c>using</c>, when it
	/// is no longer needed.
	/// </returns>
	/// <exception cref="ArgumentException">A fingerprint in <see cref="IppNetworkOptions.TrustedCertificateFingerprints"/>
	/// or <see cref="IppNetworkOptions.PrinterCertificateFingerprints"/> isn't 32 bytes in hexadecimal; a credential
	/// has no user name, or one containing a colon; a URI in <see cref="IppNetworkOptions.PrinterCredentials"/> or
	/// <see cref="IppNetworkOptions.PrinterCertificateFingerprints"/> isn't an absolute ipp or ipps URI; or
	/// <see cref="IppNetworkOptions.RequireEncryption"/> is set and <see cref="IppNetworkOptions.PrinterUris"/> has an
	/// ipp URI.</exception>
	public static IPrintSystem Create(IPdfRasterizer? pdfRasterizer, IppNetworkOptions? options)
	{
		return IppNetworkPrintSystemFactory.Create(pdfRasterizer, options, loggerFactory: null);
	}

	/// <summary>
	/// Creates a print system that prints directly to IPP printers on the network, renders PDFs as raster for
	/// printers that can't take PDF, and logs what it carries on without.
	/// </summary>
	/// <param name="pdfRasterizer">
	/// The rasteriser that renders PDFs for printers that don't take PDF, such as <c>PdfiumRasterizer</c> from
	/// Latiprimere.Rendering.Pdfium; or <see langword="null"/> to refuse PDFs to such printers.
	/// </param>
	/// <param name="options">The configuration, or <see langword="null"/> for discovery only.</param>
	/// <param name="loggerFactory">
	/// The application's logger factory, or <see langword="null"/> to log nothing. The print system logs under the
	/// category <c>Latiprimere.Ipp</c>.
	/// </param>
	/// <returns>
	/// The print system. It holds an HTTP connection, so dispose it, for example with <c>using</c>, when it
	/// is no longer needed.
	/// </returns>
	/// <exception cref="ArgumentException">A fingerprint in <see cref="IppNetworkOptions.TrustedCertificateFingerprints"/>
	/// or <see cref="IppNetworkOptions.PrinterCertificateFingerprints"/> isn't 32 bytes in hexadecimal; a credential
	/// has no user name, or one containing a colon; a URI in <see cref="IppNetworkOptions.PrinterCredentials"/> or
	/// <see cref="IppNetworkOptions.PrinterCertificateFingerprints"/> isn't an absolute ipp or ipps URI; or
	/// <see cref="IppNetworkOptions.RequireEncryption"/> is set and <see cref="IppNetworkOptions.PrinterUris"/> has an
	/// ipp URI.</exception>
	public static IPrintSystem Create(IPdfRasterizer? pdfRasterizer, IppNetworkOptions? options, ILoggerFactory? loggerFactory)
	{
		options ??= new IppNetworkOptions();

		// Certificates: the system's trust, then pinned fingerprints, then the application's check (ADR 0037)
		IppCertificateTrust trust = IppNetworkPrintSystemFactory.CreateCertificateTrust(options);
		IppConnectionSecurity security = IppNetworkPrintSystemFactory.CreateConnectionSecurity(options, trust);

		HttpClient httpClient = IppHttpClientFactory.CreateForTcp(options.ConnectTimeout, security, options.UploadStallTimeout);

		SharpIppClientAdapter client = new SharpIppClientAdapter(httpClient)
		{
			Security         = security,
			CertificateTrust = trust
		};

		IppPrintContext context = new IppPrintContext
		{
			Client             = client,
			RequestingUserName = options.UserName ?? Environment.UserName,
			DocumentFormats    = IppDocumentFormats.Standard,
			// Network printers that take one job at a time turn others away; CUPS queues them instead
			BusyRetryDuration  = options.BusyRetryDuration,
			ValidateJobs       = options.ValidateJobs,
			PdfRasterizer      = pdfRasterizer,
			MaxRenderDpi       = options.MaxRenderDpi,
			RasterFormats      = IppNetworkPrintSystemFactory.ToRasterFormats(options.RasterFormats),
			Logger             = IppLog.CreateLogger(loggerFactory)
		};

		// Required encryption needs printers' IPPS addresses even when the system's trust is all there is (ADR 0058)
		return new IppNetworkPrintSystem(context, new MdnsIppPrinterDiscovery(), options, client)
		{
			PrefersIpps = trust.TrustsBeyondSystem || options.RequireEncryption
		};
	}

	/// <summary>
	/// Makes the checks <see cref="Create(IPdfRasterizer?, IppNetworkOptions?, ILoggerFactory?)"/> makes of the
	/// options, without creating anything; so dependency injection can report a mistake when it is registered rather
	/// than when the catalog is first resolved.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <exception cref="ArgumentException">The options' trust or credentials are invalid.</exception>
	internal static void ValidateOptions(IppNetworkOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		_ = IppNetworkPrintSystemFactory.CreateConnectionSecurity(options, IppNetworkPrintSystemFactory.CreateCertificateTrust(options));
	}

	/// <summary>
	/// Works out how requests to printers are protected: certificates checked by the trust, and credentials sent
	/// only over IPPS (ADR 0037).
	/// </summary>
	/// <param name="options">The options.</param>
	/// <param name="trust">The check of printers' certificates.</param>
	/// <returns>The connection security.</returns>
	/// <exception cref="ArgumentException">A credential has no user name, or one containing a colon; or a printer
	/// credential's URI isn't an absolute ipp or ipps URI.</exception>
	internal static IppConnectionSecurity CreateConnectionSecurity(IppNetworkOptions options, IppCertificateTrust trust)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(trust);

		if (options.Credentials is not null)
		{
			IppNetworkPrintSystemFactory.ValidateCredentials(options.Credentials);
		}

		foreach (KeyValuePair<Uri, NetworkCredential> printer in options.PrinterCredentials)
		{
			if (!IppConnectionSecurity.IsPrinterCredentialsUri(printer.Key))
			{
				throw new ArgumentException(ExceptionMessages.FormatIppPrinterCredentialsUriInvalid(printer.Key), nameof(options));
			}

			ArgumentNullException.ThrowIfNull(printer.Value, nameof(options));
			IppNetworkPrintSystemFactory.ValidateCredentials(printer.Value);
		}

		// A plain address configured alongside required encryption could only ever be refused, so say so now
		if (options.RequireEncryption && options.PrinterUris.FirstOrDefault(uri => uri is { IsAbsoluteUri: true, Scheme: "ipp" }) is Uri plain)
		{
			throw new ArgumentException(ExceptionMessages.FormatIppPlainUriConfigured(plain), nameof(options));
		}

		return IppConnectionSecurity.CreateForPrinters(options.Credentials, options.PrinterCredentials, trust) with
		{
			RequiresTls = options.RequireEncryption
		};
	}

	/// <summary>
	/// Creates the check of printers' certificates. With a shared password, a certificate the system trusts is enough
	/// only for the hosts the application named, in <see cref="IppNetworkOptions.PrinterUris"/> or
	/// <see cref="IppNetworkOptions.PrinterCredentials"/>: a printer found on the network could be announced at any
	/// host, including one with a certificate from a public authority, so its certificate must be pinned or accepted
	/// by the application's callback before the password goes to it (ADR 0037).
	/// </summary>
	/// <param name="options">The options.</param>
	/// <returns>The check.</returns>
	/// <exception cref="ArgumentException">A fingerprint isn't 32 bytes in hexadecimal.</exception>
	internal static IppCertificateTrust CreateCertificateTrust(IppNetworkOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return IppCertificateTrust.CreateForPrinters
		(
			options.TrustedCertificateFingerprints,
			options.PrinterCertificateFingerprints,
			options.ServerCertificateValidationCallback,
			options.Credentials,
			options.PrinterUris.Concat(options.PrinterCredentials.Keys)
		);
	}

	/// <summary>
	/// Turns the raster formats an application chose into the order they're tried in, without repeats; a value that
	/// isn't one of them is left out.
	/// </summary>
	/// <param name="formats">The formats, in order.</param>
	/// <returns>The order.</returns>
	internal static IReadOnlyList<Raster.RasterFormat> ToRasterFormats(IEnumerable<IppRasterFormat> formats)
	{
		ArgumentNullException.ThrowIfNull(formats);

		return formats
			.Select(format => format switch
			{
				IppRasterFormat.PwgRaster   => (Raster.RasterFormat?)Raster.RasterFormat.PwgRaster,
				IppRasterFormat.AppleRaster => Raster.RasterFormat.AppleRaster,
				IppRasterFormat.Pclm        => Raster.RasterFormat.Pclm,
				_                           => null,
			})
			.OfType<Raster.RasterFormat>()
			.Distinct()
			.ToArray();
	}

	/// <summary>
	/// Checks credentials can be sent with HTTP Basic authentication, with messages naming
	/// <see cref="IppNetworkOptions"/>.
	/// </summary>
	/// <param name="credentials">The credentials.</param>
	/// <exception cref="ArgumentException">They have no user name, or one containing a colon.</exception>
	private static void ValidateCredentials(NetworkCredential credentials)
	{
		BasicAuthentication.ValidateCredentials
		(
			credentials,
			ExceptionMessages.IppCredentialsWithoutUserName,
			ExceptionMessages.IppCredentialsUserNameHasColon
		);
	}
}
