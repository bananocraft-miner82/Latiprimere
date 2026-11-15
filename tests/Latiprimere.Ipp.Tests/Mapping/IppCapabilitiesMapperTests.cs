using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests.Mapping;

/// <summary>
/// Maps printer attributes captured from the test container (tests/Latiprimere.Ipp.Tests/Data/*.ipp), decoded by
/// the real IPP client, so the tests cover what printers actually send.
/// </summary>
public sealed class IppCapabilitiesMapperTests
{
	[Fact]
	public async Task GivenIppEverywherePrinterWhenMappedThenReadsAllCoreCapabilities()
	{
		// Arrange
		PrinterDescriptionAttributes attributes = await IppCapabilitiesMapperTests.LoadAsync("ippeveprinter");

		// Act
		PrinterCapabilities capabilities = IppCapabilitiesMapper.ToCapabilities(attributes);

		// Assert
		string[] expectedSizes = new[]
		{
			"na_letter_8.5x11in",
			"na_legal_8.5x14in",
			"iso_a4_210x297mm",
			"na_number-10_4.125x9.5in",
			"iso_dl_110x220mm",
		};

		Assert.Equal(expectedSizes, capabilities.MediaSizes.Select(size => size.Name));
		Assert.Equal("na_letter_8.5x11in", capabilities.DefaultMediaSize?.Name);
		Assert.Equal(new[] { DuplexMode.OneSided }, capabilities.DuplexModes);
		Assert.Equal(DuplexMode.OneSided, capabilities.DefaultDuplexMode);
		Assert.Equal(new[] { ColorMode.Monochrome }, capabilities.ColorModes);
		Assert.Equal(ColorMode.Monochrome, capabilities.DefaultColorMode);
		Assert.Equal(999, capabilities.MaxCopies);
		Assert.Equal(new[] { PageOrientation.Portrait, PageOrientation.Landscape }, capabilities.Orientations);

		Assert.Equal
		(
			new[] { "auto", "main", "manual", "by-pass-tray" },
			capabilities.InputTrays.Select(tray => tray.Id)
		);

		Assert.Equal("By pass tray", capabilities.InputTrays[^1].Name);
	}

	[Fact]
	public async Task GivenCupsPdfQueueWhenMappedThenReadsColourAndManySizes()
	{
		// Arrange
		PrinterDescriptionAttributes attributes = await IppCapabilitiesMapperTests.LoadAsync("cups-cups-pdf");

		// Act
		PrinterCapabilities capabilities = IppCapabilitiesMapper.ToCapabilities(attributes);

		// Assert
		Assert.True(capabilities.MediaSizes.Count > 50);
		Assert.Contains(capabilities.MediaSizes, size => size.Name == "iso_a3_297x420mm" && size.DisplayName == "A3");
		Assert.Equal("iso_a4_210x297mm", capabilities.DefaultMediaSize?.Name);
		Assert.Equal(new[] { ColorMode.Monochrome, ColorMode.Color }, capabilities.ColorModes);
		Assert.Equal(ColorMode.Color, capabilities.DefaultColorMode);
		Assert.Equal(9999, capabilities.MaxCopies);
		Assert.Equal(new[] { DuplexMode.OneSided }, capabilities.DuplexModes);
	}

	[Fact]
	public async Task GivenCupsQueueInFrontOfIppPrinterWhenMappedThenMatchesThePrintersOwnCapabilities()
	{
		// Arrange — the Virtual-IPP queue's printer is the ippeveprinter instance
		PrinterDescriptionAttributes queue = await IppCapabilitiesMapperTests.LoadAsync("cups-virtual-ipp");
		PrinterDescriptionAttributes printer = await IppCapabilitiesMapperTests.LoadAsync("ippeveprinter");

		// Act
		PrinterCapabilities queueCapabilities = IppCapabilitiesMapper.ToCapabilities(queue);
		PrinterCapabilities printerCapabilities = IppCapabilitiesMapper.ToCapabilities(printer);

		// Assert — CUPS lists sizes in its own order, so compare them as sets
		Assert.Equal
		(
			printerCapabilities.MediaSizes.Select(size => size.Name).Order(),
			queueCapabilities.MediaSizes.Select(size => size.Name).Order()
		);

		Assert.Equal(printerCapabilities.DuplexModes, queueCapabilities.DuplexModes);
		Assert.Equal(printerCapabilities.ColorModes, queueCapabilities.ColorModes);
		Assert.Equal(printerCapabilities.Orientations, queueCapabilities.Orientations);

		Assert.Equal
		(
			printerCapabilities.InputTrays.Select(tray => tray.Id),
			queueCapabilities.InputTrays.Select(tray => tray.Id)
		);
	}

	[Fact]
	public async Task GivenRawQueueWhenMappedThenLeavesUnreportedCapabilitiesEmpty()
	{
		// Arrange — a raw queue has no driver, so CUPS reports very little
		PrinterDescriptionAttributes attributes = await IppCapabilitiesMapperTests.LoadAsync("cups-raw");

		// Act
		PrinterCapabilities capabilities = IppCapabilitiesMapper.ToCapabilities(attributes);

		// Assert — colour falls back to the default, the only colour attribute the queue reports
		Assert.Empty(capabilities.MediaSizes);
		Assert.Null(capabilities.DefaultMediaSize);
		Assert.Empty(capabilities.DuplexModes);
		Assert.Equal(new[] { ColorMode.Monochrome }, capabilities.ColorModes);
		Assert.Equal(9999, capabilities.MaxCopies);
	}

	[Fact]
	public async Task GivenHpEnvyNetworkPrinterWhenMappedThenReadsItsOwnCapabilities()
	{
		// Arrange — captured from a real HP ENVY 5640 over Wi-Fi, with identifiers replaced (see Data/README.md)
		PrinterDescriptionAttributes attributes = await IppCapabilitiesMapperTests.LoadAsync("hp-envy-5640");

		// Act
		PrinterCapabilities capabilities = IppCapabilitiesMapper.ToCapabilities(attributes);

		// Assert — the printer itself only prints portrait and reports no default colour mode
		Assert.Equal(26, capabilities.MediaSizes.Count);
		Assert.Contains(capabilities.MediaSizes, size => size.Name == "iso_a4_210x297mm");
		Assert.Equal("na_letter_8.5x11in", capabilities.DefaultMediaSize?.Name);

		Assert.Equal
		(
			new[] { DuplexMode.OneSided, DuplexMode.ShortEdge, DuplexMode.LongEdge },
			capabilities.DuplexModes
		);

		Assert.Equal(DuplexMode.OneSided, capabilities.DefaultDuplexMode);
		Assert.Equal(new[] { ColorMode.Monochrome, ColorMode.Color }, capabilities.ColorModes);
		Assert.Null(capabilities.DefaultColorMode);
		Assert.Equal(99, capabilities.MaxCopies);
		Assert.Equal(new[] { PageOrientation.Portrait }, capabilities.Orientations);
		Assert.Equal(new[] { "main", "photo", "auto" }, capabilities.InputTrays.Select(tray => tray.Id));
	}

	[Fact]
	public async Task GivenHpEnvyNetworkPrinterWhenDecodedThenDoesNotListPdf()
	{
		// Arrange — the printer takes PCL, JPEG, PCLm, Apple raster and octet-stream, but not PDF
		PrinterDescriptionAttributes attributes = await IppCapabilitiesMapperTests.LoadAsync("hp-envy-5640");

		// Act
		string[] formats = IppValueReader.ReadReference(attributes.DocumentFormatSupported)
			?.Select(format => format.Value)
			.ToArray() ?? [];

		// Assert
		Assert.Contains("image/urf", formats);
		Assert.Contains("application/PCLm", formats);
		Assert.DoesNotContain("application/pdf", formats);
	}

	[Fact]
	public void GivenOnlyColorSupportedWhenMappedThenOffersMonochromeAndColour()
	{
		// Arrange — an older printer without print-color-mode-supported
		PrinterDescriptionAttributes attributes = new PrinterDescriptionAttributes
		{
			ColorSupported = new IppValue<bool>(true)
		};

		// Act
		PrinterCapabilities capabilities = IppCapabilitiesMapper.ToCapabilities(attributes);

		// Assert
		Assert.Equal(new[] { ColorMode.Monochrome, ColorMode.Color }, capabilities.ColorModes);
		Assert.Null(capabilities.DefaultColorMode);
		Assert.Null(capabilities.MaxCopies);
	}

	[Fact]
	public void GivenModesWithoutEquivalentsWhenMappedThenLeavesThemOut()
	{
		// Arrange — reverse orientations, auto colour and an unknown sides keyword
		PrinterDescriptionAttributes attributes = new PrinterDescriptionAttributes
		{
			OrientationRequestedSupported = new IppValue<Orientation[]>
			(
				new[] { Orientation.ReversePortrait, Orientation.Landscape, Orientation.ReverseLandscape }
			),
			PrintColorModeSupported = new IppValue<PrintColorMode[]>
			(
				new[] { new PrintColorMode("auto"), new PrintColorMode("process-monochrome") }
			),
			SidesSupported = new IppValue<Sides[]>(new[] { new Sides("two-sided-long-edge"), new Sides("tumble") })
		};

		// Act
		PrinterCapabilities capabilities = IppCapabilitiesMapper.ToCapabilities(attributes);

		// Assert
		Assert.Equal(new[] { PageOrientation.Landscape }, capabilities.Orientations);
		Assert.Equal(new[] { ColorMode.Monochrome }, capabilities.ColorModes);
		Assert.Equal(new[] { DuplexMode.LongEdge }, capabilities.DuplexModes);
	}

	/// <summary>
	/// Decodes a captured Get-Printer-Attributes response through the real IPP client.
	/// </summary>
	/// <param name="name">The capture's name, without <c>-printer-attributes.ipp</c>.</param>
	/// <returns>The decoded attributes.</returns>
	private static async Task<PrinterDescriptionAttributes> LoadAsync(string name)
	{
		string path = Path.Combine(AppContext.BaseDirectory, "Data", name + "-printer-attributes.ipp");
		byte[] response = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
		using HttpClient httpClient = new HttpClient(new ReplayHttpMessageHandler(response));

		IppPrintContext context = new IppPrintContextBuilder()
			.WithClient(new SharpIppClientAdapter(httpClient))
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.Build();

		PrinterDescriptionAttributes? attributes = await IppPrinterQueries.GetAttributesAsync
		(
			context,
			new Uri("ipp://localhost:631/printers/Replay"),
			new[] { "all" },
			TestContext.Current.CancellationToken
		);

		return attributes ?? throw new InvalidOperationException("The capture holds no printer attributes.");
	}
}
