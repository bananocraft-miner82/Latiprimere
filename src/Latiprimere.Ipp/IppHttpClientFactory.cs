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
		return IppHttpClientFactory.CreateForTcp(connectTimeout, security, Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// Creates an HTTP client that connects over TCP to the host named in each request URI, trying its IPv6 and IPv4
	/// addresses together (see <see cref="HappyEyeballsConnector"/>), protects requests as asked, and cancels a
	/// request whose printer stalls (see <see cref="IppStallGuardHandler"/>).
	/// </summary>
	/// <param name="connectTimeout">The longest wait for each connection to open, across all of the host's
	/// addresses.</param>
	/// <param name="security">Whether to use TLS, the credentials to send, and how to check the server's
	/// certificate.</param>
	/// <param name="uploadStallTimeout">The longest a write of a request's body, or the wait for the answer once it is
	/// sent, may take; or <see cref="Timeout.InfiniteTimeSpan"/> for no limit.</param>
	/// <returns>The HTTP client. The caller owns it.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="connectTimeout"/> or
	/// <paramref name="uploadStallTimeout"/> is not positive (other than infinite), or is longer than about 24
	/// days.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="security"/> is <see langword="null"/>.</exception>
	internal static HttpClient CreateForTcp(TimeSpan connectTimeout, IppConnectionSecurity security, TimeSpan uploadStallTimeout)
	{
		IppHttpClientFactory.ValidateConnectTimeout(connectTimeout);
		IppHttpClientFactory.ValidateUploadStallTimeout(uploadStallTimeout);
		ArgumentNullException.ThrowIfNull(security);

		// The handler cancels the callback's token when the connect timeout runs out
		HappyEyeballsConnector connector = HappyEyeballsConnector.CreateDefault();

		// IPP doesn't redirect: a printer that does is refused with its HTTP status, rather than the request going
		// on to an address nobody configured, as a GET once a POST is redirected
		SocketsHttpHandler handler = new SocketsHttpHandler
		{
			AllowAutoRedirect = false,
			ConnectTimeout    = connectTimeout,
			ConnectCallback   = (context, cancellationToken) =>
			{
				return connector.ConnectAsync(context.DnsEndPoint, cancellationToken);
			}
		};

		// TLS runs over the stream the connect callback returns
		handler.SslOptions.RemoteCertificateValidationCallback = security.ServerCertificateValidationCallback;

		return IppHttpClientFactory.CreateClient(handler, security, uploadStallTimeout);
	}

	/// <summary>
	/// Checks an upload stall timeout is <see cref="Timeout.InfiniteTimeSpan"/>, or one the stall guard accepts.
	/// </summary>
	/// <param name="uploadStallTimeout">The timeout to check.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="uploadStallTimeout"/> is not positive (other than
	/// infinite), or is longer than about 24 days.</exception>
	internal static void ValidateUploadStallTimeout(TimeSpan uploadStallTimeout)
	{
		if (uploadStallTimeout != Timeout.InfiniteTimeSpan)
		{
			IppStallWatch.ValidateStallTimeout(uploadStallTimeout);
		}
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
			AllowAutoRedirect = false,
			ConnectCallback   = (context, cancellationToken) =>
			{
				return IppHttpClientFactory.ConnectToUnixSocketAsync(socketPath, cancellationToken);
			}
		};

		// The scheduler spools a job before printing it, so it doesn't stall as a printer does (ADR 0041)
		IppConnectionSecurity security = new IppConnectionSecurity { Credentials = credentials };

		return IppHttpClientFactory.CreateClient(handler, security, Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// Creates the HTTP client over a handler, adding <see cref="IppConnectionHandler"/> when requests need TLS or
	/// credentials, and <see cref="IppStallGuardHandler"/> when uploads have a stall timeout.
	/// </summary>
	/// <param name="handler">The handler that connects.</param>
	/// <param name="security">How to protect requests.</param>
	/// <param name="uploadStallTimeout">The stall timeout, or <see cref="Timeout.InfiniteTimeSpan"/> for none.</param>
	/// <returns>The HTTP client, which owns the handlers.</returns>
	/// <remarks>
	/// The client has no overall timeout: HttpClient's default of 100 s would also limit uploading a document, and
	/// a large job to a printer that reads its input only as fast as it prints takes longer. Connecting is limited
	/// by the handler's connect timeout, every request other than Print-Job by
	/// <see cref="SharpIppClientAdapter.DefaultResponseTimeout"/>, and Print-Job, where there is one, by the stall
	/// timeout, which limits a lack of progress rather than the whole upload.
	/// </remarks>
	private static HttpClient CreateClient(SocketsHttpHandler handler, IppConnectionSecurity security, TimeSpan uploadStallTimeout)
	{
		HttpMessageHandler outerHandler = !security.UseTls && !security.HasCredentials
			? handler
			: new IppConnectionHandler(security, handler);

		if (uploadStallTimeout != Timeout.InfiniteTimeSpan)
		{
			outerHandler = new IppStallGuardHandler(uploadStallTimeout, outerHandler);
		}

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
