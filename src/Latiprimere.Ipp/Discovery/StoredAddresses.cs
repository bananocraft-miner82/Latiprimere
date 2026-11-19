namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// The address store's file.
/// </summary>
internal sealed record StoredAddresses
{
	/// <summary>Gets the file format's version, <see cref="IppAddressStore.FormatVersion"/>.</summary>
	public int Version { get; init; }

	/// <summary>Gets the printers remembered.</summary>
	public List<StoredPrinter> Printers { get; init; } = [];
}
