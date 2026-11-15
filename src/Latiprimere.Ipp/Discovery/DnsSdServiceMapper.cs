using System.Net;
using System.Net.Sockets;

namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// Turns DNS-SD announcements of IPP printers into printer URIs and details, following the IPP Everywhere and
/// Bonjour Printing conventions for TXT keys.
/// </summary>
internal static class DnsSdServiceMapper
{
	/// <summary>The DNS-SD service type for plain IPP.</summary>
	internal const string IppServiceType = "_ipp._tcp";

	/// <summary>The DNS-SD service type for IPP over TLS.</summary>
	internal const string IppsServiceType = "_ipps._tcp";

	/// <summary>The resource path used when a printer doesn't announce <c>rp</c>.</summary>
	internal const string DefaultResourcePath = "ipp/print";

	/// <summary>TXT key: resource path.</summary>
	private const string ResourcePathKey = "rp";

	/// <summary>TXT key: make and model.</summary>
	private const string MakeAndModelKey = "ty";

	/// <summary>TXT key: location.</summary>
	private const string LocationKey = "note";

	/// <summary>TXT key: comma-separated document formats.</summary>
	private const string DocumentFormatsKey = "pdl";

	/// <summary>TXT key: the printer's UUID, the same value as its <c>printer-uuid</c> attribute.</summary>
	private const string UuidKey = "UUID";

	/// <summary>The prefix of a UUID written as a URN, as <c>printer-uuid</c> is.</summary>
	private const string UuidUrnPrefix = "urn:uuid:";

	/// <summary>
	/// Builds a discovered printer from an announcement.
	/// </summary>
	/// <param name="service">The announcement.</param>
	/// <returns>The printer, or <see langword="null"/> if the announcement has no usable address, port or
	/// resource path.</returns>
	/// <remarks>
	/// Announcements come from anyone on the local network, so one that doesn't make a valid URI is skipped rather
	/// than thrown about. This also runs on the mDNS browser's own thread, where an exception could end the process.
	/// </remarks>
	internal static DiscoveredIppPrinter? ToDiscoveredPrinter(DnsSdService service)
	{
		ArgumentNullException.ThrowIfNull(service);

		string? host = DnsSdServiceMapper.ChooseHost(service);

		if (host is null || service.Port <= 0 || service.Port > ushort.MaxValue)
		{
			return null;
		}

		Dictionary<string, string> txt = DnsSdServiceMapper.ParseTxt(service.Txt);
		string resourcePath = txt.GetValueOrDefault(DnsSdServiceMapper.ResourcePathKey, DnsSdServiceMapper.DefaultResourcePath);
		bool isSecure = service.ServiceType.StartsWith(DnsSdServiceMapper.IppsServiceType, StringComparison.OrdinalIgnoreCase);
		string scheme = isSecure ? "ipps" : "ipp";
		Uri? printerUri = DnsSdServiceMapper.TryCreatePrinterUri(scheme, host, service.Port, resourcePath);

		if (printerUri is null)
		{
			return null;
		}

		return new DiscoveredIppPrinter
		{
			InstanceName    = service.Instance,
			PrinterUri      = printerUri,
			MakeAndModel    = DnsSdServiceMapper.NullIfBlank(txt.GetValueOrDefault(DnsSdServiceMapper.MakeAndModelKey)),
			Location        = DnsSdServiceMapper.NullIfBlank(txt.GetValueOrDefault(DnsSdServiceMapper.LocationKey)),
			DocumentFormats = DnsSdServiceMapper.SplitList(txt.GetValueOrDefault(DnsSdServiceMapper.DocumentFormatsKey)),
			Uuid            = DnsSdServiceMapper.NormalizeUuid(txt.GetValueOrDefault(DnsSdServiceMapper.UuidKey))
		};
	}

	/// <summary>
	/// Builds the printer's URI from an announcement's parts.
	/// </summary>
	/// <param name="scheme">The scheme, <c>ipp</c> or <c>ipps</c>.</param>
	/// <param name="host">The address or host name.</param>
	/// <param name="port">The port.</param>
	/// <param name="resourcePath">The resource path from the <c>rp</c> TXT key.</param>
	/// <returns>The URI, or <see langword="null"/> if the parts don't make one with exactly that host and port.</returns>
	/// <remarks>
	/// UriBuilder throws for some host names, such as one with a space. For others, .NET 10's quietly makes a URI
	/// to a different host: <c>evil.example/x?</c>, <c>a@b.local</c> and <c>bad/host.local</c> become hosts
	/// <c>evil.example</c>, <c>b.local</c> and <c>bad</c>, without the port, where .NET 11's throws. So the result is
	/// only kept if its host and port are the ones announced.
	/// </remarks>
	private static Uri? TryCreatePrinterUri(string scheme, string host, int port, string resourcePath)
	{
		Uri printerUri;

		try
		{
			printerUri = new UriBuilder(scheme, host, port, "/" + resourcePath.TrimStart('/')).Uri;
		}
		catch (Exception exception) when (exception is UriFormatException or ArgumentException)
		{
			// .NET 11's UriBuilder rejects such a host with ArgumentException; .NET 10's accepts it, see below
			return null;
		}

		bool isAsAnnounced = string.Equals(printerUri.Host, host, StringComparison.OrdinalIgnoreCase)
							 && printerUri.Port == port;

		return isAsAnnounced ? printerUri : null;
	}

	/// <summary>
	/// Puts a UUID into one form for comparison: lower case, without a <c>urn:uuid:</c> prefix.
	/// </summary>
	/// <param name="value">The UUID as announced or as reported in <c>printer-uuid</c>.</param>
	/// <returns>The UUID, or <see langword="null"/> if <paramref name="value"/> is blank.</returns>
	internal static string? NormalizeUuid(string? value)
	{
		string? uuid = DnsSdServiceMapper.NullIfBlank(value)?.Trim();

		if (uuid is null)
		{
			return null;
		}

		if (uuid.StartsWith(DnsSdServiceMapper.UuidUrnPrefix, StringComparison.OrdinalIgnoreCase))
		{
			uuid = uuid[DnsSdServiceMapper.UuidUrnPrefix.Length..];
		}

		return uuid.ToLowerInvariant();
	}

	/// <summary>
	/// Parses TXT entries into a dictionary. Keys are case-insensitive, as DNS-SD specifies; the first occurrence of a
	/// key wins, and entries without '=' are ignored.
	/// </summary>
	/// <param name="entries">The TXT entries.</param>
	/// <returns>The keys and values.</returns>
	internal static Dictionary<string, string> ParseTxt(IEnumerable<string> entries)
	{
		Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		foreach (string entry in entries)
		{
			int separator = entry.IndexOf('=', StringComparison.Ordinal);

			if (separator > 0)
			{
				values.TryAdd(entry[..separator], entry[(separator + 1)..]);
			}
		}

		return values;
	}

	/// <summary>
	/// Chooses the host to put in the printer URI: an IPv4 address if there is one, because it works on every platform
	/// without mDNS name resolution; otherwise the announced host name.
	/// </summary>
	/// <param name="service">The announcement.</param>
	/// <returns>The host, or <see langword="null"/> if there is none.</returns>
	/// <remarks>
	/// IPv6 link-local addresses are avoided because they need a scope identifier that is only valid on this machine.
	/// </remarks>
	private static string? ChooseHost(DnsSdService service)
	{
		IPAddress? ipv4 = service.Addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);

		if (ipv4 is not null)
		{
			return ipv4.ToString();
		}

		return string.IsNullOrWhiteSpace(service.Hostname) ? null : service.Hostname.TrimEnd('.');
	}

	/// <summary>
	/// Splits a comma-separated TXT value.
	/// </summary>
	/// <param name="value">The value, or <see langword="null"/>.</param>
	/// <returns>The trimmed, non-empty items.</returns>
	private static IReadOnlyList<string> SplitList(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return Array.Empty<string>();
		}

		return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
	}

	/// <summary>
	/// Returns <see langword="null"/> for blank values.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The value, or <see langword="null"/>.</returns>
	private static string? NullIfBlank(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}
}
