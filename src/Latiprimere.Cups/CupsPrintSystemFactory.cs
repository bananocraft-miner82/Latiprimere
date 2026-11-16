using System.Net;
using Latiprimere.Cups.Resources;
using Latiprimere.Ipp;
using Latiprimere.Ipp.Connections;
using Latiprimere.Ipp.Mapping;

namespace Latiprimere.Cups;

/// <summary>
/// Creates the CUPS print system, for applications that don't use dependency injection.
/// </summary>
public static class CupsPrintSystemFactory
{
	/// <summary>
	/// Creates a CUPS print system.
	/// </summary>
	/// <param name="options">How to connect to CUPS, or <see langword="null"/> for the defaults.</param>
	/// <returns>
	/// The print system. It holds a connection to the scheduler, so dispose it, for example with
	/// <c>using</c>, when it is no longer needed. It reports itself as unsupported on platforms without CUPS unless a server is set.
	/// </returns>
	/// <exception cref="ArgumentException">The configured server is not valid; or the credentials have no user name,
	/// or one containing a colon. An invalid <c>CUPS_SERVER</c> doesn't throw here: each request of the print system
	/// fails with a <see cref="PrintException"/> that names it.</exception>
	public static IPrintSystem Create(CupsOptions? options = null)
	{
		return CupsPrintSystemFactory.Create
		(
			options ?? new CupsOptions(),
			Environment.GetEnvironmentVariable(CupsEndpoint.ServerEnvironmentVariable)
		);
	}

	/// <summary>
	/// Creates a CUPS print system, given the value of <c>CUPS_SERVER</c>.
	/// </summary>
	/// <param name="options">How to connect to CUPS.</param>
	/// <param name="environmentServer">The value of <c>CUPS_SERVER</c>, if set.</param>
	/// <returns>The print system.</returns>
	/// <exception cref="ArgumentException">The configured server is not valid; or the credentials have no user name,
	/// or one containing a colon.</exception>
	/// <remarks>Tests pass the environment value in, as setting the variable would affect every test in the process.</remarks>
	internal static IPrintSystem Create(CupsOptions options, string? environmentServer)
	{
		// Work out where CUPS is, as libcups would
		CupsPlatform platform = CupsPrintSystemFactory.GetCurrentPlatform();
		CupsEndpoint endpoint = CupsEndpoint.Resolve(options.Server, environmentServer, File.Exists, platform);

		// An invalid CUPS_SERVER fails each request, saying why, so the catalog can still offer the other print
		// systems' printers instead of the application failing to start
		if (endpoint.InvalidEnvironmentServer is not null)
		{
			IppPrintContext failingContext = new IppPrintContext
			{
				Client             = new InvalidServerIppClient(endpoint.InvalidEnvironmentServer),
				RequestingUserName   = CupsPrintSystemFactory.GetRequestingUserName(options),
				DocumentFormats      = IppDocumentFormats.Cups,
				VendorOptionPrefixes = IppVendorAttributeMapper.CupsPrefixes
			};

			return new CupsPrintSystem(failingContext, endpoint, isSupported: true, ownedClient: null);
		}

		IppConnectionSecurity security = CupsPrintSystemFactory.CreateConnectionSecurity(options, endpoint);

		// Connect through the scheduler's socket when there is one, otherwise over TCP
		HttpClient httpClient = endpoint.SocketPath is null
			? IppHttpClientFactory.CreateForTcp(options.ConnectTimeout, security)
			: IppHttpClientFactory.CreateForUnixSocket(endpoint.SocketPath, security.Credentials);

		SharpIppClientAdapter client = new SharpIppClientAdapter(httpClient, sendsCredentials: security.Credentials is not null);

		IppPrintContext context = new IppPrintContext
		{
			Client               = client,
			RequestingUserName   = CupsPrintSystemFactory.GetRequestingUserName(options),
			DocumentFormats      = IppDocumentFormats.Cups,
			VendorOptionPrefixes = IppVendorAttributeMapper.CupsPrefixes
		};

		bool isSupported = endpoint.IsExplicit || platform != CupsPlatform.Other;

		return new CupsPrintSystem(context, endpoint, isSupported, client);
	}

	/// <summary>
	/// Works out how requests to the server are protected.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <param name="endpoint">Where the server is.</param>
	/// <returns>Whether to use TLS, the credentials to send, and how to check the server's certificate.</returns>
	/// <exception cref="ArgumentException">The credentials have no user name, or one containing a colon.</exception>
	internal static IppConnectionSecurity CreateConnectionSecurity(CupsOptions options, CupsEndpoint endpoint)
	{
		NetworkCredential? credentials = options.Credentials;

		if (credentials is not null)
		{
			CupsPrintSystemFactory.ValidateCredentials(credentials);
		}

		// The local socket is never encrypted. Over TCP, Automatic keeps the password from travelling in clear text
		bool useTls = endpoint.SocketPath is null && options.Encryption switch
		{
			CupsEncryption.Always => true,
			CupsEncryption.Never => false,
			_ => credentials is not null,
		};

		return new IppConnectionSecurity
		{
			UseTls                              = useTls,
			Credentials                         = credentials,
			ServerCertificateValidationCallback = options.ServerCertificateValidationCallback
		};
	}

	/// <summary>
	/// Works out the user name to send as <c>requesting-user-name</c>.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <returns><see cref="CupsOptions.UserName"/> if set, otherwise the credentials' user name, otherwise the
	/// operating-system user name.</returns>
	internal static string GetRequestingUserName(CupsOptions options)
	{
		// CUPS uses the authenticated user once a request is authenticated, so match it
		return options.UserName ?? options.Credentials?.UserName ?? Environment.UserName;
	}

	/// <summary>
	/// Checks the credentials can be sent with HTTP Basic authentication.
	/// </summary>
	/// <param name="credentials">The credentials.</param>
	/// <exception cref="ArgumentException">The user name is missing or contains a colon.</exception>
	private static void ValidateCredentials(NetworkCredential credentials)
	{
		if (string.IsNullOrWhiteSpace(credentials.UserName))
		{
			throw new ArgumentException(ExceptionMessages.CredentialsWithoutUserName, nameof(credentials));
		}

		if (credentials.UserName.Contains(':', StringComparison.Ordinal))
		{
			throw new ArgumentException(ExceptionMessages.CredentialsUserNameHasColon, nameof(credentials));
		}
	}

	/// <summary>
	/// Identifies the current operating system.
	/// </summary>
	/// <returns>The platform.</returns>
	private static CupsPlatform GetCurrentPlatform()
	{
		if (OperatingSystem.IsLinux())
		{
			return CupsPlatform.Linux;
		}

		return OperatingSystem.IsMacOS() ? CupsPlatform.MacOS : CupsPlatform.Other;
	}
}
