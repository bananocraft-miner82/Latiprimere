using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Testing.Builders;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

public sealed class PrinterUriRequirementsReaderTests
{
	[Fact]
	public void GivenHpEnvyAddressesWhenReadForItsIpAddressThenNeedsNothingAndOffersItsIppsAddress()
	{
		// Arrange — as the HP ENVY 5640 reports itself, by its .local name, reached by its IP address
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder()
			.WithUriAuthentication(("ipp://HP-ENVY.local.:631/ipp/print", "none"), ("ipps://HP-ENVY.local.:443/ipp/print", "none"))
			.Build();

		// Act
		PrinterUriRequirements? requirements = PrinterUriRequirementsReader.Read(attributes, new Uri("ipp://192.0.2.10:631/ipp/print"));

		// Assert — the IPPS address keeps its port and path, at the host used
		Assert.NotNull(requirements);
		Assert.Equal("none", requirements.Authentication);
		Assert.False(requirements.AsksForPassword);
		Assert.False(requirements.AsksForUnsupportedAuthentication);
		Assert.Equal(new Uri("ipps://192.0.2.10:443/ipp/print"), requirements.SecureAlternative);
	}

	[Fact]
	public void GivenPasswordOnIppsAddressWhenReadForItThenAsksForPasswordWithoutAlternative()
	{
		// Arrange
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder()
			.WithUriAuthentication(("ipp://printer.local/ipp/print", "requesting-user-name"), ("ipps://printer.local/ipp/print", "basic"))
			.Build();

		// Act
		PrinterUriRequirements? requirements = PrinterUriRequirementsReader.Read(attributes, new Uri("ipps://192.0.2.10:631/ipp/print"));

		// Assert — an address without a port has IPP's 631
		Assert.NotNull(requirements);
		Assert.True(requirements.AsksForPassword);
		Assert.Null(requirements.SecureAlternative);
	}

	[Fact]
	public void GivenUserNameOnlyWhenReadThenNeedsNoPassword()
	{
		// Arrange — as CUPS queues report themselves
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder()
			.WithUriAuthentication(("ipp://localhost:631/printers/Raw", "requesting-user-name"))
			.Build();

		// Act
		PrinterUriRequirements? requirements = PrinterUriRequirementsReader.Read(attributes, new Uri("ipp://192.0.2.10:631/printers/Raw"));

		// Assert
		Assert.NotNull(requirements);
		Assert.False(requirements.AsksForPassword);
		Assert.False(requirements.AsksForUnsupportedAuthentication);
		Assert.Null(requirements.SecureAlternative);
	}

	[Theory]
	[InlineData("digest")]
	[InlineData("certificate")]
	[InlineData("negotiate")]
	[InlineData("Something-New")]
	public void GivenAuthenticationLatiprimereCantGiveWhenReadThenSaysSo(string keyword)
	{
		// Arrange — keyword provided by InlineData above
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder()
			.WithUriAuthentication(("ipp://printer.local/ipp/print", keyword))
			.Build();

		// Act
		PrinterUriRequirements? requirements = PrinterUriRequirementsReader.Read(attributes, new Uri("ipp://printer.local/ipp/print"));

		// Assert
		Assert.NotNull(requirements);
		Assert.True(requirements.AsksForUnsupportedAuthentication);
		Assert.False(requirements.AsksForPassword);
		Assert.Equal(keyword.ToLowerInvariant(), requirements.Authentication);
	}

	[Fact]
	public void GivenPathWithTrailingSlashAndOtherCaseWhenReadThenMatches()
	{
		// Arrange
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder()
			.WithUriAuthentication(("ipp://printer.local:631/IPP/Print/", "basic"))
			.Build();

		// Act
		PrinterUriRequirements? requirements = PrinterUriRequirementsReader.Read(attributes, new Uri("ipp://192.0.2.10/ipp/print"));

		// Assert
		Assert.True(requirements?.AsksForPassword);
	}

	[Theory]
	[InlineData("ipps://192.0.2.10:631/ipp/print")]
	[InlineData("ipp://192.0.2.10:8631/ipp/print")]
	[InlineData("ipp://192.0.2.10:631/ipp/other")]
	public void GivenNoEntryForTheAddressUsedWhenReadThenReturnsNull(string used)
	{
		// Arrange — another scheme, port or path (provided by InlineData above)
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder()
			.WithUriAuthentication(("ipp://printer.local:631/ipp/print", "basic"))
			.Build();

		// Act
		PrinterUriRequirements? requirements = PrinterUriRequirementsReader.Read(attributes, new Uri(used));

		// Assert
		Assert.Null(requirements);
	}

	[Fact]
	public void GivenPrinterThatDoesNotSayWhenReadThenReturnsNull()
	{
		// Arrange
		PrinterDescriptionAttributes attributes = new PrinterAttributesBuilder().Build();

		// Act
		PrinterUriRequirements? requirements = PrinterUriRequirementsReader.Read(attributes, new Uri("ipp://printer.local/ipp/print"));

		// Assert
		Assert.Null(requirements);
	}

	[Fact]
	public void GivenFewerAuthenticationsThanAddressesWhenReadForTheLastThenReturnsNull()
	{
		// Arrange — the lists are parallel, so the second address has nothing beside it
		PrinterDescriptionAttributes attributes = new PrinterDescriptionAttributes
		{
			PrinterUriSupported = new IppValue<Uri[]>([new Uri("ipp://printer.local/ipp/print"), new Uri("ipps://printer.local/ipp/print")]),
			UriAuthenticationSupported = new IppValue<UriAuthentication[]>([new UriAuthentication("none")])
		};

		// Act
		PrinterUriRequirements? requirements = PrinterUriRequirementsReader.Read(attributes, new Uri("ipps://printer.local/ipp/print"));

		// Assert
		Assert.Null(requirements);
	}
}
