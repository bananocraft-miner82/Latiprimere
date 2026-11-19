using System.Net;
using System.Net.Sockets;
using Latiprimere.Ipp.Connections;

namespace Latiprimere.Ipp;

/// <summary>
/// Creates the HTTP clients that carry IPP requests, over TCP or a Unix domain socket.
/// </summary>
internal static class IppHttpClientFactory
{
	/// <summary>
	/// The default longest wait for a TCP connection. Without one, .NET waits for the operating system, which on
	/// Windows gives up on an address with nothing behind it after about 21 seconds.
	/// </summary>
	internal static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(10);

	/// <summary>
	/// The longest connect timeout <see cref="SocketsHttpHandler"/> accepts.
	/// </summary>
	private static readonly TimeSpan MaxConnectTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

	/// <summary>
	/// Creates an HTTP client that connects over TCP to the host named in each request URI, trying its IPv6 and IPv4
	/// addresses together (see <see cref="HappyEyeballsConnector"/>).
	/// </summary>
	/// <param name="connectTimeout">The longest wait for each connection to open, across all of the host's
	/// addresses.</param>
	/// <returns>The HTTP client. The caller owns it.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="connectTimeout"/> is not positive, or is longer
	/// than about 24 days.</exception>
	internal static HttpClient CreateForTcp(TimeSpan connectTimeout)
	{
		return IppHttpClientFactory.CreateForTcp(connectTimeout, IppConnectionSecurity.None);
	}

	/// <summary>
	/// Creates an HTTP client that connects over TCP to the host named in each request URI, trying its IPv6 and IPv4
	/// addresses together (see <see cref="HappyEyeballsConnector"/>), and protects requests as asked.
	/// </summary>
	/// <param name="connectTimeout">The longest wait for each connection to open, across all of the host's
	/// addresses.</param>
	/// <param name="security">Whether to use TLS, the credentials to send, and how to check the server's
	/// certificate.</param>
	/// <returns>The HTTP client. The caller owns it.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="connectTimeout"/> is not positive, or is longer
	/// than about 24 days.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="security"/> is <see langword="null"/>.</exception>
	internal static HttpClient CreateForTcp(TimeSpan connectTimeout, IppConnectionSecurity security)
	{
		IppHttpClientFactory.ValidateConnectTimeout(connectTimeout);
		ArgumentNullException.ThrowIfNull(security);

		// The handler cancels the callback's token when the connect timeout runs out
		HappyEyeballsConnector connector = HappyEyeballsConnector.CreateDefault();

		SocketsHttpHandler handler = new SocketsHttpHandler
		{
			ConnectTimeout  = connectTimeout,
			ConnectCallback = (context, cancellationToken) =>
			{
				return connector.ConnectAsync(context.DnsEndPoint, cancellationToken);
			}
		};

		// TLS runs over the stream the connect callback returns
		handler.SslOptions.RemoteCertificateValidationCallback = security.ServerCertificateValidationCallback;

		return IppHttpClientFactory.CreateClient(handler, security);
	}

	/// <summary>
	/// Checks a connect timeout is one the HTTP handler accepts.
	/// </summary>
	/// <param name="connectTimeout">The timeout to check.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="connectTimeout"/> is not positive, or is longer
	/// than about 24 days.</exception>
	internal static void ValidateConnectTimeout(TimeSpan connectTimeout)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(connectTimeout, TimeSpan.Zero);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(connectTimeout, IppHttpClientFactory.MaxConnectTimeout);
	}

	/// <summary>
	/// Creates an HTTP client that sends every request over the given Unix domain socket, whatever host the request
	/// URI names. Used to reach the local CUPS scheduler.
	/// </summary>
	/// <param name="socketPath">The path of the Unix domain socket.</param>
	/// <returns>The HTTP client. The caller owns it.</returns>
	/// <exception cref="ArgumentException"><paramref name="socketPath"/> is <see langword="null"/>, empty or
	/// whitespace.</exception>
	internal static HttpClient CreateForUnixSocket(string socketPath)
	{
		return IppHttpClientFactory.CreateForUnixSocket(socketPath, credentials: null);
	}

	/// <summary>
	/// Creates an HTTP client that sends every request over the given Unix domain socket, whatever host the request
	/// URI names, with the given credentials. Used to reach the local CUPS scheduler, which never asks for TLS on its
	/// socket.
	/// </summary>
	/// <param name="socketPath">The path of the Unix domain socket.</param>
	/// <param name="credentials">The user name and password to send with every request, or <see langword="null"/>
	/// for none.</param>
	/// <returns>The HTTP client. The caller owns it.</returns>
	/// <exception cref="ArgumentException"><paramref name="socketPath"/> is <see langword="null"/>, empty or
	/// whitespace.</exception>
	internal static HttpClient CreateForUnixSocket(string socketPath, NetworkCredential? credentials)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);

		// Replace the TCP connection with one to the socket; HTTP runs over it unchanged
		SocketsHttpHandler handler = new SocketsHttpHandler
		{
			ConnectCallback = (context, cancellationToken) =>
			{
				return IppHttpClientFactory.ConnectToUnixSocketAsync(socketPath, cancellationToken);
			}
		};

		return IppHttpClientFactory.CreateClient(handler, new IppConnectionSecurity { Credentials = credentials });
	}

	/// <summary>
	/// Creates the HTTP client over a handler, adding <see cref="IppConnectionHandler"/> when requests need TLS or
	/// credentials.
	/// </summary>
	/// <param name="handler">The handler that connects.</param>
	/// <param name="security">How to protect requests.</param>
	/// <returns>The HTTP client, which owns the handlers.</returns>
	/// <remarks>
	/// The client has no overall timeout: HttpClient's default of 100 s would also limit uploading a document, and
	/// a large job to a printer that reads its input only as fast as it prints takes longer. Connecting is limited
	/// by the handler's connect timeout, and every request other than Print-Job by
	/// <see cref="SharpIppClientAdapter.DefaultResponseTimeout"/>.
	/// </remarks>
	private static HttpClient CreateClient(SocketsHttpHandler handler, IppConnectionSecurity security)
	{
		HttpMessageHandler outerHandler = !security.UseTls && !security.HasCredentials
			? handler
			: new IppConnectionHandler(security, handler);

		return new HttpClient(outerHandler, disposeHandler: true)
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
	}

	/// <summary>
	/// Opens a stream to a Unix domain socket.
	/// </summary>
	/// <param name="socketPath">The path of the socket.</param>
	/// <param name="cancellationToken">A token to cancel the connection attempt.</param>
	/// <returns>A stream that owns the connected socket.</returns>
	private static async ValueTask<Stream> ConnectToUnixSocketAsync(string socketPath, CancellationToken cancellationToken)
	{
		Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

		try
		{
			await socket
				.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cancellationToken)
				.ConfigureAwait(false);

			return new NetworkStream(socket, ownsSocket: true);
		}
		catch
		{
			// Don't leak the socket if the connection fails; the caller sees the original exception
			socket.Dispose();
			throw;
		}
	}
}
