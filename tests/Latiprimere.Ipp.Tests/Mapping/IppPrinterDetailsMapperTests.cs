using Latiprimere.Ipp.Mapping;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

/// <summary>
/// A printer's details read from its raw IPP attributes (ADR 0059).
/// </summary>
public sealed class IppPrinterDetailsMapperTests
{
	[Fact]
	public void GivenAnIppEverywherePrintersAttributesWhenMappedThenEachDetailIsRead()
	{
		// Arrange — a second value of a set may come without its name
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Uri, "printer-more-info", "http://192.168.1.20/"),
			new IppAttribute(Tag.Uri, "printer-supply-info-uri", "https://192.168.1.20/supplies"),
			new IppAttribute(Tag.Uri, "printer-icons", "https://192.168.1.20/icon-48.png"),
			new IppAttribute(Tag.Uri, string.Empty, "https://192.168.1.20/icon-128.png"),
			new IppAttribute(Tag.Uri, string.Empty, "https://192.168.1.20/icon-512.png"),
			new IppAttribute(Tag.NameWithoutLanguage, "printer-firmware-name", "Firmware"),
			new IppAttribute(Tag.NameWithoutLanguage, string.Empty, "Network"),
			new IppAttribute(Tag.TextWithoutLanguage, "printer-firmware-string-version", "2.4.1"),
			new IppAttribute(Tag.TextWithoutLanguage, string.Empty, "7.0"),
			new IppAttribute(Tag.Keyword, "identify-actions-supported", "flash"),
			new IppAttribute(Tag.Keyword, string.Empty, "display"),
		];

		// Act
		PrinterDetails details = IppPrinterDetailsMapper.ToDetails(attributes, "ipp:office");

		// Assert
		Assert.Equal("ipp:office", details.PrinterId);
		Assert.Equal(new Uri("http://192.168.1.20/"), details.WebPage);
		Assert.Equal(new Uri("https://192.168.1.20/supplies"), details.SuppliesWebPage);
		Assert.Equal(["https://192.168.1.20/icon-48.png", "https://192.168.1.20/icon-128.png", "https://192.168.1.20/icon-512.png"], details.Icons.Select(icon => icon.ToString()));
		Assert.Equal([("Firmware", "2.4.1"), ("Network", "7.0")], details.Firmware.Select(part => (part.Name, part.Version)));
		Assert.Equal([IdentifyAction.Flash, IdentifyAction.Display], details.IdentifyActions);
	}

	[Theory]
	[InlineData("file:///etc/passwd")]
	[InlineData("ftp://192.168.1.20/icon.png")]
	[InlineData("icons/printer.png")]
	[InlineData("not a uri")]
	public void GivenAWebPageThatIsNotHttpWhenMappedThenItIsLeftOut(string value)
	{
		// Arrange (provided by InlineData above) — the printer's say-so, which an application may fetch
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Uri, "printer-more-info", value),
			new IppAttribute(Tag.Uri, "printer-icons", value),
		];

		// Act
		PrinterDetails details = IppPrinterDetailsMapper.ToDetails(attributes, "ipp:office");

		// Assert
		Assert.Null(details.WebPage);
		Assert.Empty(details.Icons);
	}

	[Fact]
	public void GivenFirmwareNamesAndVersionsThatDoNotPairWhenMappedThenOnlyWholePairsAreGiven()
	{
		// Arrange — a blank version, and a third name without one
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.NameWithoutLanguage, "printer-firmware-name", "Firmware"),
			new IppAttribute(Tag.NameWithoutLanguage, string.Empty, "Engine"),
			new IppAttribute(Tag.NameWithoutLanguage, string.Empty, "Network"),
			new IppAttribute(Tag.TextWithoutLanguage, "printer-firmware-string-version", " 2.4.1 "),
			new IppAttribute(Tag.TextWithoutLanguage, string.Empty, " "),
		];

		// Act
		PrinterDetails details = IppPrinterDetailsMapper.ToDetails(attributes, "ipp:office");

		// Assert
		PrinterFirmware part = Assert.Single(details.Firmware);
		Assert.Equal(("Firmware", "2.4.1"), (part.Name, part.Version));
	}

	[Fact]
	public void GivenIdentifyActionsWithRepeatsAndOnesUnknownWhenMappedThenEachKnownOneIsGivenOnce()
	{
		// Arrange
		List<IppAttribute> attributes =
		[
			new IppAttribute(Tag.Keyword, "identify-actions-supported", "sound"),
			new IppAttribute(Tag.Keyword, string.Empty, "dance"),
			new IppAttribute(Tag.Keyword, string.Empty, "speak"),
			new IppAttribute(Tag.Keyword, string.Empty, "sound"),
		];

		// Act
		PrinterDetails details = IppPrinterDetailsMapper.ToDetails(attributes, "ipp:office");

		// Assert
		Assert.Equal([IdentifyAction.Sound, IdentifyAction.Speak], details.IdentifyActions);
	}

	[Fact]
	public void GivenNoDetailAttributesWhenMappedThenNothingIsReported()
	{
		// Arrange
		List<IppAttribute> attributes = [new IppAttribute(Tag.Keyword, "sides-supported", "one-sided")];

		// Act
		PrinterDetails details = IppPrinterDetailsMapper.ToDetails(attributes, "ipp:office");

		// Assert
		Assert.Null(details.WebPage);
		Assert.Null(details.SuppliesWebPage);
		Assert.Empty(details.Icons);
		Assert.Empty(details.Firmware);
		Assert.Empty(details.IdentifyActions);
	}
}
