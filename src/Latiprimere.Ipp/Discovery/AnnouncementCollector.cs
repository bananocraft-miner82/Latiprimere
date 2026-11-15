using Tmds.MDns;

namespace Latiprimere.Ipp.Discovery;

/// <summary>
/// Keeps the current DNS-SD services reported by a service browser's events, safely across threads.
/// </summary>
/// <remarks>
/// Announcements are copied when their event arrives, because the browser later changes the same objects: stopping
/// it marks every announcement as removed.
/// </remarks>
internal sealed class AnnouncementCollector
{
	private readonly Lock gate = new Lock(); // Guards the dictionary, which browser threads update
	private readonly Dictionary<(string Instance, string Type), DnsSdService> services =
		new Dictionary<(string Instance, string Type), DnsSdService>(); // Current services, copied, by identity
	private readonly Func<DnsSdService, bool>? isWanted; // Recognises the service being looked for, if any
	private readonly TaskCompletionSource wantedServiceFound =
		new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); // Completes when it appears

	/// <summary>
	/// Initialises a new instance of the <see cref="AnnouncementCollector"/> class.
	/// </summary>
	/// <param name="isWanted">
	/// Recognises a service being looked for, to complete <see cref="WantedServiceFound"/> when it is announced; or
	/// <see langword="null"/> to collect everything for as long as the browser runs.
	/// </param>
	public AnnouncementCollector(Func<DnsSdService, bool>? isWanted = null)
	{
		this.isWanted = isWanted;
	}

	/// <summary>
	/// Gets a task that completes when a wanted service is announced. Never completes without a wanted-service test.
	/// </summary>
	public Task WantedServiceFound
	{
		get
		{
			return this.wantedServiceFound.Task;
		}
	}

	/// <summary>
	/// Records a new or changed announcement.
	/// </summary>
	/// <param name="sender">The service browser.</param>
	/// <param name="e">The announcement.</param>
	public void Browser_ServiceAdded(object? sender, ServiceAnnouncementEventArgs e)
	{
		this.Add(AnnouncementCollector.ToService(e.Announcement));
	}

	/// <summary>
	/// Forgets a service that has gone.
	/// </summary>
	/// <param name="sender">The service browser.</param>
	/// <param name="e">The announcement.</param>
	public void Browser_ServiceRemoved(object? sender, ServiceAnnouncementEventArgs e)
	{
		this.Remove(AnnouncementCollector.ToService(e.Announcement));
	}

	/// <summary>
	/// Records a new or changed service, and completes <see cref="WantedServiceFound"/> if it is the one wanted.
	/// </summary>
	/// <param name="service">The service.</param>
	public void Add(DnsSdService service)
	{
		lock (this.gate)
		{
			this.services[AnnouncementCollector.KeyOf(service)] = service;
		}

		if (this.isWanted is not null && this.isWanted(service))
		{
			this.wantedServiceFound.TrySetResult();
		}
	}

	/// <summary>
	/// Forgets a service that has gone.
	/// </summary>
	/// <param name="service">The service.</param>
	public void Remove(DnsSdService service)
	{
		lock (this.gate)
		{
			this.services.Remove(AnnouncementCollector.KeyOf(service));
		}
	}

	/// <summary>
	/// Returns a snapshot of the current services.
	/// </summary>
	/// <returns>The services.</returns>
	public IReadOnlyList<DnsSdService> GetServices()
	{
		lock (this.gate)
		{
			return this.services.Values.ToList();
		}
	}

	/// <summary>
	/// Copies a Tmds.MDns announcement into the library-independent, immutable form.
	/// </summary>
	/// <param name="announcement">The announcement.</param>
	/// <returns>The service.</returns>
	private static DnsSdService ToService(ServiceAnnouncement announcement)
	{
		return new DnsSdService
		{
			Instance    = announcement.Instance,
			ServiceType = announcement.Type,
			Hostname    = announcement.Hostname,
			Port        = announcement.Port,
			Addresses   = announcement.Addresses?.ToList() ?? [],
			Txt         = announcement.Txt?.ToList() ?? []
		};
	}

	/// <summary>
	/// Identifies a service by instance name and type, ignoring case as DNS does.
	/// </summary>
	/// <param name="service">The service.</param>
	/// <returns>The key.</returns>
	private static (string Instance, string Type) KeyOf(DnsSdService service)
	{
		return (service.Instance.ToUpperInvariant(), service.ServiceType.ToUpperInvariant());
	}
}
