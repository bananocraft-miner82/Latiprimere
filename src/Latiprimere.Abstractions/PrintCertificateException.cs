using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// The exception thrown when a printer's certificate isn't trusted, so no encrypted connection to it was made.
/// </summary>
/// <remarks>
/// A printer reached over IPPS usually has a certificate it made itself, which no certificate authority vouches for.
/// To trust it, compare <see cref="CertificateFingerprint"/> with the fingerprint the printer's own web page or
/// control panel shows, and if they match, add it to <c>IppNetworkOptions.TrustedCertificateFingerprints</c>. Because
/// it derives from <see cref="PrintException"/>, a <c>catch</c> for <see cref="PrintException"/> catches it too.
/// </remarks>
public sealed class PrintCertificateException : PrintException
{
	/// <summary>
	/// Initialises a new instance of the <see cref="PrintCertificateException"/> class with a default message.
	/// </summary>
	public PrintCertificateException()
		: base(ExceptionMessages.CertificateNotTrusted)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintCertificateException"/> class with the given message.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	public PrintCertificateException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintCertificateException"/> class with the given message and
	/// cause.
	/// </summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that caused this exception.</param>
	public PrintCertificateException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the SHA-256 fingerprint of the certificate that was refused, as colon-separated pairs of hexadecimal
	/// digits such as <c>3A:7F:...</c>, ready to add to <c>IppNetworkOptions.TrustedCertificateFingerprints</c>; or
	/// <see langword="null"/> if it isn't known.
	/// </summary>
	public string? CertificateFingerprint { get; init; }

	/// <summary>
	/// Gets the address of the printer whose certificate was refused, or <see langword="null"/> if it isn't known.
	/// </summary>
	public Uri? PrinterUri { get; init; }
}
