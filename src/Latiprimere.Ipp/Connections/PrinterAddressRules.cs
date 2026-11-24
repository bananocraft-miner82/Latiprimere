using System.Net;
using System.Net.Sockets;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Which addresses a printer found on the network may be reached at (ADR 0058). Anyone on the network can announce a
/// printer at any address, or answer for any <c>.local</c> name, so without these an announcement could make the
/// application connect to its own machine's services, or to a cloud provider's metadata service, and keep that
/// address known as a printer's.
/// </summary>
internal static class PrinterAddressRules
{
	/// <summary>The cloud instance metadata service's IPv4 address, at AWS, Azure, Google Cloud and others.</summary>
	private static readonly IPAddress CloudMetadataIpv4 = IPAddress.Parse("169.254.169.254");

	/// <summary>AWS's instance metadata service over IPv6.</summary>
	private static readonly IPAddress CloudMetadataIpv6 = IPAddress.Parse("fd00:ec2::254");

	/// <summary>
	/// Checks whether an address could be a printer's on the network: not this machine's loopback, an unspecified,
	/// multicast, broadcast or reserved address, or a cloud metadata service.
	/// </summary>
	/// <param name="address">The address.</param>
	/// <returns>Whether a printer found on the network may be reached there.</returns>
	internal static bool CanBePrinter(IPAddress address)
	{
		ArgumentNullException.ThrowIfNull(address);

		IPAddress candidate = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

		if (IPAddress.IsLoopback(candidate)
			|| candidate.Equals(PrinterAddressRules.CloudMetadataIpv4)
			|| candidate.Equals(PrinterAddressRules.CloudMetadataIpv6))
		{
			return false;
		}

		if (candidate.AddressFamily == AddressFamily.InterNetwork)
		{
			// 0.0.0.0/8 is "this network"; 224.0.0.0/4 multicast, and 240.0.0.0/4, broadcast among it, reserved
			byte first = candidate.GetAddressBytes()[0];

			return first is not 0 and < 224;
		}

		if (candidate.AddressFamily == AddressFamily.InterNetworkV6)
		{
			return !candidate.Equals(IPAddress.IPv6Any) && !candidate.IsIPv6Multicast;
		}

		return false;
	}

	/// <summary>
	/// Checks whether a host from an announcement, or in a URI, could be a printer's on the network: an address that
	/// <see cref="CanBePrinter"/> allows, or a name other than <c>localhost</c>.
	/// </summary>
	/// <param name="host">The host, with or without the brackets of an IPv6 address.</param>
	/// <returns>Whether a printer found on the network may be reached there.</returns>
	internal static bool CanBePrinterHost(string host)
	{
		ArgumentNullException.ThrowIfNull(host);

		string bare = host.Trim('[', ']').TrimEnd('.');

		if (IPAddress.TryParse(bare, out IPAddress? address))
		{
			return PrinterAddressRules.CanBePrinter(address);
		}

		return bare.Length > 0
			   && !string.Equals(bare, "localhost", StringComparison.OrdinalIgnoreCase)
			   && !bare.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Checks whether a host name is a multicast DNS name, which anyone on the network can answer for, so its
	/// addresses are held to <see cref="CanBePrinter"/> when it is resolved.
	/// </summary>
	/// <param name="host">The host name.</param>
	/// <returns>Whether it ends in <c>.local</c>.</returns>
	internal static bool IsMulticastDnsName(string host)
	{
		ArgumentNullException.ThrowIfNull(host);

		return host.TrimEnd('.').EndsWith(".local", StringComparison.OrdinalIgnoreCase);
	}
}
