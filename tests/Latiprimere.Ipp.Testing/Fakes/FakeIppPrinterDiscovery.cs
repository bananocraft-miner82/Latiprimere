using Latiprimere.Ipp.Discovery;

namespace Latiprimere.Ipp.Testing.Fakes;

/// <summary>
/// A discovery that returns a fixed list of printers immediately, and counts how often it is asked.
/// </summary>
public sealed class FakeIppPrinterDiscovery : IIppPrinterDiscovery
{
	private readonly List<DiscoveredIppPrinter> printers = new List<DiscoveredIppPrinter>(); // Printers to report

	/// <summary>Gets the number of times discovery has run.</summary>
	public int DiscoveryCount { get; private set; }

	/// <summary>Gets the duration requested by the last discovery.</summary>
	public TimeSpan LastDuration { get; private set; }

	/// <summary>
	/// Adds a printer for discovery to report.
	/// </summary>
	/// <param name="instanceName">The announced name.</param>
	/// <param name="printerUri">The printer URI.</param>
	/// <param name="uuid">The announced UUID, or <see langword="null"/> for none.</param>
	public void AddPrinter(string instanceName, Uri printerUri, string? uuid = null)
	{
		this.printers.Add
		(
			new DiscoveredIppPrinter
			{
				InstanceName = instanceName,
				PrinterUri   = printerUri,
				MakeAndModel = "Test Printer",
				Location     = "Test lab",
				Uuid         = uuid
			}
		);
	}

	/// <summary>
	/// Forgets every printer added, as if they had all stopped announcing themselves.
	/// </summary>
	public void Clear()
	{
		this.printers.Clear();
	}

	/// <summary>
	/// Returns the printers added.
	/// </summary>
	/// <param name="duration">Recorded, not waited for.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The printers.</returns>
	internal Task<IReadOnlyList<DiscoveredIppPrinter>> DiscoverAsync
	(
		TimeSpan          duration,
		CancellationToken cancellationToken
	)
	{
		this.DiscoveryCount++;
		this.LastDuration = duration;

		return Task.FromResult<IReadOnlyList<DiscoveredIppPrinter>>(this.printers.ToList());
	}

	/// <summary>Gets the names looked up with <c>ResolveAsync</c>, in order.</summary>
	public List<string> ResolvedNames { get; } = new List<string>();

	/// <summary>Gets the UUIDs searched for, in order.</summary>
	public List<string> SearchedUuids { get; } = new List<string>();

	/// <summary>Gets or sets what searching by UUID throws, if anything.</summary>
	public Exception? UuidSearchFailure { get; set; }

	/// <inheritdoc/>
	Task<DiscoveredIppPrinter?> IIppPrinterDiscovery.FindByUuidAsync
	(
		string            uuid,
		TimeSpan          timeout,
		CancellationToken cancellationToken
	)
	{
		this.SearchedUuids.Add(uuid);

		if (this.UuidSearchFailure is not null)
		{
			return Task.FromException<DiscoveredIppPrinter?>(this.UuidSearchFailure);
		}
		DiscoveredIppPrinter? printer = this.printers.FirstOrDefault(candidate => candidate.Uuid == uuid);

		return Task.FromResult(printer);
	}

	/// <inheritdoc/>
	Task<DiscoveredIppPrinter?> IIppPrinterDiscovery.ResolveAsync
	(
		string            instanceName,
		TimeSpan          timeout,
		CancellationToken cancellationToken
	)
	{
		this.ResolvedNames.Add(instanceName);
		DiscoveredIppPrinter? printer = this.printers.FirstOrDefault(candidate =>
			string.Equals(candidate.InstanceName, instanceName, StringComparison.OrdinalIgnoreCase));

		return Task.FromResult(printer);
	}

	/// <inheritdoc/>
	Task<IReadOnlyList<DiscoveredIppPrinter>> IIppPrinterDiscovery.DiscoverAsync
	(
		TimeSpan duration,
		CancellationToken cancellationToken
	)
	{
		return this.DiscoverAsync(duration, cancellationToken);
	}
}
