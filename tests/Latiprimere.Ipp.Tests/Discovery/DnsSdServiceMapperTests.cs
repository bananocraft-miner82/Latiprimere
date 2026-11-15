using System.Net;
using Latiprimere.Ipp.Discovery;
using Xunit;

namespace Latiprimere.Ipp.Tests.Discovery;

public sealed class DnsSdServiceMapperTests
{
	[Fact]
	public void GivenFullAnnouncementWhenMappedThenBuildsUriAndReadsTxtKeys()
	{
		// Arrange
		DnsSdService service = new DnsSdService
		{
			Instance    = "Office Laser",
			ServiceType = "_ipp._tcp",
			Hostname    = "office-laser.local.",
			Port        = 631,
			Addresses   = new[] { IPAddress.Parse("fe80::1"), IPAddress.Parse("192.168.1.20") },
			Txt         = new[]
			{
				"rp=ipp/print",
				"ty=Acme LaserJet 9000",
				"note=Second floor",
				"pdl=application/pdf, image/urf"
			}
		};

		// Act
		DiscoveredIppPrinter? printer = DnsSdServiceMapper.ToDiscoveredPrinter(service);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal("Office Laser", printer.InstanceName);
		Assert.Equal(new Uri("ipp://192.168.1.20:631/ipp/print"), printer.PrinterUri);
		Assert.Equal("Acme LaserJet 9000", printer.MakeAndModel);
		Assert.Equal("Second floor", printer.Location);
		Assert.Equal(new[] { "application/pdf", "image/urf" }, printer.DocumentFormats);
	}

	[Theory]
	[InlineData("bad host.local.")]
	[InlineData("bad/host.local.")]
	[InlineData("evil.example/x?")]
	[InlineData("a@b.local.")]
	public void GivenHostNameThatMakesNoUriWhenMappedThenSkipsTheAnnouncement(string hostname)
	{
		// Arrange — hostname can't be a URI host, or would turn into a different one (provided by InlineData
		// above), and there's no IPv4 address to use instead; anyone on the network can announce this
		DnsSdService service = new DnsSdService
		{
			Instance    = "Office Laser",
			ServiceType = "_ipp._tcp",
			Hostname    = hostname,
			Port        = 631,
			Addresses   = new[] { IPAddress.Parse("fe80::1") }
		};

		// Act
		DiscoveredIppPrinter? printer = DnsSdServiceMapper.ToDiscoveredPrinter(service);

		// Assert — skipped, not thrown, as this also runs on the mDNS browser's thread
		Assert.Null(printer);
	}

	[Fact]
	public void GivenNoIpv4AddressWhenMappedThenUsesHostNameWithoutTrailingDot()
	{
		// Arrange — only an IPv6 link-local address, which needs a machine-specific scope to use
		DnsSdService service = new DnsSdService
		{
			Instance    = "Office Laser",
			ServiceType = "_ipp._tcp",
			Hostname    = "office-laser.local.",
			Port        = 631,
			Addresses   = new[] { IPAddress.Parse("fe80::1") }
		};

		// Act
		DiscoveredIppPrinter? printer = DnsSdServiceMapper.ToDiscoveredPrinter(service);

		// Assert
		Assert.Equal("office-laser.local", printer?.PrinterUri.Host);
	}

	[Fact]
	public void GivenIppsServiceWithoutResourcePathWhenMappedThenUsesIppsSchemeAndDefaultPath()
	{
		// Arrange
		DnsSdService service = new DnsSdService
		{
			Instance    = "Office Laser",
			ServiceType = "_ipps._tcp",
			Port        = 443,
			Addresses   = new[] { IPAddress.Parse("192.168.1.20") }
		};

		// Act
		DiscoveredIppPrinter? printer = DnsSdServiceMapper.ToDiscoveredPrinter(service);

		// Assert
		Assert.Equal(new Uri("ipps://192.168.1.20:443/ipp/print"), printer?.PrinterUri);
		Assert.Empty(printer!.DocumentFormats); // Not null: asserted on the line above
	}

	[Theory]
	[InlineData(null, 631)]       // no host name and no address
	[InlineData("printer", 0)]    // no port
	public void GivenUnusableAnnouncementWhenMappedThenReturnsNull(string? hostname, int port)
	{
		// Arrange — an announcement missing its host or port (provided by InlineData above)
		DnsSdService service = new DnsSdService
		{
			Instance    = "Broken",
			ServiceType = "_ipp._tcp",
			Hostname    = hostname,
			Port        = port
		};

		// Act
		DiscoveredIppPrinter? printer = DnsSdServiceMapper.ToDiscoveredPrinter(service);

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public void GivenAnnouncedUuidWhenMappedThenKeepsItInComparableForm()
	{
		// Arrange
		DnsSdService service = new DnsSdService
		{
			Instance    = "Office Laser",
			ServiceType = "_ipp._tcp",
			Port        = 631,
			Addresses   = new[] { IPAddress.Parse("192.168.1.20") },
			Txt         = new[] { "UUID=6D6F7A00-0000-4000-8000-000000000001" }
		};

		// Act
		DiscoveredIppPrinter? printer = DnsSdServiceMapper.ToDiscoveredPrinter(service);

		// Assert
		Assert.Equal("6d6f7a00-0000-4000-8000-000000000001", printer?.Uuid);
	}

	[Theory]
	[InlineData("urn:uuid:6D6F7A00-0000-4000-8000-000000000001", "6d6f7a00-0000-4000-8000-000000000001")]
	[InlineData("URN:UUID:6d6f7a00-0000-4000-8000-000000000001", "6d6f7a00-0000-4000-8000-000000000001")]
	[InlineData(" 6d6f7a00-0000-4000-8000-000000000001 ", "6d6f7a00-0000-4000-8000-000000000001")]
	[InlineData("  ", null)]
	[InlineData(null, null)]
	public void GivenUuidInAnyFormWhenNormalizedThenIsLowerCaseWithoutUrnPrefix(string? value, string? expected)
	{
		// Arrange — UUIDs as announced or as reported in printer-uuid (provided by InlineData above)

		// Act
		string? uuid = DnsSdServiceMapper.NormalizeUuid(value);

		// Assert
		Assert.Equal(expected, uuid);
	}

	[Fact]
	public void GivenTxtEntriesWhenParsedThenKeysIgnoreCaseFirstWinsAndBareEntriesAreSkipped()
	{
		// Arrange
		string[] entries = new[] { "TY=First", "ty=Second", "Color", "empty=", "=novalue" };

		// Act
		Dictionary<string, string> values = DnsSdServiceMapper.ParseTxt(entries);

		// Assert
		Assert.Equal("First", values["ty"]);
		Assert.Equal(string.Empty, values["empty"]);
		Assert.False(values.ContainsKey("Color"));
		Assert.Equal(2, values.Count);
	}

	[Fact]
	public void GivenResourcePathWithLeadingSlashWhenMappedThenPathHasOneSlash()
	{
		// Arrange
		DnsSdService service = new DnsSdService
		{
			Instance    = "Printer",
			ServiceType = "_ipp._tcp",
			Port        = 631,
			Addresses   = new[] { IPAddress.Parse("10.0.0.5") },
			Txt         = new[] { "rp=/printers/office" }
		};

		// Act
		DiscoveredIppPrinter? printer = DnsSdServiceMapper.ToDiscoveredPrinter(service);

		// Assert
		Assert.Equal("/printers/office", printer?.PrinterUri.AbsolutePath);
	}
}
