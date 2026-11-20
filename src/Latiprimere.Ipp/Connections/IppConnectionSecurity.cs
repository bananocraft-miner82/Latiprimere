using System.Net;
using System.Net.Security;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// How IPP requests are protected: whether they travel over TLS, the user name and password sent with them, and how
/// the server's certificate is checked.
/// </summary>
internal sealed record IppConnectionSecurity
{
	/// <summary>
	/// Plain connections without credentials.
	/// </summary>
	public static readonly IppConnectionSecurity None = new IppConnectionSecurity();

	/// <summary>
	/// Gets a value indicating whether requests are sent over TLS (<c>https</c>), whatever scheme their URI names.
	/// </summary>
	public bool UseTls { get; init; }

	/// <summary>
	/// Gets the user name and password sent with every request, or <see langword="null"/> to send none.
	/// </summary>
	public NetworkCredential? Credentials { get; init; }

	/// <summary>
	/// Gets the check of the server's TLS certificate, or <see langword="null"/> for the standard check against the
	/// trusted certificate authorities and the host name.
	/// </summary>
	public RemoteCertificateValidationCallback? ServerCertificateValidationCallback { get; init; }

	/// <summary>
	/// Gets credentials for particular printers, by <c>host:port</c> in lower case (see <see cref="ToHostPort"/>), used
	/// in place of <see cref="Credentials"/> for requests to them; or <see langword="null"/> for none.
	/// </summary>
	public IReadOnlyDictionary<string, NetworkCredential>? PrinterCredentials { get; init; }

	/// <summary>
	/// Gets a value indicating whether credentials are sent only over TLS, as direct IPP does (ADR 0037); CUPS sends
	/// them as <c>CupsOptions.Encryption</c> says.
	/// </summary>
	public bool SendsCredentialsOnlyOverTls { get; init; }

	/// <summary>
	/// Gets a value indicating whether any credentials are configured.
	/// </summary>
	public bool HasCredentials
	{
		get
		{
			return this.Credentials is not null || this.PrinterCredentials is { Count: > 0 };
		}
	}

	/// <summary>
	/// Creates the security direct IPP uses for printers (ADR 0037): certificates checked by the trust, credentials
	/// for all printers or for particular ones, sent only over TLS. Used for printers printed to directly, and for
	/// Windows queues sent to their IPP printers.
	/// </summary>
	/// <param name="credentials">The credentials for every printer, or <see langword="null"/>.</param>
	/// <param name="printerCredentials">Credentials for particular printers, by printer URI, already checked with
	/// <see cref="IsPrinterCredentialsUri"/> and <see cref="BasicAuthentication.ValidateCredentials"/>.</param>
	/// <param name="trust">The check of printers' certificates.</param>
	/// <returns>The security.</returns>
	public static IppConnectionSecurity CreateForPrinters
	(
		NetworkCredential?                                credentials,
		IEnumerable<KeyValuePair<Uri, NetworkCredential>> printerCredentials,
		IppCertificateTrust                               trust
	)
	{
		ArgumentNullException.ThrowIfNull(printerCredentials);
		ArgumentNullException.ThrowIfNull(trust);

		Dictionary<string, NetworkCredential> byHostPort = new Dictionary<string, NetworkCredential>(StringComparer.Ordinal);

		foreach (KeyValuePair<Uri, NetworkCredential> printer in printerCredentials)
		{
			byHostPort[IppConnectionSecurity.ToHostPort(printer.Key)] = printer.Value;
		}

		return new IppConnectionSecurity
		{
			Credentials                         = credentials,
			PrinterCredentials                  = byHostPort,
			SendsCredentialsOnlyOverTls         = true,
			ServerCertificateValidationCallback = trust.Validate
		};
	}

	/// <summary>
	/// Checks a key of a printer credentials dictionary is an absolute <c>ipp</c> or <c>ipps</c> URI.
	/// </summary>
	/// <param name="uri">The key.</param>
	/// <returns>Whether it is.</returns>
	public static bool IsPrinterCredentialsUri(Uri? uri)
	{
		return uri is { IsAbsoluteUri: true, Scheme: "ipp" or "ipps" };
	}

	/// <summary>
	/// Makes the key of <see cref="PrinterCredentials"/> for a URI: its host, without IPv6 brackets, and port, in
	/// lower case. An <c>ipp</c> or <c>ipps</c> URI without a port has IPP's 631.
	/// </summary>
	/// <param name="uri">An absolute <c>ipp</c>, <c>ipps</c>, <c>http</c> or <c>https</c> URI.</param>
	/// <returns>The key, such as <c>192.168.1.20:631</c>.</returns>
	public static string ToHostPort(Uri uri)
	{
		ArgumentNullException.ThrowIfNull(uri);

		const int ippPort = 631; // The default port of both ipp and ipps (RFC 8010, RFC 7472)
		int port = uri.Port >= 0 ? uri.Port : ippPort;

		return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{uri.IdnHost.Trim('[', ']').ToLowerInvariant()}:{port}");
	}

	/// <summary>
	/// Finds the credentials configured for a URI, whether or not they would be sent over its scheme.
	/// </summary>
	/// <param name="uri">The printer's or the request's URI.</param>
	/// <returns>Its own credentials, else <see cref="Credentials"/>, else <see langword="null"/>.</returns>
	public NetworkCredential? FindCredentials(Uri uri)
	{
		ArgumentNullException.ThrowIfNull(uri);

		return this.PrinterCredentials?.GetValueOrDefault(IppConnectionSecurity.ToHostPort(uri)) ?? this.Credentials;
	}

	/// <summary>
	/// Finds the credentials to send with a request: <see cref="FindCredentials"/>, unless they are kept to TLS and
	/// the request isn't encrypted.
	/// </summary>
	/// <param name="uri">The printer's or the request's URI.</param>
	/// <returns>The credentials, or <see langword="null"/> to send none.</returns>
	public NetworkCredential? FindCredentialsToSend(Uri uri)
	{
		ArgumentNullException.ThrowIfNull(uri);

		bool isEncrypted = this.UseTls || uri.Scheme is "https" or "ipps";

		return this.SendsCredentialsOnlyOverTls && !isEncrypted ? null : this.FindCredentials(uri);
	}
}
