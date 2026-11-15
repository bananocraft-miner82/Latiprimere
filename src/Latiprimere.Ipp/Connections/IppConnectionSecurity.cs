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
}
