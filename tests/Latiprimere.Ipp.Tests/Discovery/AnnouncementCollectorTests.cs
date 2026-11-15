using System.Net;
using Latiprimere.Ipp.Discovery;
using Xunit;

namespace Latiprimere.Ipp.Tests.Discovery;

public sealed class AnnouncementCollectorTests
{
	[Fact]
	public void GivenServiceAnnouncedAgainWithChangesWhenAddedThenReplacesIt()
	{
		// Arrange — the same service, its names in other case, now on another port
		AnnouncementCollector collector = new AnnouncementCollector();
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipp._tcp", 631));

		// Act
		collector.Add(AnnouncementCollectorTests.CreateService("OFFICE", "_IPP._TCP", 8631));

		// Assert
		DnsSdService service = Assert.Single(collector.GetServices());
		Assert.Equal(8631, service.Port);
	}

	[Fact]
	public void GivenSameInstanceAsIppAndIppsWhenAddedThenKeepsBoth()
	{
		// Arrange
		AnnouncementCollector collector = new AnnouncementCollector();

		// Act
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipp._tcp", 631));
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipps._tcp", 443));

		// Assert — they're different services; choosing between them is for the discovery
		Assert.Equal(2, collector.GetServices().Count);
	}

	[Fact]
	public void GivenAnnouncedServiceWhenRemovedThenNoLongerKept()
	{
		// Arrange
		AnnouncementCollector collector = new AnnouncementCollector();
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipp._tcp", 631));
		collector.Add(AnnouncementCollectorTests.CreateService("Warehouse", "_ipp._tcp", 631));

		// Act — the removal names the service in other case
		collector.Remove(AnnouncementCollectorTests.CreateService("office", "_ipp._tcp", 631));

		// Assert
		DnsSdService service = Assert.Single(collector.GetServices());
		Assert.Equal("Warehouse", service.Instance);
	}

	[Fact]
	public void GivenUnknownServiceWhenRemovedThenOthersAreKept()
	{
		// Arrange — a removal can arrive for a service whose announcement never did
		AnnouncementCollector collector = new AnnouncementCollector();
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipp._tcp", 631));

		// Act
		collector.Remove(AnnouncementCollectorTests.CreateService("Warehouse", "_ipp._tcp", 631));

		// Assert
		Assert.Single(collector.GetServices());
	}

	[Fact]
	public void GivenServicesTakenWhenAnotherAnnouncedThenEarlierListIsUnchanged()
	{
		// Arrange
		AnnouncementCollector collector = new AnnouncementCollector();
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipp._tcp", 631));
		IReadOnlyList<DnsSdService> services = collector.GetServices();

		// Act
		collector.Add(AnnouncementCollectorTests.CreateService("Warehouse", "_ipp._tcp", 631));

		// Assert — a snapshot, so callers can enumerate it while browser threads keep adding
		Assert.Single(services);
		Assert.Equal(2, collector.GetServices().Count);
	}

	[Fact]
	public void GivenWantedServiceTestWhenOtherServicesAnnouncedThenWaitsUntilTheWantedOne()
	{
		// Arrange
		AnnouncementCollector collector = new AnnouncementCollector(AnnouncementCollectorTests.IsOffice);

		// Act
		collector.Add(AnnouncementCollectorTests.CreateService("Warehouse", "_ipp._tcp", 631));
		bool isFoundEarly = collector.WantedServiceFound.IsCompleted;
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipp._tcp", 631));
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipp._tcp", 8631));

		// Assert — completed once, however often the wanted service is announced
		Assert.False(isFoundEarly);
		Assert.True(collector.WantedServiceFound.IsCompletedSuccessfully);
	}

	[Fact]
	public void GivenNoWantedServiceTestWhenServicesAnnouncedThenNeverReportsAWantedOne()
	{
		// Arrange — listing collects everything for as long as the browser runs
		AnnouncementCollector collector = new AnnouncementCollector();

		// Act
		collector.Add(AnnouncementCollectorTests.CreateService("Office", "_ipp._tcp", 631));

		// Assert
		Assert.False(collector.WantedServiceFound.IsCompleted);
	}

	/// <summary>
	/// Recognises the service named Office.
	/// </summary>
	/// <param name="service">The service.</param>
	/// <returns><see langword="true"/> for Office.</returns>
	private static bool IsOffice(DnsSdService service)
	{
		return service.Instance == "Office";
	}

	/// <summary>
	/// Creates a service for a printer at 192.168.1.20.
	/// </summary>
	/// <param name="instance">The instance name.</param>
	/// <param name="serviceType">The service type.</param>
	/// <param name="port">The port.</param>
	/// <returns>The service.</returns>
	private static DnsSdService CreateService(string instance, string serviceType, int port)
	{
		return new DnsSdService
		{
			Instance    = instance,
			ServiceType = serviceType,
			Port        = port,
			Addresses   = new[] { IPAddress.Parse("192.168.1.20") }
		};
	}
}
