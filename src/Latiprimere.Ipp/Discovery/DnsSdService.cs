using System.Net;

namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// One DNS-SD service announcement, independent of the mDNS library that received it.
/// </summary>
internal sealed record DnsSdService
{
	/// <summary>Gets the service instance name.</summary>
	public required string Instance { get; init; }

	/// <summary>Gets the service type, such as <c>_ipp._tcp</c>.</summary>
	public required string ServiceType { get; init; }

	/// <summary>Gets the full host name from the SRV record, such as <c>printer.local</c>, if any.</summary>
	public string? Hostname { get; init; }

	/// <summary>Gets the port from the SRV record.</summary>
	public required int Port { get; init; }

	/// <summary>Gets the host's addresses.</summary>
	public IReadOnlyList<IPAddress> Addresses { get; init; } = Array.Empty<IPAddress>();

	/// <summary>Gets the TXT record entries, each <c>key=value</c>.</summary>
	public IReadOnlyList<string> Txt { get; init; } = Array.Empty<string>();
}
