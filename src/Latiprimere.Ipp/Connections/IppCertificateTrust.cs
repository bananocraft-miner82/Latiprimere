using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Decides which printers' TLS certificates are trusted: those the system trusts (when a shared password is
/// configured, only for hosts the application named), those pinned by SHA-256 fingerprint, and those the application's
/// callback accepts (ADR 0037). Remembers each certificate it refuses, so the error can say what to pin.
/// </summary>
internal sealed class IppCertificateTrust
{
	/// <summary>The length of a SHA-256 fingerprint in hexadecimal digits.</summary>
	private const int FingerprintDigits = 64;

	private readonly HashSet<string> pinned;                              // Normalised fingerprints trusted
	private readonly RemoteCertificateValidationCallback? callback;        // The application's own check, if any
	private readonly HashSet<string>? systemTrustHosts;                    // Hosts the system's trust is enough for; null for all
	private readonly ConcurrentDictionary<string, RefusedCertificate> refusedByHost =
		new ConcurrentDictionary<string, RefusedCertificate>(StringComparer.OrdinalIgnoreCase); // Last refused, by host

	/// <summary>
	/// Initialises a new instance of the <see cref="IppCertificateTrust"/> class that accepts the system's trust for
	/// every host.
	/// </summary>
	/// <param name="pinnedFingerprints">SHA-256 fingerprints of certificates to trust, in hexadecimal, with or without
	/// colons or spaces between the bytes.</param>
	/// <param name="callback">The application's check for certificates neither the system trusts nor pinned, or
	/// <see langword="null"/>.</param>
	/// <exception cref="ArgumentException">A fingerprint isn't 32 bytes in hexadecimal.</exception>
	public IppCertificateTrust(IEnumerable<string> pinnedFingerprints, RemoteCertificateValidationCallback? callback)
		: this(pinnedFingerprints, callback, systemTrustHosts: null)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="IppCertificateTrust"/> class that accepts the system's trust only
	/// for some hosts, as when a shared password is configured (ADR 0037).
	/// </summary>
	/// <param name="pinnedFingerprints">SHA-256 fingerprints of certificates to trust, in hexadecimal, with or without
	/// colons or spaces between the bytes.</param>
	/// <param name="callback">The application's check for certificates the system's trust and the pins don't accept,
	/// or <see langword="null"/>.</param>
	/// <param name="systemTrustHosts">
	/// The hosts for which a certificate the system trusts is enough, such as those the application configured. Any
	/// other host's certificate must be pinned or accepted by <paramref name="callback"/>. <see langword="null"/>
	/// accepts the system's trust for every host.
	/// </param>
	/// <exception cref="ArgumentException">A fingerprint isn't 32 bytes in hexadecimal.</exception>
	public IppCertificateTrust
	(
		IEnumerable<string>                  pinnedFingerprints,
		RemoteCertificateValidationCallback? callback,
		IEnumerable<string>?                 systemTrustHosts
	)
	{
		ArgumentNullException.ThrowIfNull(pinnedFingerprints);

		this.pinned = new HashSet<string>(pinnedFingerprints.Select(IppCertificateTrust.NormalizeFingerprint), StringComparer.Ordinal);
		this.callback = callback;
		this.systemTrustHosts = systemTrustHosts is null
			? null
			: new HashSet<string>(systemTrustHosts.Select(IppCertificateTrust.NormalizeHost), StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Gets a value indicating whether anything beyond the system's trust is configured: pinned certificates or a
	/// callback. Only then are printers' IPPS addresses preferred (decision D4).
	/// </summary>
	public bool TrustsBeyondSystem
	{
		get
		{
			return this.pinned.Count > 0 || this.callback is not null;
		}
	}

	/// <summary>
	/// Puts a fingerprint into one form for comparison: 64 upper-case hexadecimal digits.
	/// </summary>
	/// <param name="fingerprint">The fingerprint, with or without colons, spaces or dashes between the bytes.</param>
	/// <returns>The fingerprint.</returns>
	/// <exception cref="ArgumentException">It isn't 32 bytes in hexadecimal.</exception>
	internal static string NormalizeFingerprint(string fingerprint)
	{
		ArgumentNullException.ThrowIfNull(fingerprint);

		string digits = string.Concat(fingerprint.Where(character => character is not (':' or ' ' or '-'))).ToUpperInvariant();

		if (digits.Length != IppCertificateTrust.FingerprintDigits || !digits.All(char.IsAsciiHexDigit))
		{
			throw new ArgumentException(ExceptionMessages.FormatIppCertificateFingerprintInvalid(fingerprint), nameof(fingerprint));
		}

		return digits;
	}

	/// <summary>
	/// Writes a certificate's SHA-256 fingerprint as pairs of upper-case hexadecimal digits separated by colons, as
	/// browsers and <c>openssl</c> show it.
	/// </summary>
	/// <param name="certificate">The certificate.</param>
	/// <returns>The fingerprint, such as <c>3A:7F:…</c>.</returns>
	internal static string GetFingerprint(X509Certificate certificate)
	{
		ArgumentNullException.ThrowIfNull(certificate);

		byte[] hash = certificate.GetCertHash(HashAlgorithmName.SHA256);

		return string.Join(':', hash.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
	}

	/// <summary>
	/// Finds the certificate last refused for a host.
	/// </summary>
	/// <param name="host">The host, as in the printer's URI.</param>
	/// <returns>The refused certificate, or <see langword="null"/> if none was refused.</returns>
	public RefusedCertificate? FindRefusal(string host)
	{
		ArgumentNullException.ThrowIfNull(host);

		return this.refusedByHost.GetValueOrDefault(IppCertificateTrust.NormalizeHost(host));
	}

	/// <summary>
	/// Checks a printer's certificate during the TLS handshake.
	/// </summary>
	/// <param name="sender">The TLS stream, which names the host.</param>
	/// <param name="certificate">The printer's certificate.</param>
	/// <param name="chain">Its chain.</param>
	/// <param name="errors">What the system's check found.</param>
	/// <returns>Whether to trust it.</returns>
	public bool Validate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
	{
		string? host = sender is SslStream stream && !string.IsNullOrEmpty(stream.TargetHostName) ? stream.TargetHostName : null;

		return this.Validate(sender, host, certificate, chain, errors);
	}

	/// <summary>
	/// Checks a printer's certificate for a host: the system's trust where it is enough, then the pins, then the
	/// application's check.
	/// </summary>
	/// <param name="sender">The TLS stream, passed on to the application's check.</param>
	/// <param name="host">The host connected to, or <see langword="null"/> if not known.</param>
	/// <param name="certificate">The printer's certificate.</param>
	/// <param name="chain">Its chain.</param>
	/// <param name="errors">What the system's check found.</param>
	/// <returns>Whether to trust it.</returns>
	internal bool Validate(object sender, string? host, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
	{
		// The system's trust proves the certificate names the host, but anyone on the network can announce a printer
		// at a host of their choosing; so with a shared password, it is enough only for hosts the application named
		bool isTrustedBySystem = errors == SslPolicyErrors.None;

		if (isTrustedBySystem && this.IsSystemTrustEnough(host))
		{
			return true;
		}

		if (certificate is null)
		{
			return false;
		}

		string fingerprint = IppCertificateTrust.GetFingerprint(certificate);

		// Pinned: trusted whatever name it carries, as printers' certificates rarely name the address used
		if (this.pinned.Contains(fingerprint.Replace(":", string.Empty, StringComparison.Ordinal)))
		{
			return true;
		}

		if (this.callback?.Invoke(sender, certificate, chain, errors) == true)
		{
			return true;
		}

		if (host is not null)
		{
			this.refusedByHost[IppCertificateTrust.NormalizeHost(host)] = new RefusedCertificate
			{
				Fingerprint       = fingerprint,
				IsTrustedBySystem = isTrustedBySystem
			};
		}

		return false;
	}

	/// <summary>
	/// Puts a host into one form: without the brackets of an IPv6 address.
	/// </summary>
	/// <param name="host">The host.</param>
	/// <returns>The host.</returns>
	private static string NormalizeHost(string host)
	{
		return host.Trim('[', ']');
	}

	/// <summary>
	/// Checks whether a certificate the system trusts is enough for a host.
	/// </summary>
	/// <param name="host">The host, or <see langword="null"/> if not known.</param>
	/// <returns>Whether it is.</returns>
	private bool IsSystemTrustEnough(string? host)
	{
		return this.systemTrustHosts is null
			|| (host is not null && this.systemTrustHosts.Contains(IppCertificateTrust.NormalizeHost(host)));
	}
}
