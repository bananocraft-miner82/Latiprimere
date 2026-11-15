using System.Net;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Opens a TCP connection to one address.
/// </summary>
internal interface ITcpConnector
{
	/// <summary>
	/// Connects to an address and port.
	/// </summary>
	/// <param name="endPoint">The address and port.</param>
	/// <param name="cancellationToken">A token to abandon the attempt.</param>
	/// <returns>A stream over the connection. The caller owns it.</returns>
	/// <exception cref="System.Net.Sockets.SocketException">The connection failed.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	Task<Stream> ConnectAsync(IPEndPoint endPoint, CancellationToken cancellationToken);
}
