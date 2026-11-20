using System.Net;
using System.Net.Security;
using Latiprimere.Ipp;

namespace Latiprimere.Cups;

/// <summary>
/// Configures how Latiprimere connects to CUPS.
/// </summary>
public sealed class CupsOptions
{
	/// <summary>
	/// The default longest wait for a TCP connection to the CUPS server to open.
	/// </summary>
	public static readonly TimeSpan DefaultConnectTimeout = IppHttpClientFactory.DefaultConnectTimeout;

	private TimeSpan connectTimeout = CupsOptions.DefaultConnectTimeout; // Backs ConnectTimeout
	private CupsEncryption encryption = CupsEncryption.Automatic;        // Backs Encryption

	/// <summary>
	/// Gets or sets the CUPS server to use, in the same form as the <c>CUPS_SERVER</c> environment variable: either
	/// the path of the scheduler's Unix domain socket (for example <c>/run/cups/cups.sock</c>), or a host name with
	/// an optional port (for example <c>printserver:631</c>).
	/// </summary>
	/// <remarks>
	/// When <see langword="null"/>, Latiprimere uses <c>CUPS_SERVER</c> if set, then the local scheduler's socket,
	/// then <c>localhost:631</c>. Setting a server explicitly also allows CUPS to be used from Windows.
	/// </remarks>
	public string? Server { get; set; }

	/// <summary>
	/// Gets or sets the user name sent with requests as <c>requesting-user-name</c>, which CUPS uses to decide who
	/// owns a job. When <see langword="null"/>, the user name of <see cref="Credentials"/> is used if set, otherwise
	/// the current operating-system user name.
	/// </summary>
	/// <remarks>
	/// It isn't a login: CUPS trusts this name without checking it. For a server that asks for a password, set
	/// <see cref="Credentials"/>; once a request is authenticated, CUPS uses the authenticated user instead.
	/// </remarks>
	public string? UserName { get; set; }

	/// <summary>
	/// Gets or sets the user name and password to authenticate with, or <see langword="null"/>, the default, to send
	/// none.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A standard CUPS installation needs no password to print, or to check or cancel your own jobs. Servers set up to
	/// ask for one answer with a <see cref="PrintAuthenticationException"/> until it is set; catch that to ask the
	/// user, then create the print system again.
	/// </para>
	/// <para>
	/// They are sent with every request, as HTTP Basic authentication, and over TCP they are encrypted by default:
	/// see <see cref="Encryption"/>. The <see cref="NetworkCredential.Domain"/> is not used.
	/// </para>
	/// </remarks>
	public NetworkCredential? Credentials { get; set; }

	/// <summary>
	/// Gets or sets when TCP connections to the server are encrypted. Defaults to
	/// <see cref="CupsEncryption.Automatic"/>: encrypted when <see cref="Credentials"/> are set.
	/// </summary>
	/// <remarks>
	/// CUPS creates a self-signed certificate for itself unless given another, and .NET doesn't trust one of those.
	/// To connect to such a server, check its certificate in <see cref="ServerCertificateValidationCallback"/>.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is not one of the enumeration's values.</exception>
	public CupsEncryption Encryption
	{
		get
		{
			return this.encryption;
		}

		set
		{
			if (!Enum.IsDefined(value))
			{
				throw new ArgumentOutOfRangeException(nameof(value), value, null);
			}

			this.encryption = value;
		}
	}

	/// <summary>
	/// Gets or sets the check of the server's certificate on encrypted connections, or <see langword="null"/>, the
	/// default, for the standard check against the trusted certificate authorities and the host name.
	/// </summary>
	/// <remarks>
	/// Use it to trust a server's self-signed certificate, by comparing the certificate with one you know is the
	/// server's, for example by its thumbprint. Accepting every certificate gives up the protection encryption is
	/// for: anyone able to intercept the connection could read the password.
	/// </remarks>
	public RemoteCertificateValidationCallback? ServerCertificateValidationCallback { get; set; }

	/// <summary>
	/// Gets or sets the longest wait for a TCP connection to the CUPS server to open. Defaults to
	/// <see cref="DefaultConnectTimeout"/>, 10 seconds; without a limit, a server address with nothing behind it can
	/// take about 21 seconds to fail on Windows. Connections through the scheduler's Unix domain socket open at once
	/// or fail at once, so this doesn't apply to them.
	/// </summary>
	/// <remarks>
	/// It covers opening the connection only, not waiting for an answer. When it runs out, the request fails with a
	/// <see cref="PrintException"/>.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">The value is not positive, or is longer than about 24 days.</exception>
	public TimeSpan ConnectTimeout
	{
		get
		{
			return this.connectTimeout;
		}

		set
		{
			IppHttpClientFactory.ValidateConnectTimeout(value);
			this.connectTimeout = value;
		}
	}
}
