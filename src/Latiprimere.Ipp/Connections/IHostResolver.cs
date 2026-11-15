using System.Net;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Looks up the addresses of a host name.
/// </summary>
internal interface IHostResolver
{
	/// <summary>
	/// Looks up every address of a host, IPv6 and IPv4.
	/// </summary>
	/// <param name="host">The host name.</param>
	/// <param name="cancellationToken">A token to cancel the lookup.</param>
	/// <returns>The addresses, in the order the system returned them.</returns>
	/// <exception cref="System.Net.Sockets.SocketException">The name could not be resolved.</exception>
	Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
