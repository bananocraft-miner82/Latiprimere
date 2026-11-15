using System.Net;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Looks up host names through the operating system's resolver.
/// </summary>
internal sealed class DnsHostResolver : IHostResolver
{
	/// <inheritdoc/>
	public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
	{
		return Dns.GetHostAddressesAsync(host, cancellationToken);
	}
}
