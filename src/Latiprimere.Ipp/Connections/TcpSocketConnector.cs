using System.Net;
using System.Net.Sockets;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Opens TCP connections with sockets, set up as <see cref="SocketsHttpHandler"/> sets up its own.
/// </summary>
internal sealed class TcpSocketConnector : ITcpConnector
{
	/// <inheritdoc/>
	public async Task<Stream> ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(endPoint);

		// IPP requests are small and answered at once, so don't hold them back to fill packets
		Socket socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

		try
		{
			await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(false);

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
