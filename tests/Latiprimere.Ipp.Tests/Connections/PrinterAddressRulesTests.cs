using System.Net;
using Latiprimere.Ipp.Connections;
using Xunit;

namespace Latiprimere.Ipp.Tests.Connections;

public sealed class PrinterAddressRulesTests
{
	[Theory]
	[InlineData("192.168.1.20", true)]
	[InlineData("10.0.0.5", true)]
	[InlineData("172.16.4.9", true)]
	[InlineData("169.254.10.20", true)]
	[InlineData("fe80::1", true)]
	[InlineData("fd00::20", true)]
	[InlineData("2001:db8::1", true)]
	[InlineData("127.0.0.1", false)]
	[InlineData("127.255.0.9", false)]
	[InlineData("::1", false)]
	[InlineData("::ffff:127.0.0.1", false)]
	[InlineData("::ffff:192.168.1.20", true)]
	[InlineData("0.0.0.0", false)]
	[InlineData("0.1.2.3", false)]
	[InlineData("::", false)]
	[InlineData("224.0.0.251", false)]
	[InlineData("239.255.255.250", false)]
	[InlineData("240.0.0.1", false)]
	[InlineData("255.255.255.255", false)]
	[InlineData("ff02::fb", false)]
	[InlineData("169.254.169.254", false)]
	[InlineData("::ffff:169.254.169.254", false)]
	[InlineData("fd00:ec2::254", false)]
	public void GivenAddressWhenCheckedThenOnlyThoseAPrinterOnTheNetworkCouldHaveAreAllowed(string address, bool expected)
	{
		// Arrange — addresses printers have, link-local among them, and ones none has (provided by InlineData above)
		IPAddress parsed = IPAddress.Parse(address);

		// Act
		bool allowed = PrinterAddressRules.CanBePrinter(parsed);

		// Assert
		Assert.Equal(expected, allowed);
	}

	[Theory]
	[InlineData("192.168.1.20", true)]
	[InlineData("[fd00::20]", true)]
	[InlineData("office-laser.local", true)]
	[InlineData("printer.example.com", true)]
	[InlineData("[::1]", false)]
	[InlineData("127.0.0.1", false)]
	[InlineData("localhost", false)]
	[InlineData("LocalHost.", false)]
	[InlineData("printer.localhost", false)]
	[InlineData("", false)]
	public void GivenHostWhenCheckedThenAddressesAreHeldToTheRulesAndLocalhostIsRefused(string host, bool expected)
	{
		// Arrange — host provided by InlineData above

		// Act
		bool allowed = PrinterAddressRules.CanBePrinterHost(host);

		// Assert
		Assert.Equal(expected, allowed);
	}

	[Theory]
	[InlineData("office-laser.local", true)]
	[InlineData("office-laser.LOCAL.", true)]
	[InlineData("printer.example.com", false)]
	[InlineData("local", false)]
	[InlineData("192.168.1.20", false)]
	public void GivenHostNameWhenCheckedThenOnlyDotLocalNamesAreMulticastDns(string host, bool expected)
	{
		// Arrange — host provided by InlineData above

		// Act
		bool isMulticast = PrinterAddressRules.IsMulticastDnsName(host);

		// Assert
		Assert.Equal(expected, isMulticast);
	}
}
