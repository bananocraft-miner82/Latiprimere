using Latiprimere.Cups.Resources;

namespace Latiprimere.Cups;

/// <summary>
/// Where to reach the CUPS scheduler: its base IPP URI, and the Unix domain socket to connect through, if any.
/// </summary>
internal sealed record CupsEndpoint
{
	/// <summary>The environment variable libcups reads to find the server.</summary>
	internal const string ServerEnvironmentVariable = "CUPS_SERVER";

	/// <summary>The scheduler's socket on most Linux distributions.</summary>
	internal const string LinuxSocketPath = "/run/cups/cups.sock";

	/// <summary>The scheduler's socket on macOS.</summary>
	internal const string MacOSSocketPath = "/private/var/run/cupsd";

	/// <summary>The default IPP port.</summary>
	internal const int DefaultPort = 631;

	/// <summary>The host used in printer URIs when connecting through a local socket.</summary>
	internal const string LocalHost = "localhost";

	/// <summary>The suffix libcups allows after a server to choose the IPP version, as in <c>host/version=1.1</c>.</summary>
	private const string VersionSuffix = "/version=";

	/// <summary>
	/// Gets the base IPP URI of the server, such as <c>ipp://localhost:631/</c>.
	/// </summary>
	public required Uri ServerUri { get; init; }

	/// <summary>
	/// Gets the Unix domain socket to connect through, or <see langword="null"/> to connect over TCP.
	/// </summary>
	public string? SocketPath { get; init; }

	/// <summary>
	/// Gets a value indicating whether the server was configured explicitly rather than assumed to be local.
	/// </summary>
	public bool IsExplicit { get; init; }

	/// <summary>
	/// Gets the <c>CUPS_SERVER</c> value, if it was set but isn't a valid server; then every request fails, saying
	/// so, rather than the application failing to start.
	/// </summary>
	public string? InvalidEnvironmentServer { get; init; }

	/// <summary>
	/// Works out where to reach CUPS.
	/// </summary>
	/// <param name="configuredServer">The server from <see cref="CupsOptions.Server"/>, if set.</param>
	/// <param name="environmentServer">The value of <c>CUPS_SERVER</c>, if set.</param>
	/// <param name="socketExists">Checks whether a socket file exists.</param>
	/// <param name="platform">The current operating system.</param>
	/// <returns>The endpoint. An invalid <c>CUPS_SERVER</c> gives one with <see cref="InvalidEnvironmentServer"/>
	/// set.</returns>
	/// <exception cref="ArgumentException">The configured server is not valid.</exception>
	/// <remarks>
	/// A configured server is the application's own setting, so an invalid one throws. <c>CUPS_SERVER</c> comes from
	/// the user's environment, and throwing for it would stop the application starting, Windows and IPP printing
	/// included, so it is reported by the CUPS print system's requests instead.
	/// </remarks>
	internal static CupsEndpoint Resolve
	(
		string? configuredServer,
		string? environmentServer,
		Func<string, bool> socketExists,
		CupsPlatform platform
	)
	{
		// An explicit setting wins, then the environment, as with libcups
		if (!string.IsNullOrWhiteSpace(configuredServer))
		{
			return CupsEndpoint.Parse(configuredServer.Trim());
		}

		if (!string.IsNullOrWhiteSpace(environmentServer))
		{
			try
			{
				return CupsEndpoint.Parse(environmentServer.Trim());
			}
			catch (ArgumentException)
			{
				return new CupsEndpoint
				{
					ServerUri                = CupsEndpoint.CreateServerUri(CupsEndpoint.LocalHost, CupsEndpoint.DefaultPort),
					IsExplicit               = true,
					InvalidEnvironmentServer = environmentServer.Trim()
				};
			}
		}

		// Otherwise use the local scheduler's socket where the platform has one
		string? socketPath = platform switch
		{
			CupsPlatform.Linux => CupsEndpoint.LinuxSocketPath,
			CupsPlatform.MacOS => CupsEndpoint.MacOSSocketPath,
			_ => null,
		};

		if (socketPath is not null && socketExists(socketPath))
		{
			return CupsEndpoint.ForSocket(socketPath, isExplicit: false);
		}

		// Fall back to the scheduler's TCP port on this machine
		return new CupsEndpoint
		{
			ServerUri = CupsEndpoint.CreateServerUri(CupsEndpoint.LocalHost, CupsEndpoint.DefaultPort)
		};
	}

	/// <summary>
	/// Gets the IPP URI of a CUPS queue.
	/// </summary>
	/// <param name="queueName">The queue name.</param>
	/// <returns>The printer URI, such as <c>ipp://localhost:631/printers/Office</c>.</returns>
	internal Uri GetPrinterUri(string queueName)
	{
		return new Uri(this.ServerUri, "printers/" + Uri.EscapeDataString(queueName));
	}

	/// <summary>
	/// Parses a server setting in <c>CUPS_SERVER</c> form.
	/// </summary>
	/// <param name="server">A socket path or <c>host[:port]</c>, optionally followed by <c>/version=...</c>.</param>
	/// <returns>The endpoint.</returns>
	/// <exception cref="ArgumentException"><paramref name="server"/> is not valid.</exception>
	private static CupsEndpoint Parse(string server)
	{
		// Absolute paths are sockets
		if (server.StartsWith('/'))
		{
			return CupsEndpoint.ForSocket(server, isExplicit: true);
		}

		// libcups accepts host[:port]/version=1.1 for servers that need an older IPP version. Latiprimere doesn't
		// choose the IPP version per server, so the suffix is accepted and ignored.
		int versionSuffix = server.IndexOf(CupsEndpoint.VersionSuffix, StringComparison.OrdinalIgnoreCase);
		string hostAndPort = versionSuffix > 0 ? server[..versionSuffix] : server;

		// Anything else is host[:port]; parsing it as a URI handles IPv6 literals and validates the port. A user
		// name (user@host) isn't part of the CUPS_SERVER form, and would otherwise be dropped silently.
		if (!Uri.TryCreate("ipp://" + hostAndPort + "/", UriKind.Absolute, out Uri? parsedUri)
			|| parsedUri.AbsolutePath != "/"
			|| parsedUri.UserInfo.Length > 0
			|| string.IsNullOrEmpty(parsedUri.Host))
		{
			string message = ExceptionMessages.FormatInvalidCupsServer(server);

			throw new ArgumentException(message, nameof(server));
		}

		return new CupsEndpoint
		{
			ServerUri  = CupsEndpoint.CreateServerUri(parsedUri.Host, parsedUri.Port),
			IsExplicit = true
		};
	}

	/// <summary>
	/// Creates an endpoint that connects through a Unix domain socket.
	/// </summary>
	/// <param name="socketPath">The socket path.</param>
	/// <param name="isExplicit">Whether the socket was configured explicitly.</param>
	/// <returns>The endpoint.</returns>
	private static CupsEndpoint ForSocket(string socketPath, bool isExplicit)
	{
		return new CupsEndpoint
		{
			ServerUri  = CupsEndpoint.CreateServerUri(CupsEndpoint.LocalHost, CupsEndpoint.DefaultPort),
			SocketPath = socketPath,
			IsExplicit = isExplicit
		};
	}

	/// <summary>
	/// Creates a base IPP URI.
	/// </summary>
	/// <param name="host">The host name.</param>
	/// <param name="port">The port, or -1 for the default.</param>
	/// <returns>The URI, with a trailing slash so relative printer paths resolve beneath it.</returns>
	private static Uri CreateServerUri(string host, int port)
	{
		UriBuilder builder = new UriBuilder("ipp", host, port < 0 ? CupsEndpoint.DefaultPort : port, "/");

		return builder.Uri;
	}
}
