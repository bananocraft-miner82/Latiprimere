namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// Finds IPP printers on the local network.
/// </summary>
internal interface IIppPrinterDiscovery
{
	/// <summary>
	/// Listens for printer announcements for a while and returns what was found.
	/// </summary>
	/// <param name="duration">How long to listen.</param>
	/// <param name="cancellationToken">A token to stop listening early.</param>
	/// <returns>The printers found, one per printer even if announced on several interfaces or service types.</returns>
	Task<IReadOnlyList<DiscoveredIppPrinter>> DiscoverAsync(TimeSpan duration, CancellationToken cancellationToken);

	/// <summary>
	/// Looks for one printer by its announced name, returning as soon as it answers.
	/// </summary>
	/// <param name="instanceName">The DNS-SD instance name, compared without regard to case.</param>
	/// <param name="timeout">How long to listen at most.</param>
	/// <param name="cancellationToken">A token to stop listening early.</param>
	/// <returns>The printer, or <see langword="null"/> if it didn't answer in time.</returns>
	Task<DiscoveredIppPrinter?> ResolveAsync(string instanceName, TimeSpan timeout, CancellationToken cancellationToken);

	/// <summary>
	/// Searches the network for the printer announcing a UUID, as a Windows WSD queue records its printer's
	/// (ADR 0036). Stops as soon as it is announced over plain IPP.
	/// </summary>
	/// <param name="uuid">The UUID, normalised by <see cref="DnsSdServiceMapper.NormalizeUuid"/>.</param>
	/// <param name="timeout">How long to search at most.</param>
	/// <param name="cancellationToken">A token to stop searching.</param>
	/// <returns>The printer, or <see langword="null"/> if none announced that UUID in time.</returns>
	Task<DiscoveredIppPrinter?> FindByUuidAsync(string uuid, TimeSpan timeout, CancellationToken cancellationToken);
}
