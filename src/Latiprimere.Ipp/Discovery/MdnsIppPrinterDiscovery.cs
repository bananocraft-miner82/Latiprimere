using Tmds.MDns;

namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// Finds IPP printers by browsing for <c>_ipp._tcp</c> and <c>_ipps._tcp</c> services over multicast DNS.
/// </summary>
internal sealed class MdnsIppPrinterDiscovery : IIppPrinterDiscovery
{
	/// <summary>The service types browsed for.</summary>
	private static readonly string[] ServiceTypes = new string[]
	{
		DnsSdServiceMapper.IppServiceType,
		DnsSdServiceMapper.IppsServiceType,
	};

	/// <inheritdoc/>
	public async Task<IReadOnlyList<DiscoveredIppPrinter>> DiscoverAsync(TimeSpan duration, CancellationToken cancellationToken)
	{
		AnnouncementCollector collector = new AnnouncementCollector();
		await MdnsIppPrinterDiscovery.BrowseAsync(collector, duration, cancellationToken).ConfigureAwait(false);

		return MdnsIppPrinterDiscovery.ToPrinters(collector.GetServices());
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Stops as soon as the printer is announced over plain IPP. If it only answers over IPPS, that is used once the
	/// timeout has passed.
	/// </remarks>
	public async Task<DiscoveredIppPrinter?> ResolveAsync(string instanceName, TimeSpan timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(instanceName);

		AnnouncementCollector collector = new AnnouncementCollector
		(
			service => MdnsIppPrinterDiscovery.EndsSearch(service, instanceName)
		);

		await MdnsIppPrinterDiscovery.BrowseAsync(collector, timeout, cancellationToken).ConfigureAwait(false);

		return MdnsIppPrinterDiscovery.FindInstance(collector.GetServices(), instanceName);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Stops as soon as the printer is announced over plain IPP. If it only answers over IPPS, that is used once the
	/// timeout has passed.
	/// </remarks>
	public async Task<DiscoveredIppPrinter?> FindByUuidAsync(string uuid, TimeSpan timeout, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(uuid);

		AnnouncementCollector collector = new AnnouncementCollector
		(
			service => service.ServiceType.StartsWith(DnsSdServiceMapper.IppServiceType, StringComparison.OrdinalIgnoreCase)
				&& DnsSdServiceMapper.ToDiscoveredPrinter(service)?.Uuid == uuid
		);

		await MdnsIppPrinterDiscovery.BrowseAsync(collector, timeout, cancellationToken).ConfigureAwait(false);

		return MdnsIppPrinterDiscovery.FindUuid(collector.GetServices(), uuid);
	}

	/// <summary>
	/// Picks the printer announcing a UUID from the services found, preferring plain IPP over IPPS.
	/// </summary>
	/// <param name="services">The services found.</param>
	/// <param name="uuid">The normalised UUID.</param>
	/// <returns>The printer, or <see langword="null"/> if none announced it.</returns>
	internal static DiscoveredIppPrinter? FindUuid(IEnumerable<DnsSdService> services, string uuid)
	{
		return MdnsIppPrinterDiscovery.ToPrinters(services).FirstOrDefault(printer => printer.Uuid == uuid);
	}

	/// <summary>
	/// Maps services to printers, keeping one per instance name and one per URI, and preferring plain IPP over IPPS,
	/// because network printers usually present self-signed certificates that clients reject.
	/// </summary>
	/// <param name="services">The services found.</param>
	/// <returns>The printers.</returns>
	/// <remarks>
	/// A printer is often announced several times: on each network interface, as both IPP and IPPS, or under
	/// renamed instances ("Printer (2)") after a name conflict. Only one of each is kept.
	/// </remarks>
	internal static List<DiscoveredIppPrinter> ToPrinters(IEnumerable<DnsSdService> services)
	{
		List<DiscoveredIppPrinter> all = services
			.OrderBy(service => service.ServiceType.StartsWith(DnsSdServiceMapper.IppsServiceType, StringComparison.OrdinalIgnoreCase))
			.ThenBy(service => service.Instance.Length)
			.Select(DnsSdServiceMapper.ToDiscoveredPrinter)
			.OfType<DiscoveredIppPrinter>()
			.ToList();

		// An instance announced over both keeps its IPPS address too, for when its certificate is trusted
		return all
			.DistinctBy(printer => printer.InstanceName, StringComparer.OrdinalIgnoreCase)
			.DistinctBy(printer => printer.PrinterUri)
			.Select(printer => printer.PrinterUri.Scheme != "ipp" ? printer : printer with
			{
				SecureUri = all.FirstOrDefault(other => MdnsIppPrinterDiscovery.IsSecureAddressOf(other, printer))?.PrinterUri
			})
			.ToList();
	}

	/// <summary>
	/// Determines whether a service announcement ends the search for a printer by name: a usable plain IPP
	/// announcement of that instance. An IPPS one doesn't, as plain IPP may still follow and is preferred.
	/// </summary>
	/// <param name="service">The service announced.</param>
	/// <param name="instanceName">The instance name searched for, compared without regard to case.</param>
	/// <returns><see langword="true"/> if the search can stop.</returns>
	internal static bool EndsSearch(DnsSdService service, string instanceName)
	{
		return MdnsIppPrinterDiscovery.IsInstance(service, instanceName)
			&& service.ServiceType.StartsWith(DnsSdServiceMapper.IppServiceType, StringComparison.OrdinalIgnoreCase)
			&& DnsSdServiceMapper.ToDiscoveredPrinter(service) is not null;
	}

	/// <summary>
	/// Picks the printer with a given instance name from the services found, preferring plain IPP over IPPS.
	/// </summary>
	/// <param name="services">The services found.</param>
	/// <param name="instanceName">The instance name, compared without regard to case.</param>
	/// <returns>The printer, or <see langword="null"/> if none has that name.</returns>
	internal static DiscoveredIppPrinter? FindInstance(IEnumerable<DnsSdService> services, string instanceName)
	{
		IEnumerable<DnsSdService> matches = services.Where(service => MdnsIppPrinterDiscovery.IsInstance(service, instanceName));

		return MdnsIppPrinterDiscovery.ToPrinters(matches).FirstOrDefault();
	}

	/// <summary>
	/// Browses until the duration passes or the collector finds the service it wants.
	/// </summary>
	/// <param name="collector">Collects the announcements.</param>
	/// <param name="duration">How long to browse at most.</param>
	/// <param name="cancellationToken">A token to stop browsing early.</param>
	/// <returns>A task that completes when browsing has stopped.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	private static async Task BrowseAsync(AnnouncementCollector collector, TimeSpan duration, CancellationToken cancellationToken)
	{
		// Collect services from the browser's events: its own collection is cleared by StopBrowse, and isn't
		// safe to read while it is being updated on the browser's threads
		ServiceBrowser browser = new ServiceBrowser();
		browser.ServiceAdded += collector.Browser_ServiceAdded;
		browser.ServiceChanged += collector.Browser_ServiceAdded;
		browser.ServiceRemoved += collector.Browser_ServiceRemoved;

		browser.StartBrowse(MdnsIppPrinterDiscovery.ServiceTypes, useSynchronizationContext: false);

		try
		{
			// Cancelling the delay once the wanted service arrives releases its timer straight away
			using CancellationTokenSource delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

			Task delay = Task.Delay(duration, delayCancellation.Token);
			await Task.WhenAny(delay, collector.WantedServiceFound).ConfigureAwait(false);
			delayCancellation.Cancel();
			cancellationToken.ThrowIfCancellationRequested();
		}
		finally
		{
			// StopBrowse reports every known service as removed, so stop listening before stopping the browser
			browser.ServiceAdded -= collector.Browser_ServiceAdded;
			browser.ServiceChanged -= collector.Browser_ServiceAdded;
			browser.ServiceRemoved -= collector.Browser_ServiceRemoved;
			browser.StopBrowse();
		}
	}

	/// <summary>
	/// Checks whether a service has the given instance name, ignoring case as DNS does.
	/// </summary>
	/// <param name="service">The service.</param>
	/// <param name="instanceName">The instance name.</param>
	/// <returns>Whether the names match.</returns>
	private static bool IsInstance(DnsSdService service, string instanceName)
	{
		return string.Equals(service.Instance, instanceName, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Checks whether an IPPS announcement is the same printer's as a plain IPP one: the same name, from the same host,
	/// and not with another UUID.
	/// </summary>
	/// <param name="secure">The IPPS announcement.</param>
	/// <param name="plain">The plain IPP announcement.</param>
	/// <returns>Whether its address is the printer's IPPS address.</returns>
	/// <remarks>
	/// A name is unique only within a service type, so anyone on the network may announce an IPPS service with the
	/// name of another's IPP one. The host must match, so a lookup preferring IPPS doesn't go elsewhere.
	/// </remarks>
	private static bool IsSecureAddressOf(DiscoveredIppPrinter secure, DiscoveredIppPrinter plain)
	{
		return secure.PrinterUri.Scheme == "ipps"
			&& string.Equals(secure.InstanceName, plain.InstanceName, StringComparison.OrdinalIgnoreCase)
			&& string.Equals(secure.PrinterUri.Host, plain.PrinterUri.Host, StringComparison.OrdinalIgnoreCase)
			&& (secure.Uuid is null || plain.Uuid is null || secure.Uuid == plain.Uuid);
	}
}
