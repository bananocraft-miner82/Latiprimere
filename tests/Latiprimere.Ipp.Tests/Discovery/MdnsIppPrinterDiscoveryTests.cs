using System.Net;
using Latiprimere.Ipp.Discovery;
using Xunit;

namespace Latiprimere.Ipp.Tests.Discovery;

public sealed class MdnsIppPrinterDiscoveryTests
{
	[Fact]
	public void GivenPrinterAnnouncedAsIppAndIppsWhenToPrintersThenKeepsPlainIpp()
	{
		// Arrange
		DnsSdService[] services = new[]
		{
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipps._tcp", "192.168.1.20", 443),
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipp._tcp", "192.168.1.20", 631),
		};

		// Act
		List<DiscoveredIppPrinter> printers = MdnsIppPrinterDiscovery.ToPrinters(services);

		// Assert
		DiscoveredIppPrinter printer = Assert.Single(printers);
		Assert.Equal("ipp", printer.PrinterUri.Scheme);
	}

	[Fact]
	public void GivenRenamedDuplicatesWithSameUriWhenToPrintersThenKeepsShortestName()
	{
		// Arrange — the same printer re-announced as "Office (2)" after a name conflict
		DnsSdService[] services = new[]
		{
			MdnsIppPrinterDiscoveryTests.CreateService("Office (2)", "_ipp._tcp", "192.168.1.20", 631),
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipp._tcp", "192.168.1.20", 631),
		};

		// Act
		List<DiscoveredIppPrinter> printers = MdnsIppPrinterDiscovery.ToPrinters(services);

		// Assert
		DiscoveredIppPrinter printer = Assert.Single(printers);
		Assert.Equal("Office", printer.InstanceName);
	}

	[Fact]
	public void GivenDifferentPrintersAndAnUnusableAnnouncementWhenToPrintersThenKeepsTheUsableOnes()
	{
		// Arrange
		DnsSdService[] services = new[]
		{
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipp._tcp", "192.168.1.20", 631),
			MdnsIppPrinterDiscoveryTests.CreateService("Warehouse", "_ipp._tcp", "192.168.1.30", 631),
			MdnsIppPrinterDiscoveryTests.CreateService("Broken", "_ipp._tcp", "192.168.1.40", 0),
		};

		// Act
		List<DiscoveredIppPrinter> printers = MdnsIppPrinterDiscovery.ToPrinters(services);

		// Assert
		Assert.Equal(new[] { "Office", "Warehouse" }, printers.Select(printer => printer.InstanceName).Order());
	}

	[Fact]
	public void GivenPrinterAnnouncedAsIppAndIppsWhenFindInstanceThenIgnoresCaseAndReturnsPlainIpp()
	{
		// Arrange
		DnsSdService[] services = new[]
		{
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipps._tcp", "192.168.1.20", 443),
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipp._tcp", "192.168.1.20", 631),
			MdnsIppPrinterDiscoveryTests.CreateService("Warehouse", "_ipp._tcp", "192.168.1.30", 631),
		};

		// Act
		DiscoveredIppPrinter? printer = MdnsIppPrinterDiscovery.FindInstance(services, "OFFICE");

		// Assert
		Assert.Equal(new Uri("ipp://192.168.1.20:631/ipp/print"), printer?.PrinterUri);
	}

	[Fact]
	public void GivenOnlyIppsAnnouncementWhenFindInstanceThenReturnsIt()
	{
		// Arrange
		DnsSdService[] services = new[]
		{
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipps._tcp", "192.168.1.20", 443),
		};

		// Act
		DiscoveredIppPrinter? printer = MdnsIppPrinterDiscovery.FindInstance(services, "Office");

		// Assert
		Assert.Equal("ipps", printer?.PrinterUri.Scheme);
	}

	[Fact]
	public void GivenNoAnnouncementWithTheNameWhenFindInstanceThenReturnsNull()
	{
		// Arrange
		DnsSdService[] services = new[]
		{
			MdnsIppPrinterDiscoveryTests.CreateService("Office (2)", "_ipp._tcp", "192.168.1.20", 631),
		};

		// Act
		DiscoveredIppPrinter? printer = MdnsIppPrinterDiscovery.FindInstance(services, "Office");

		// Assert
		Assert.Null(printer);
	}

	[Theory]
	[InlineData("Office", "_ipp._tcp", 631, true)]
	[InlineData("OFFICE", "_IPP._TCP", 631, true)]
	[InlineData("Office", "_ipps._tcp", 631, false)]
	[InlineData("Office (2)", "_ipp._tcp", 631, false)]
	[InlineData("Office", "_ipp._tcp", 0, false)]
	public void GivenAnnouncementWhenSearchingForOfficeThenOnlyAUsablePlainIppOneEndsTheSearch
	(
		string instance,
		string serviceType,
		int port,
		bool expected
	)
	{
		// Arrange — the announcement's instance, type and port, and whether it should end the search (provided by
		// InlineData above). IPPS doesn't, as plain IPP may still follow and is preferred
		DnsSdService service = MdnsIppPrinterDiscoveryTests.CreateService(instance, serviceType, "192.168.1.20", port);

		// Act
		bool endsSearch = MdnsIppPrinterDiscovery.EndsSearch(service, "Office");

		// Assert
		Assert.Equal(expected, endsSearch);
	}

	/// <summary>
	/// Creates an announcement for a printer at an IPv4 address.
	/// </summary>
	/// <param name="instance">The instance name.</param>
	/// <param name="serviceType">The service type.</param>
	/// <param name="address">The IPv4 address.</param>
	/// <param name="port">The port.</param>
	/// <returns>The service.</returns>
	private static DnsSdService CreateService(string instance, string serviceType, string address, int port)
	{
		return new DnsSdService
		{
			Instance    = instance,
			ServiceType = serviceType,
			Port        = port,
			Addresses   = new[] { IPAddress.Parse(address) }
		};
	}
}
