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
}
