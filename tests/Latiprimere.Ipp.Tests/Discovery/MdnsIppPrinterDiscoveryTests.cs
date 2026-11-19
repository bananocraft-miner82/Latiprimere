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
	public void GivenPrinterAnnouncedAsIppAndIppsWhenToPrintersThenKeepsBothAddresses()
	{
		// Arrange — and another announced over IPPS only
		DnsSdService[] services =
		[
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipps._tcp", "192.168.1.20", 443),
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipp._tcp", "192.168.1.20", 631),
			MdnsIppPrinterDiscoveryTests.CreateService("Secure only", "_ipps._tcp", "192.168.1.30", 631),
		];

		// Act
		List<DiscoveredIppPrinter> printers = MdnsIppPrinterDiscovery.ToPrinters(services);

		// Assert
		DiscoveredIppPrinter office = Assert.Single(printers, printer => printer.InstanceName == "Office");
		Assert.Equal(new Uri("ipp://192.168.1.20:631/ipp/print"), office.PrinterUri);
		Assert.Equal(new Uri("ipps://192.168.1.20:443/ipp/print"), office.SecureUri);

		DiscoveredIppPrinter secureOnly = Assert.Single(printers, printer => printer.InstanceName == "Secure only");
		Assert.Equal("ipps", secureOnly.PrinterUri.Scheme);
		Assert.Null(secureOnly.SecureUri);
	}

	[Fact]
	public void GivenIppsAnnouncementOfTheSameNameFromAnotherHostWhenToPrintersThenItIsntThePrintersIppsAddress()
	{
		// Arrange — anyone can announce an IPPS service with the name of another's IPP one
		DnsSdService[] services =
		[
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipps._tcp", "192.168.1.66", 443),
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipp._tcp", "192.168.1.20", 631),
		];

		// Act
		List<DiscoveredIppPrinter> printers = MdnsIppPrinterDiscovery.ToPrinters(services);

		// Assert
		DiscoveredIppPrinter office = Assert.Single(printers);
		Assert.Equal(new Uri("ipp://192.168.1.20:631/ipp/print"), office.PrinterUri);
		Assert.Null(office.SecureUri);
	}

	[Fact]
	public void GivenIppsAnnouncementWithAnotherUuidWhenToPrintersThenItIsntThePrintersIppsAddress()
	{
		// Arrange — the same host, but announcing another printer's UUID
		DnsSdService[] services =
		[
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipps._tcp", "192.168.1.20", 443) with { Txt = ["UUID=11111111-2222-3333-4444-555555555555"] },
			MdnsIppPrinterDiscoveryTests.CreateService("Office", "_ipp._tcp", "192.168.1.20", 631) with { Txt = ["UUID=00000000-0000-1000-8000-00117d58570e"] },
		];

		// Act
		List<DiscoveredIppPrinter> printers = MdnsIppPrinterDiscovery.ToPrinters(services);

		// Assert
		DiscoveredIppPrinter office = Assert.Single(printers);
		Assert.Null(office.SecureUri);
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
	public void GivenPrintersAnnouncingUuidsWhenFindUuidThenReturnsTheOneWithItOverPlainIpp()
	{
		// Arrange — announced with urn:uuid: and in upper case, as some printers do; and another printer
		const string uuid = "00000000-0000-1000-8000-00117d58570e";

		DnsSdService[] services =
		[
			MdnsIppPrinterDiscoveryTests.CreateService("Canon", "_ipps._tcp", "192.168.1.40", 443) with { Txt = ["rp=ipp/print", "UUID=urn:uuid:00000000-0000-1000-8000-00117D58570E"] },
			MdnsIppPrinterDiscoveryTests.CreateService("Canon", "_ipp._tcp", "192.168.1.40", 631) with { Txt = ["rp=ipp/print", "UUID=" + uuid] },
			MdnsIppPrinterDiscoveryTests.CreateService("Other", "_ipp._tcp", "192.168.1.50", 631) with { Txt = ["rp=ipp/print", "UUID=11111111-2222-3333-4444-555555555555"] },
		];

		// Act
		DiscoveredIppPrinter? found = MdnsIppPrinterDiscovery.FindUuid(services, uuid);
		DiscoveredIppPrinter? missing = MdnsIppPrinterDiscovery.FindUuid(services, "99999999-0000-0000-0000-000000000000");

		// Assert
		Assert.Equal(new Uri("ipp://192.168.1.40:631/ipp/print"), found?.PrinterUri);
		Assert.Null(missing);
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
