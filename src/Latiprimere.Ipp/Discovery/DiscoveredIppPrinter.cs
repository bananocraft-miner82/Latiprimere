namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// An IPP printer found on the network through DNS-SD (Bonjour/mDNS).
/// </summary>
internal sealed record DiscoveredIppPrinter
{
	/// <summary>Gets the service instance name, which is the printer's display name, such as "Office Laser".</summary>
	public required string InstanceName { get; init; }

	/// <summary>Gets the URI to send IPP requests to: over plain IPP if announced, otherwise over IPPS.</summary>
	public required Uri PrinterUri { get; init; }

	/// <summary>
	/// Gets the printer's IPPS URI when it announced both, for use instead of <see cref="PrinterUri"/> once its
	/// certificate is trusted (ADR 0037); or <see langword="null"/> when it announced one only.
	/// </summary>
	public Uri? SecureUri { get; init; }

	/// <summary>Gets the make and model from the <c>ty</c> TXT key, if announced.</summary>
	public string? MakeAndModel { get; init; }

	/// <summary>Gets the location from the <c>note</c> TXT key, if announced.</summary>
	public string? Location { get; init; }

	/// <summary>Gets the document formats from the <c>pdl</c> TXT key; empty if not announced.</summary>
	public IReadOnlyList<string> DocumentFormats { get; init; } = Array.Empty<string>();

	/// <summary>
	/// Gets the printer's UUID from the <c>UUID</c> TXT key, in lower case without a <c>urn:uuid:</c> prefix; or
	/// <see langword="null"/> if not announced.
	/// </summary>
	public string? Uuid { get; init; }
}
