using System.Diagnostics.CodeAnalysis;
using System.Net;
using Latiprimere.Cups.Resources;
using Latiprimere.Ipp;
using Latiprimere.Ipp.Connections;
using Latiprimere.Ipp.Mapping;
using Microsoft.Extensions.Logging;

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
	[SuppressMessage
	(
		"ApiDesign",
		"RS0027:API with optional parameter(s) should have the most parameters amongst its public overloads",
		Justification = "Shipped before the overload with a logger factory, whose parameters are both required so that Create(null) stays unambiguous."
	)]
	public static IPrintSystem Create(CupsOptions? options = null)
	{
		return CupsPrintSystemFactory.Create(options, loggerFactory: null);
	}

	/// <summary>
	/// Creates a CUPS print system that logs what it carries on without, such as vendor options it couldn't read.
	/// </summary>
	/// <param name="options">How to connect to CUPS, or <see langword="null"/> for the defaults.</param>
	/// <param name="loggerFactory">
	/// The application's logger factory, or <see langword="null"/> to log nothing. The print system logs under the
	/// category <c>Latiprimere.Ipp</c>.
	/// </param>
	/// <returns>
	/// The print system. It holds a connection to the scheduler, so dispose it, for example with
	/// <c>using</c>, when it is no longer needed. It reports itself as unsupported on platforms without CUPS unless a server is set.
	/// </returns>
	/// <exception cref="ArgumentException">The configured server is not valid; or the credentials have no user name,
	/// or one containing a colon. An invalid <c>CUPS_SERVER</c> doesn't throw here: each request of the print system
	/// fails with a <see cref="PrintException"/> that names it.</exception>
	public static IPrintSystem Create(CupsOptions? options, ILoggerFactory? loggerFactory)
	{
		return CupsPrintSystemFactory.CreateForEnvironment
		(
			options ?? new CupsOptions(),
			Environment.GetEnvironmentVariable(CupsEndpoint.ServerEnvironmentVariable),
			loggerFactory
		);
	}

	/// <summary>
	/// Creates a CUPS print system, given the value of <c>CUPS_SERVER</c>.
	/// </summary>
	/// <param name="options">How to connect to CUPS.</param>
	/// <param name="environmentServer">The value of <c>CUPS_SERVER</c>, if set.</param>
	/// <param name="loggerFactory">The application's logger factory, or <see langword="null"/> to log nothing.</param>
	/// <returns>The print system.</returns>
	/// <exception cref="ArgumentException">The configured server is not valid; or the credentials have no user name,
	/// or one containing a colon.</exception>
	/// <remarks>Tests pass the environment value in, as setting the variable would affect every test in the process.</remarks>
	internal static IPrintSystem CreateForEnvironment(CupsOptions options, string? environmentServer, ILoggerFactory? loggerFactory = null)
	{
		ILogger logger = IppLog.CreateLogger(loggerFactory);

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
				VendorOptionPrefixes = IppVendorAttributeMapper.CupsPrefixes,
				Logger               = logger
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
			VendorOptionPrefixes = IppVendorAttributeMapper.CupsPrefixes,
			Logger               = logger
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
	/// Checks the options as creating the print system does, without creating anything or looking for the scheduler;
	/// so dependency injection can report a mistake when it is registered rather than when the catalog is first
	/// resolved.
	/// </summary>
	/// <param name="options">The options.</param>
	/// <exception cref="ArgumentException"><see cref="CupsOptions.Server"/> isn't a socket path or host[:port], or
	/// the credentials have no user name, or one containing a colon.</exception>
	internal static void ValidateOptions(CupsOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Only the configured server is parsed: the environment's is reported per request, and no socket is looked for
		if (!string.IsNullOrWhiteSpace(options.Server))
		{
			_ = CupsEndpoint.Resolve(options.Server, environmentServer: null, socketExists: _ => false, CupsPlatform.Other);
		}

		if (options.Credentials is not null)
		{
			CupsPrintSystemFactory.ValidateCredentials(options.Credentials);
		}
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
	/// Checks the credentials can be sent with HTTP Basic authentication, with messages naming
	/// <see cref="CupsOptions"/>.
	/// </summary>
	/// <param name="credentials">The credentials.</param>
	/// <exception cref="ArgumentException">The user name is missing or contains a colon.</exception>
	private static void ValidateCredentials(NetworkCredential credentials)
	{
		BasicAuthentication.ValidateCredentials
		(
			credentials,
			ExceptionMessages.CredentialsWithoutUserName,
			ExceptionMessages.CredentialsUserNameHasColon
		);
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
