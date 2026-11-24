using Latiprimere.Ipp.Discovery;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using SharpIpp.Models.Requests;
using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppNetworkPrintSystemTests
{
	/// <summary>When the clock starts in tests that move it.</summary>
	private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);

	/// <summary>A printer URI used across tests.</summary>
	private static readonly Uri OfficeUri = new Uri("ipp://192.168.1.20:631/ipp/print");

	/// <summary>Where the Office printer is after DHCP gives it a new address.</summary>
	private static readonly Uri MovedOfficeUri = new Uri("ipp://192.168.1.45:631/ipp/print");

	/// <summary>The Office printer's IPPS address.</summary>
	private static readonly Uri SecureOfficeUri = new Uri("ipps://192.168.1.20:443/ipp/print");

	/// <summary>The Office printer's UUID.</summary>
	private const string OfficeUuid = "aaaaaaaa-0000-4000-8000-0000000000aa";

	/// <summary>The UUID of a different printer.</summary>
	private const string OtherUuid = "bbbbbbbb-0000-4000-8000-0000000000bb";

	[Fact]
	public async Task GivenDiscoveredPrinterWhenGetPrintersThenIdentifiesItByNameWithoutContactingIt()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office Laser", IppNetworkPrintSystemTests.OfficeUri);
		IppNetworkOptions options = new IppNetworkOptions { DiscoveryDuration = TimeSpan.FromSeconds(3) };

		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem
		(
			client,
			discovery,
			options
		);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrinterInfo printer = Assert.Single(printers);
		Assert.Equal("ipp:dnssd:Office Laser", printer.Id);
		Assert.Equal("Office Laser", printer.Name);
		Assert.Equal("Test lab", printer.Location);
		Assert.Equal("Test Printer", printer.MakeAndModel);
		Assert.Equal(PrinterState.Unknown, printer.State);
		Assert.Equal(TimeSpan.FromSeconds(3), discovery.LastDuration);
		Assert.Empty(client.GetPrinterAttributesRequests);
	}

	[Fact]
	public async Task GivenConfiguredPrintersWhenGetPrintersThenIdentifiesThemByUriAndUsesAnyAnnouncement()
	{
		// Arrange — one configured printer also announces itself, the other doesn't
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office Laser", IppNetworkPrintSystemTests.OfficeUri);
		IppNetworkOptions options = new IppNetworkOptions();
		options.PrinterUris.Add(IppNetworkPrintSystemTests.OfficeUri);
		options.PrinterUris.Add(new Uri("ipp://192.168.1.30/ipp/print"));

		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem
		(
			new FakeIppClient(),
			discovery,
			options
		);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert — listed once each, under the identifier the application configured
		Assert.Equal(2, printers.Count);
		Assert.Equal("ipp:ipp://192.168.1.20:631/ipp/print", printers[0].Id);
		Assert.Equal("Office Laser", printers[0].Name);
		Assert.Equal("Test lab", printers[0].Location);
		Assert.Equal("ipp:ipp://192.168.1.30/ipp/print", printers[1].Id);
		Assert.Equal("192.168.1.30", printers[1].Name);
		Assert.Null(printers[1].Location);
	}

	[Theory]
	[InlineData("ipp://192.168.1.20/ipp/print")]
	[InlineData("ipps://192.168.1.20/ipp/print")]
	public async Task GivenConfiguredUriWithoutThePortTheAnnouncementGivesWhenGetPrintersThenListsItOnce(string configuredUri)
	{
		// Arrange — announced URIs always name the port; the IPPS one is 631 too, so it matches without one
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office Laser", IppNetworkPrintSystemTests.OfficeUri, secureUri: new Uri("ipps://192.168.1.20:631/ipp/print"));
		IppNetworkOptions options = new IppNetworkOptions();
		options.PrinterUris.Add(new Uri(configuredUri));

		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem
		(
			new FakeIppClient(),
			discovery,
			options
		);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert — under the configured identifier, described from the announcement
		PrinterInfo printer = Assert.Single(printers);
		Assert.Equal("ipp:" + configuredUri, printer.Id);
		Assert.Equal("Office Laser", printer.Name);
	}

	[Fact]
	public async Task GivenDiscoveryDisabledWhenGetPrintersThenListsOnlyConfiguredPrinters()
	{
		// Arrange
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri);
		IppNetworkOptions options = new IppNetworkOptions { EnableDiscovery = false };
		options.PrinterUris.Add(new Uri("ipp://192.168.1.30/ipp/print"));

		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem
		(
			new FakeIppClient(),
			discovery,
			options
		);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrinterInfo printer = Assert.Single(printers);
		Assert.Equal("192.168.1.30", printer.Name);
		Assert.Equal(0, discovery.DiscoveryCount);
	}

	[Theory]
	[InlineData("windows:Office")]
	[InlineData("ipp:not a uri")]
	[InlineData("ipp:http://192.168.1.20/ipp/print")]
	[InlineData("ipp:")]
	[InlineData("ipp:dnssd:")]
	public async Task GivenIdNotFromThisPrintSystemWhenFindPrinterThenReturnsNullWithoutContactingAnything
	(
		string printerId
	)
	{
		// Arrange — identifiers from other print systems, with non-IPP URIs, or with no name (provided by InlineData)
		FakeIppClient client = new FakeIppClient();
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
		Assert.Empty(client.GetPrinterAttributesRequests);
		Assert.Empty(discovery.ResolvedNames);
	}

	[Theory]
	[InlineData("ipp:dnssd:Office Laser", null, "Office Laser")]
	[InlineData("ipp:dnssd:Floor 2: Laser", null, "Floor 2: Laser")]
	[InlineData("ipp:ipps://printer.local/ipp/print", "ipps://printer.local/ipp/print", null)]
	public void GivenIdFromThisPrintSystemWhenParsedThenGivesUriOrName
	(
		string  printerId,
		string? expectedUri,
		string? expectedName
	)
	{
		// Arrange — name and URI identifiers, and the parts they hold (provided by InlineData above)

		// Act
		bool isOurs = IppNetworkPrintSystem.TryParsePrinterId(printerId, out Uri? printerUri, out string? instanceName);

		// Assert
		Assert.True(isOurs);
		Assert.Equal(expectedUri, printerUri?.ToString());
		Assert.Equal(expectedName, instanceName);
	}

	[Fact]
	public async Task GivenNameIdWhenFindPrinterThenResolvesNameAndReadsPrinterAtCurrentAddress()
	{
		// Arrange — the name is looked up in a different case from its announcement
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter
		(
			IppNetworkPrintSystemTests.OfficeUri,
			new PrinterAttributesBuilder().WithName("HP-1").Build()
		);

		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office Laser", IppNetworkPrintSystemTests.OfficeUri);
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"ipp:dnssd:office laser",
			TestContext.Current.CancellationToken
		);

		// Assert — keeps the identifier and name it was asked for, not the printer's own printer-name
		Assert.NotNull(printer);
		Assert.Equal("ipp:dnssd:office laser", printer.Info.Id);
		Assert.Equal("office laser", printer.Info.Name);
		Assert.Equal(new[] { "office laser" }, discovery.ResolvedNames);
		GetPrinterAttributesRequest request = Assert.Single(client.GetPrinterAttributesRequests);
		Assert.Equal(IppNetworkPrintSystemTests.OfficeUri, request.OperationAttributes?.PrinterUri?.Value);
	}

	[Fact]
	public async Task GivenListedPrinterWhenFindPrinterByNameThenUsesRememberedAddressWithoutSearching()
	{
		// Arrange — the UUID is announced and reported in different forms, which still match
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter
		(
			IppNetworkPrintSystemTests.OfficeUri,
			new PrinterAttributesBuilder().WithUuid("urn:uuid:6D6F7A00-0000-4000-8000-000000000001").Build()
		);

		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri, "6d6f7a00-0000-4000-8000-000000000001");
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery);
		await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Act
		IPrinter? printer = await IppNetworkPrintSystemTests.FindOfficeAsync(printSystem);

		// Assert
		Assert.NotNull(printer);
		Assert.Empty(discovery.ResolvedNames);
		GetPrinterAttributesRequest request = Assert.Single(client.GetPrinterAttributesRequests);
		Assert.Equal(IppNetworkPrintSystemTests.OfficeUri, request.OperationAttributes?.PrinterUri?.Value);
	}

	[Fact]
	public async Task GivenPrinterMovedWhenFindPrinterByNameThenSearchesAndRemembersNewAddress()
	{
		// Arrange — listed at one address, then announced at another; nothing answers at the old one
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppNetworkPrintSystemTests.MovedOfficeUri, new PrinterAttributesBuilder().Build());
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri);
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery);
		await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);
		discovery.Clear();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.MovedOfficeUri);

		// Act — twice: the second lookup should go straight to the new address
		IPrinter? first = await IppNetworkPrintSystemTests.FindOfficeAsync(printSystem);
		IPrinter? second = await IppNetworkPrintSystemTests.FindOfficeAsync(printSystem);

		// Assert
		Assert.NotNull(first);
		Assert.NotNull(second);
		Assert.Equal(new[] { "Office" }, discovery.ResolvedNames);

		Uri?[] requestedUris = client.GetPrinterAttributesRequests
			.Select(request => request.OperationAttributes?.PrinterUri?.Value)
			.ToArray();

		Uri[] expectedUris = new[]
		{
			IppNetworkPrintSystemTests.OfficeUri,
			IppNetworkPrintSystemTests.MovedOfficeUri,
			IppNetworkPrintSystemTests.MovedOfficeUri,
		};
		Assert.Equal(expectedUris, requestedUris);
	}

	[Fact]
	public async Task GivenDifferentPrinterAtRememberedAddressWhenFindPrinterByNameThenSearches()
	{
		// Arrange — another printer took the old address; its printer-uuid doesn't match the announcement
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter
		(
			IppNetworkPrintSystemTests.OfficeUri,
			new PrinterAttributesBuilder()
				.WithName("Someone else")
				.WithUuid("urn:uuid:" + IppNetworkPrintSystemTests.OtherUuid)
				.Build()
		);

		client.AddPrinter
		(
			IppNetworkPrintSystemTests.MovedOfficeUri,
			new PrinterAttributesBuilder().WithUuid("urn:uuid:" + IppNetworkPrintSystemTests.OfficeUuid).Build()
		);

		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri, IppNetworkPrintSystemTests.OfficeUuid);
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery);
		await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);
		discovery.Clear();

		discovery.AddPrinter
		(
			"Office",
			IppNetworkPrintSystemTests.MovedOfficeUri,
			IppNetworkPrintSystemTests.OfficeUuid
		);

		// Act
		IPrinter? printer = await IppNetworkPrintSystemTests.FindOfficeAsync(printSystem);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal(new[] { "Office" }, discovery.ResolvedNames);

		Assert.Equal
		(
			IppNetworkPrintSystemTests.MovedOfficeUri,
			client.GetPrinterAttributesRequests[^1].OperationAttributes?.PrinterUri?.Value
		);
	}

	[Theory]
	[InlineData("unreachable")]
	[InlineData("unresponsive")]
	public async Task GivenRememberedAddressNotAnsweringWhenFindPrinterByNameThenSearches(string failure)
	{
		// Arrange — the old address refuses connections or never answers (provided by InlineData above)
		FakeIppClient client = new FakeIppClient();

		if (failure == "unreachable")
		{
			client.AddUnreachable(IppNetworkPrintSystemTests.OfficeUri);
		}
		else
		{
			client.AddUnresponsive(IppNetworkPrintSystemTests.OfficeUri);
		}

		client.AddPrinter(IppNetworkPrintSystemTests.MovedOfficeUri, new PrinterAttributesBuilder().Build());
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri);

		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem
		(
			client,
			discovery,
			knownAddressTimeout: TimeSpan.FromMilliseconds(50)
		);

		await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);
		discovery.Clear();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.MovedOfficeUri);

		// Act
		IPrinter? printer = await IppNetworkPrintSystemTests.FindOfficeAsync(printSystem);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal(new[] { "Office" }, discovery.ResolvedNames);
	}

	[Fact]
	public async Task GivenRememberedPrinterSwitchedOffWhenFindPrinterByNameThenReturnsNull()
	{
		// Arrange — nothing at the old address, and no announcement any more
		FakeIppClient client = new FakeIppClient();
		client.AddUnreachable(IppNetworkPrintSystemTests.OfficeUri);
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri);
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery);
		await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);
		discovery.Clear();

		// Act
		IPrinter? printer = await IppNetworkPrintSystemTests.FindOfficeAsync(printSystem);

		// Assert
		Assert.Null(printer);
		Assert.Equal(new[] { "Office" }, discovery.ResolvedNames);
	}

	[Fact]
	public async Task GivenNameThatDoesNotAnswerWhenFindPrinterThenReturnsNullWithoutContactingAnyPrinter()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"ipp:dnssd:Office Laser",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.Null(printer);
		Assert.Empty(client.GetPrinterAttributesRequests);
	}

	[Fact]
	public async Task GivenReachablePrinterWhenFindPrinterThenReturnsItWithCurrentDetails()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter
		(
			IppNetworkPrintSystemTests.OfficeUri,
			new PrinterAttributesBuilder().WithName("Office").WithDocumentFormats("application/pdf").Build()
		);

		IppNetworkOptions options = new IppNetworkOptions { PrinterUris = { IppNetworkPrintSystemTests.OfficeUri } };
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, options: options);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"ipp:ipp://192.168.1.20:631/ipp/print",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal("ipp:ipp://192.168.1.20:631/ipp/print", printer.Info.Id);
		Assert.Equal("Office", printer.Info.Name);
		Assert.Equal(PrinterState.Idle, printer.Info.State);
		Assert.False(printer.Info.IsDefault);
		GetPrinterAttributesRequest request = Assert.Single(client.GetPrinterAttributesRequests);

		Assert.Equal
		(
			IppRequestedAttributes.NetworkPrinterAttributes,
			request.OperationAttributes?.RequestedAttributes?.Value
		);
	}

	[Fact]
	public async Task GivenPrinterAdvertisingFormatsWhenSubmittingUnsupportedFormatThenRejectsWithoutSending()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter
		(
			IppNetworkPrintSystemTests.OfficeUri,
			new PrinterAttributesBuilder().WithDocumentFormats("application/pdf", "image/urf").Build()
		);

		IppNetworkOptions options = new IppNetworkOptions { PrinterUris = { IppNetworkPrintSystemTests.OfficeUri } };
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, options: options);

		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"ipp:ipp://192.168.1.20:631/ipp/print",
			TestContext.Current.CancellationToken
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x1B, 0x40 });

		// Act
		Func<Task> submit = () => printer!.SubmitAsync
		( // Not null: the printer was added above
			PrintContent.FromRaw(document),
			PrintOptions.Default,
			TestContext.Current.CancellationToken
		);

		// Assert
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("application/pdf, image/urf", exception.Message, StringComparison.Ordinal);
		Assert.Null(client.LastPrintJobRequest);
	}

	[Fact]
	public async Task GivenUnlistedUriWhenFindPrinterThenReturnsNullWithoutContactingIt()
	{
		// Arrange — a printer answers there, but the URI is neither configured nor announced
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppNetworkPrintSystemTests.OfficeUri, new PrinterAttributesBuilder().WithName("Office").Build());
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"ipp:ipp://192.168.1.20:631/ipp/print",
			TestContext.Current.CancellationToken
		);

		// Assert — no request at all, so an identifier from outside can't probe hosts and ports
		Assert.Null(printer);
		Assert.Empty(client.GetPrinterAttributesRequests);
	}

	[Theory]
	[InlineData("ipp:ipp://192.168.1.20:8631/ipp/print")]
	[InlineData("ipp:ipp://192.168.1.20:631/ipp/other")]
	[InlineData("ipp:ipp://192.168.1.20:631/IPP/PRINT")]
	[InlineData("ipp:ipps://192.168.1.20:631/ipp/print")]
	[InlineData("ipp:ipp://192.168.1.20:631/ipp/print?x=1")]
	[InlineData("ipp:ipp://192.168.1.21:631/ipp/print")]
	public async Task GivenUriCloseToConfiguredOneWhenFindPrinterThenReturnsNullWithoutContactingIt(string printerId)
	{
		// Arrange — printerId differs from the configured URI only in its port, path, path case, scheme, query or
		// host (provided by InlineData above): each could reach a different service
		FakeIppClient client = new FakeIppClient();
		IppNetworkOptions options = new IppNetworkOptions { PrinterUris = { new Uri("ipp://192.168.1.20/ipp/print") } };
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, options: options);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
		Assert.Empty(client.GetPrinterAttributesRequests);
	}

	[Theory]
	[InlineData("ipp://office.local/ipp/print", "ipp:ipp://OFFICE.LOCAL/ipp/print")]
	[InlineData("ipp://office.local/ipp/print", "ipp:ipp://office.local:631/ipp/print")]
	[InlineData("ipp://office.local/ipp/print", "ipp:ipp://guest@office.local/ipp/print")]
	[InlineData("ipp://[fe80::1]/ipp/print", "ipp:ipp://[FE80::1]:631/ipp/print")]
	public async Task GivenUriNamingConfiguredPrinterDifferentlyWhenFindPrinterThenContactsIt(string configuredUri, string printerId)
	{
		// Arrange — printerId names the configured printer with the host in other case, the default port, a user
		// name (which Uri equality ignores, and which reaches nothing new) or an IPv6 address written differently
		// (provided by InlineData above)
		FakeIppClient client = new FakeIppClient();
		IppNetworkOptions options = new IppNetworkOptions { PrinterUris = { new Uri(configuredUri) } };
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, options: options);

		// Act
		await printSystem.FindPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert — asked once; the fake has no printer there, so the lookup itself finds nothing
		Assert.Single(client.GetPrinterAttributesRequests);
	}

	[Theory]
	[InlineData("ipp://192.168.1.20/ipp/print")]
	[InlineData("ipp://192.168.1.20:631/ipp/print")]
	public async Task GivenConfiguredUriWithOrWithoutDefaultPortWhenFindPrinterThenFindsIt(string configuredUri)
	{
		// Arrange — configuredUri names the printer with and without IPP's default port (provided by InlineData)
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppNetworkPrintSystemTests.OfficeUri, new PrinterAttributesBuilder().WithName("Office").Build());
		IppNetworkOptions options = new IppNetworkOptions { PrinterUris = { new Uri(configuredUri) } };
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, options: options);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"ipp:ipp://192.168.1.20:631/ipp/print",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.NotNull(printer);
	}

	[Fact]
	public async Task GivenPrinterListingWhatEachAddressAsksForWhenFindPrinterThenKeepsItForItsAddress()
	{
		// Arrange — the printer names itself by its .local name, and asks for a password on plain IPP
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter
		(
			IppNetworkPrintSystemTests.OfficeUri,
			new PrinterAttributesBuilder()
				.WithName("Office")
				.WithUriAuthentication(("ipp://office.local:631/ipp/print", "basic"), ("ipps://office.local:443/ipp/print", "basic"))
				.Build()
		);

		IppNetworkOptions options = new IppNetworkOptions { PrinterUris = { IppNetworkPrintSystemTests.OfficeUri } };
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, options: options);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync("ipp:" + IppNetworkPrintSystemTests.OfficeUri, TestContext.Current.CancellationToken);

		// Assert — the requested attributes include what each address asks for
		IppPrinter ippPrinter = Assert.IsType<IppPrinter>(printer);
		Assert.True(ippPrinter.UriRequirements?.AsksForPassword);
		Assert.Equal(new Uri("ipps://192.168.1.20:443/ipp/print"), ippPrinter.UriRequirements?.SecureAlternative);
		Assert.Contains(IppRequestedAttributes.UriAuthenticationSupported, client.GetPrinterAttributesRequests[0].OperationAttributes?.RequestedAttributes?.Value ?? []);
	}

	[Fact]
	public async Task GivenAnnouncedPrinterWhenFindPrinterByItsUriThenFindsIt()
	{
		// Arrange — listing found the printer on the network, so its URI is known
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppNetworkPrintSystemTests.OfficeUri, new PrinterAttributesBuilder().WithName("Office").Build());
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri);
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery);
		await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"ipp:ipp://192.168.1.20:631/ipp/print",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.NotNull(printer);
	}

	[Fact]
	public async Task GivenPrinterThatDoesNotExistWhenFindPrinterThenReturnsNull()
	{
		// Arrange — any URI allowed, so the lookup reaches the address and finds nothing there
		IppNetworkOptions options = new IppNetworkOptions { AllowAnyPrinterUri = true };
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(new FakeIppClient(), options: options);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"ipp:ipp://192.168.1.20:631/ipp/missing",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public async Task GivenAnyStateWhenFindDefaultPrinterThenReturnsNull()
	{
		// Arrange
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(new FakeIppClient());

		// Act
		IPrinter? printer = await printSystem.FindDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
		Assert.True(printSystem.IsSupported);
		Assert.Equal("IPP", printSystem.Name);
	}

	[Fact]
	public void GivenOwnedClientWhenDisposedThenDisposesClient()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client);

		// Act
		printSystem.Dispose();

		// Assert
		Assert.True(client.IsDisposed);
	}

	[Fact]
	public async Task GivenOptionsChangedAfterCreationWhenGetPrintersThenUsesTheOriginalOptions()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions { EnableDiscovery = false };

		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem
		(
			new FakeIppClient(),
			new FakeIppPrinterDiscovery(),
			options
		);

		// Act
		options.PrinterUris.Add(IppNetworkPrintSystemTests.OfficeUri);
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Empty(printers);
	}

	/// <summary>
	/// Looks up the Office printer by name.
	/// </summary>
	/// <param name="printSystem">The print system.</param>
	/// <returns>The printer, or <see langword="null"/> if not found.</returns>
	private static Task<IPrinter?> FindOfficeAsync(IppNetworkPrintSystem printSystem)
	{
		return printSystem.FindPrinterAsync("ipp:dnssd:Office", TestContext.Current.CancellationToken);
	}

	[Theory]
	[InlineData(true, "none", "ipps://192.168.1.20:443/ipp/print")]
	[InlineData(true, "untrusted", "ipp://192.168.1.20:631/ipp/print")]
	[InlineData(true, "unreachable", "ipp://192.168.1.20:631/ipp/print")]
	[InlineData(false, "none", "ipp://192.168.1.20:631/ipp/print")]
	public async Task GivenPrinterAnnouncingIppsWhenLookedUpByNameThenIppsIsUsedOnlyWhenPreferredAndItOpens(bool prefersIpps, string failure, string expected)
	{
		// Arrange — trust configured or not; the IPPS address fine, its certificate untrusted, or nothing listening
		// there (provided by InlineData above)
		Uri plain = new Uri("ipp://192.168.1.20:631/ipp/print");
		Uri secure = new Uri("ipps://192.168.1.20:443/ipp/print");
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(plain, new PrinterAttributesBuilder().WithName("Office").Build());
		client.AddPrinter(secure, new PrinterAttributesBuilder().WithName("Office").Build());

		if (failure == "untrusted")
		{
			client.AddUntrusted(secure);
		}
		else if (failure == "unreachable")
		{
			client.AddUnreachable(secure);
		}

		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", plain, secureUri: secure);
		using IppNetworkPrintSystem system = new IppNetworkPrintSystem
		(
			new IppPrintContextBuilder().WithClient(client).WithDocumentFormats(IppDocumentFormats.Standard).Build(),
			discovery,
			new IppNetworkOptions(),
			client
		)
		{
			PrefersIpps = prefersIpps
		};

		// Act
		IPrinter? printer = await system.FindPrinterAsync("ipp:dnssd:Office", TestContext.Current.CancellationToken);

		// Assert — the address used is the one the next request goes to
		Assert.NotNull(printer);
		Assert.Equal(new Uri(expected), client.GetPrinterAttributesRequests[^1].OperationAttributes?.PrinterUri?.Value);
	}

	[Theory]
	[InlineData("untrusted")]
	[InlineData("unreachable")]
	public async Task GivenPrinterReachedOverIppsBeforeWhenItsIppsAddressFailsThenItIsRefusedRatherThanReachedOverPlainIpp(string failure)
	{
		// Arrange — reached over IPPS once; then its certificate isn't trusted, or its IPPS port doesn't answer, as
		// when another device answers in its place or blocks the port (provided by InlineData above)
		FakeIppClient client = IppNetworkPrintSystemTests.CreateClientWithBothAddresses();
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateOfficeDiscovery(withIpps: true);
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, discovery, new IppNetworkOptions());
		Assert.NotNull(await IppNetworkPrintSystemTests.FindOfficeAsync(system));

		if (failure == "untrusted")
		{
			client.AddUntrusted(IppNetworkPrintSystemTests.SecureOfficeUri);
		}
		else
		{
			client.AddUnreachable(IppNetworkPrintSystemTests.SecureOfficeUri);
		}

		// Act
		Exception? exception = await Record.ExceptionAsync(() => IppNetworkPrintSystemTests.FindOfficeAsync(system));

		// Assert — refused, and nothing was asked of its plain address
		Assert.IsAssignableFrom<PrintException>(exception);
		Assert.False(IppNetworkPrintSystemTests.WasAskedOverPlainIpp(client));
	}

	[Fact]
	public async Task GivenPrinterReachedOverIppsBeforeWhenItsNameIsAnnouncedWithOnlyAPlainAddressThenItIsRefused()
	{
		// Arrange — reached over IPPS; then its IPPS address is gone, and the name is announced over plain IPP only
		FakeIppClient client = IppNetworkPrintSystemTests.CreateClientWithBothAddresses();
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateOfficeDiscovery(withIpps: true);
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, discovery, new IppNetworkOptions());
		Assert.NotNull(await IppNetworkPrintSystemTests.FindOfficeAsync(system));

		client.AddUnreachable(IppNetworkPrintSystemTests.SecureOfficeUri);
		discovery.Clear();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri);

		// Act
		Exception? exception = await Record.ExceptionAsync(() => IppNetworkPrintSystemTests.FindOfficeAsync(system));

		// Assert
		Assert.IsType<PrintException>(exception);
		Assert.Contains("was reached over an encrypted (IPPS) connection before", exception.Message, StringComparison.Ordinal);
		Assert.False(IppNetworkPrintSystemTests.WasAskedOverPlainIpp(client));
	}

	[Fact]
	public async Task GivenNothingBeyondTheSystemsTrustWhenPrinterAnnouncingIppsIsLookedUpAgainThenPlainIppIsUsedAsBefore()
	{
		// Arrange — IPPS isn't preferred, so plain IPP is all the printer is ever reached over
		FakeIppClient client = IppNetworkPrintSystemTests.CreateClientWithBothAddresses();
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateOfficeDiscovery(withIpps: true);
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, discovery, new IppNetworkOptions(), prefersIpps: false);
		Assert.NotNull(await IppNetworkPrintSystemTests.FindOfficeAsync(system));

		// Act
		IPrinter? printer = await IppNetworkPrintSystemTests.FindOfficeAsync(system);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal(IppNetworkPrintSystemTests.OfficeUri, client.GetPrinterAttributesRequests[^1].OperationAttributes?.PrinterUri?.Value);
	}

	[Theory]
	[InlineData("none", true)]
	[InlineData("untrusted", false)]
	[InlineData("unreachable", false)]
	[InlineData("plain only", false)]
	public async Task GivenEncryptionRequiredWhenPrinterFoundOnTheNetworkIsLookedUpThenItIsReachedOnlyOverIpps(string failure, bool expected)
	{
		// Arrange — its IPPS address fine, its certificate untrusted, nothing listening there, or no IPPS address
		// announced at all (provided by InlineData above)
		FakeIppClient client = IppNetworkPrintSystemTests.CreateClientWithBothAddresses();
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateOfficeDiscovery(withIpps: failure != "plain only");

		if (failure == "untrusted")
		{
			client.AddUntrusted(IppNetworkPrintSystemTests.SecureOfficeUri);
		}
		else if (failure == "unreachable")
		{
			client.AddUnreachable(IppNetworkPrintSystemTests.SecureOfficeUri);
		}

		IppNetworkOptions options = new IppNetworkOptions { RequireEncryption = true };
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, discovery, options);

		// Act
		Exception? exception = await Record.ExceptionAsync(() => IppNetworkPrintSystemTests.FindOfficeAsync(system));

		// Assert — found over IPPS, or refused with a PrintException; never asked over plain IPP
		Assert.Equal(expected, exception is null);
		Assert.True(expected || exception is PrintException);
		Assert.False(IppNetworkPrintSystemTests.WasAskedOverPlainIpp(client));
	}

	[Fact]
	public async Task GivenEncryptionRequiredWhenAPlainUriIsLookedUpThenThrowsPrintExceptionWithoutContactingIt()
	{
		// Arrange — even with any URI allowed
		FakeIppClient client = IppNetworkPrintSystemTests.CreateClientWithBothAddresses();
		IppNetworkOptions options = new IppNetworkOptions { RequireEncryption = true, AllowAnyPrinterUri = true };
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, new FakeIppPrinterDiscovery(), options);

		// Act
		Exception? exception = await Record.ExceptionAsync
		(
			() => system.FindPrinterAsync("ipp:" + IppNetworkPrintSystemTests.OfficeUri.AbsoluteUri, TestContext.Current.CancellationToken)
		);

		// Assert
		Assert.IsType<PrintException>(exception);
		Assert.Contains("RequireEncryption", exception.Message, StringComparison.Ordinal);
		Assert.Empty(client.GetPrinterAttributesRequests);
	}

	[Fact]
	public async Task GivenAnAddressStoreWhenAPrinterIsReachedOverIppsThenItIsRememberedAcrossARestart()
	{
		// Arrange
		FakeIppClient client = IppNetworkPrintSystemTests.CreateClientWithBothAddresses();
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateOfficeDiscovery(withIpps: true);

		await IppNetworkPrintSystemTests.WithAddressStoreAsync(async options =>
		{
			using (IppNetworkPrintSystem first = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, discovery, options))
			{
				Assert.NotNull(await IppNetworkPrintSystemTests.FindOfficeAsync(first));
			}

			client.AddUntrusted(IppNetworkPrintSystemTests.SecureOfficeUri);
			using IppNetworkPrintSystem second = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, discovery, options);

			// Act
			Exception? exception = await Record.ExceptionAsync(() => IppNetworkPrintSystemTests.FindOfficeAsync(second));

			// Assert — remembered in the file, so the restarted print system doesn't fall back either
			Assert.True(Assert.Single(IppNetworkPrintSystemTests.LoadStore(options)).ReachedOverIpps);
			Assert.IsAssignableFrom<PrintException>(exception);
			Assert.False(IppNetworkPrintSystemTests.WasAskedOverPlainIpp(client));
		});
	}

	[Fact]
	public async Task GivenPrinterThatAnnouncedAUuidWhenItsRememberedAddressAnswersWithoutOneThenItIsSearchedForInstead()
	{
		// Arrange — the address remembered for the name now answers with no printer-uuid at all
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppNetworkPrintSystemTests.OfficeUri, new PrinterAttributesBuilder().WithName("Office").Build());
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri, IppNetworkPrintSystemTests.OfficeUuid);
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery);
		_ = await system.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Act
		_ = await IppNetworkPrintSystemTests.FindOfficeAsync(system);

		// Assert — not taken on trust: the name was searched for
		Assert.Single(discovery.ResolvedNames);
	}

	[Theory]
	[InlineData(null, true)]
	[InlineData(IppNetworkPrintSystemTests.OtherUuid, false)]
	public async Task GivenRememberedAddressWithoutUuidAllowedWhenItsPrinterReportsNoneThenItIsUsedButAnotherUuidIsStillRefused(string? reportedUuid, bool expected)
	{
		// Arrange — the application takes older printers that don't report printer-uuid on trust; the printer at the
		// remembered address reports none, or another printer's (provided by InlineData above)
		PrinterAttributesBuilder attributes = new PrinterAttributesBuilder().WithName("Office");
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppNetworkPrintSystemTests.OfficeUri, (reportedUuid is null ? attributes : attributes.WithUuid("urn:uuid:" + reportedUuid)).Build());
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri, IppNetworkPrintSystemTests.OfficeUuid);
		IppNetworkOptions options = new IppNetworkOptions { AllowRememberedAddressWithoutUuid = true };
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery, options);
		_ = await system.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Act
		_ = await IppNetworkPrintSystemTests.FindOfficeAsync(system);

		// Assert — used at its address without a search, or searched for
		Assert.Equal(expected ? 0 : 1, discovery.ResolvedNames.Count);
	}

	[Fact]
	public async Task GivenPlainIppAfterIppsAllowedWhenAPrinterReachedOverIppsLosesItsIppsAddressThenItFallsBackToPlainIpp()
	{
		// Arrange — reached over IPPS once; then its certificate isn't trusted, and the application allows the fallback
		FakeIppClient client = IppNetworkPrintSystemTests.CreateClientWithBothAddresses();
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateOfficeDiscovery(withIpps: true);
		IppNetworkOptions options = new IppNetworkOptions { AllowPlainIppAfterIpps = true };
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, discovery, options);
		Assert.NotNull(await IppNetworkPrintSystemTests.FindOfficeAsync(system));
		client.AddUntrusted(IppNetworkPrintSystemTests.SecureOfficeUri);

		// Act
		IPrinter? printer = await IppNetworkPrintSystemTests.FindOfficeAsync(system);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal(IppNetworkPrintSystemTests.OfficeUri, client.GetPrinterAttributesRequests[^1].OperationAttributes?.PrinterUri?.Value);
	}

	[Fact]
	public async Task GivenPlainIppAfterIppsAllowedButEncryptionRequiredWhenItsIppsAddressFailsThenItIsStillRefused()
	{
		// Arrange — required encryption wins over the allowed fallback
		FakeIppClient client = IppNetworkPrintSystemTests.CreateClientWithBothAddresses();
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateOfficeDiscovery(withIpps: true);
		IppNetworkOptions options = new IppNetworkOptions { AllowPlainIppAfterIpps = true, RequireEncryption = true };
		using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreateIppsPrintSystem(client, discovery, options);
		client.AddUntrusted(IppNetworkPrintSystemTests.SecureOfficeUri);

		// Act
		Exception? exception = await Record.ExceptionAsync(() => IppNetworkPrintSystemTests.FindOfficeAsync(system));

		// Assert
		Assert.IsAssignableFrom<PrintException>(exception);
		Assert.False(IppNetworkPrintSystemTests.WasAskedOverPlainIpp(client));
	}

	/// <summary>
	/// Creates a fake client with the Office printer at both its plain and its IPPS address.
	/// </summary>
	/// <returns>The client.</returns>
	private static FakeIppClient CreateClientWithBothAddresses()
	{
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppNetworkPrintSystemTests.OfficeUri, new PrinterAttributesBuilder().WithName("Office").Build());
		client.AddPrinter(IppNetworkPrintSystemTests.SecureOfficeUri, new PrinterAttributesBuilder().WithName("Office").Build());

		return client;
	}

	/// <summary>
	/// Creates a discovery that finds the Office printer, at its plain address and, if asked, its IPPS one.
	/// </summary>
	/// <param name="withIpps">Whether it announces its IPPS address too.</param>
	/// <returns>The discovery.</returns>
	private static FakeIppPrinterDiscovery CreateOfficeDiscovery(bool withIpps)
	{
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", IppNetworkPrintSystemTests.OfficeUri, secureUri: withIpps ? IppNetworkPrintSystemTests.SecureOfficeUri : null);

		return discovery;
	}

	/// <summary>
	/// Checks whether the Office printer was asked anything at its plain IPP address.
	/// </summary>
	/// <param name="client">The fake client.</param>
	/// <returns>Whether it was.</returns>
	private static bool WasAskedOverPlainIpp(FakeIppClient client)
	{
		return client.GetPrinterAttributesRequests.Any(request => request.OperationAttributes?.PrinterUri?.Value == IppNetworkPrintSystemTests.OfficeUri);
	}

	/// <summary>
	/// Creates a network print system over fakes that prefers printers' IPPS addresses, as when a certificate is
	/// pinned.
	/// </summary>
	/// <param name="client">The fake IPP client. The print system doesn't own it, so it can be used again.</param>
	/// <param name="discovery">The fake discovery.</param>
	/// <param name="options">The options.</param>
	/// <param name="prefersIpps">Whether IPPS addresses are preferred.</param>
	/// <returns>The print system.</returns>
	private static IppNetworkPrintSystem CreateIppsPrintSystem
	(
		FakeIppClient           client,
		FakeIppPrinterDiscovery discovery,
		IppNetworkOptions       options,
		bool                    prefersIpps = true
	)
	{
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithDocumentFormats(IppDocumentFormats.Standard).Build();

		return new IppNetworkPrintSystem(context, discovery, options, ownedClient: null)
		{
			PrefersIpps = prefersIpps
		};
	}

	[Theory]
	[InlineData("ipp:ipp://192.168.1.20:631/ipp/print")]
	[InlineData("ipp:ipps://192.168.1.20:443/ipp/print")]
	[InlineData("ipp:ipps://192.168.1.20/ipp/print")]
	public async Task GivenPrinterListedWithBothAddressesWhenLookedUpByEitherUriThenItIsKnown(string printerId)
	{
		// Arrange — the last one names IPPS's own port 631, which the printer doesn't use, so isn't known
		Uri plain = new Uri("ipp://192.168.1.20:631/ipp/print");
		Uri secure = new Uri("ipps://192.168.1.20:443/ipp/print");
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(plain, new PrinterAttributesBuilder().WithName("Office").Build());
		client.AddPrinter(secure, new PrinterAttributesBuilder().WithName("Office").Build());
		client.AddPrinter(new Uri("ipps://192.168.1.20/ipp/print"), new PrinterAttributesBuilder().WithName("Office").Build());
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		discovery.AddPrinter("Office", plain, secureUri: secure);
		using IppNetworkPrintSystem printSystem = IppNetworkPrintSystemTests.CreatePrintSystem(client, discovery, new IppNetworkOptions());
		_ = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(!printerId.EndsWith("20/ipp/print", StringComparison.Ordinal), printer is not null);
	}

	[Theory]
	[InlineData("1c852a4d-b800-1f08-abcd-a0d3c1d9e51b", true)]
	[InlineData("99999999-0000-0000-0000-000000000000", false)]
	public async Task GivenAnAddressStoreWhenRestartedThenASleepingPrinterIsFoundAtItsRememberedAddressOnlyIfItIsStillThere(string reportedUuid, bool expected)
	{
		// Arrange — the first run lists the printer; after the restart it doesn't answer the search, and either
		// it, or another printer, answers at its address (provided by InlineData above)
		string path = Path.Combine(Path.GetTempPath(), "latiprimere-tests", Guid.NewGuid().ToString("N"), "printers.json");
		Uri printerUri = new Uri("ipp://192.168.1.20:631/ipp/print");
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(printerUri, new PrinterAttributesBuilder().WithName("Office").WithUuid("urn:uuid:" + reportedUuid).Build());

		FakeIppPrinterDiscovery before = new FakeIppPrinterDiscovery();
		before.AddPrinter("Office", printerUri, "1c852a4d-b800-1f08-abcd-a0d3c1d9e51b");
		IppNetworkOptions options = new IppNetworkOptions { AddressStorePath = path };

		try
		{
			using (IppNetworkPrintSystem first = IppNetworkPrintSystemTests.CreatePrintSystem(client, before, options))
			{
				await first.GetPrintersAsync(TestContext.Current.CancellationToken);
			}

			FakeIppPrinterDiscovery after = new FakeIppPrinterDiscovery();
			using IppNetworkPrintSystem second = IppNetworkPrintSystemTests.CreatePrintSystem(client, after, options);

			// Act
			IPrinter? printer = await second.FindPrinterAsync("ipp:dnssd:Office", TestContext.Current.CancellationToken);

			// Assert — found without searching, or searched for and not found
			Assert.True(File.Exists(path));
			Assert.Equal(expected, printer is not null);
			Assert.Equal(expected ? 0 : 1, after.ResolvedNames.Count);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
		}
	}

	[Fact]
	public async Task GivenMorePrintersAnnouncedThanCanBeRememberedWhenListedThenTheStoreKeepsOnlyTheLimit()
	{
		// Arrange — anyone on the network can announce printers, made up by the hundred
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateDiscovery("Printer", 300);

		await IppNetworkPrintSystemTests.WithAddressStoreAsync(async options =>
		{
			ManualTimeProvider clock = new ManualTimeProvider(IppNetworkPrintSystemTests.Start);
			using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreatePrintSystem(new FakeIppClient(), discovery, options, timeProvider: clock);

			// Act
			await system.GetPrintersAsync(TestContext.Current.CancellationToken);

			// Assert
			Assert.Equal(IppNetworkPrintSystem.MaxRememberedPrinters, IppNetworkPrintSystemTests.LoadStore(options).Count);
		});
	}

	[Fact]
	public async Task GivenPrinterSeenLongestAgoWhenMoreAreAnnouncedThanCanBeRememberedThenItIsForgottenFirst()
	{
		// Arrange — a printer seen an hour before a full set of others
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateDiscovery("Old", 1);

		await IppNetworkPrintSystemTests.WithAddressStoreAsync(async options =>
		{
			ManualTimeProvider clock = new ManualTimeProvider(IppNetworkPrintSystemTests.Start);
			using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreatePrintSystem(new FakeIppClient(), discovery, options, timeProvider: clock);
			await system.GetPrintersAsync(TestContext.Current.CancellationToken);

			discovery.Clear();
			IppNetworkPrintSystemTests.AddPrinters(discovery, "New", IppNetworkPrintSystem.MaxRememberedPrinters);
			clock.Advance(TimeSpan.FromHours(1));

			// Act
			await system.GetPrintersAsync(TestContext.Current.CancellationToken);

			// Assert
			IReadOnlyList<DiscoveredIppPrinter> stored = IppNetworkPrintSystemTests.LoadStore(options);
			Assert.Equal(IppNetworkPrintSystem.MaxRememberedPrinters, stored.Count);
			Assert.DoesNotContain(stored, printer => printer.InstanceName == "Old 1");
		});
	}

	[Fact]
	public async Task GivenPrinterNotSeenForLongerThanItIsRememberedWhenListedThenItIsForgotten()
	{
		// Arrange — one printer seen, then only another, long after
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateDiscovery("Gone", 1);

		await IppNetworkPrintSystemTests.WithAddressStoreAsync(async options =>
		{
			ManualTimeProvider clock = new ManualTimeProvider(IppNetworkPrintSystemTests.Start);
			using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreatePrintSystem(new FakeIppClient(), discovery, options, timeProvider: clock);
			await system.GetPrintersAsync(TestContext.Current.CancellationToken);

			discovery.Clear();
			IppNetworkPrintSystemTests.AddPrinters(discovery, "Here", 1);
			clock.Advance(IppNetworkPrintSystem.RememberedFor + TimeSpan.FromDays(1));

			// Act
			await system.GetPrintersAsync(TestContext.Current.CancellationToken);

			// Assert
			DiscoveredIppPrinter stored = Assert.Single(IppNetworkPrintSystemTests.LoadStore(options));
			Assert.Equal("Here 1", stored.InstanceName);
		});
	}

	[Fact]
	public async Task GivenPrinterSeenAgainSoonAtTheSameAddressWhenListedThenTheStoreIsNotRewritten()
	{
		// Arrange — the same printer, at the same address, an hour apart
		FakeIppPrinterDiscovery discovery = IppNetworkPrintSystemTests.CreateDiscovery("Office", 1);

		await IppNetworkPrintSystemTests.WithAddressStoreAsync(async options =>
		{
			ManualTimeProvider clock = new ManualTimeProvider(IppNetworkPrintSystemTests.Start);
			using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreatePrintSystem(new FakeIppClient(), discovery, options, timeProvider: clock);
			await system.GetPrintersAsync(TestContext.Current.CancellationToken);
			clock.Advance(TimeSpan.FromHours(1));

			// Act
			await system.GetPrintersAsync(TestContext.Current.CancellationToken);

			// Assert — still the time it was first seen: the second sighting didn't write the store
			DiscoveredIppPrinter stored = Assert.Single(IppNetworkPrintSystemTests.LoadStore(options));
			Assert.Equal(IppNetworkPrintSystemTests.Start, stored.LastSeen);
		});
	}

	[Fact]
	public async Task GivenStoreWrittenBeforeTheLimitWhenRestartedThenItShrinksToTheLimit()
	{
		// Arrange — a store filled before the limit, without last-seen times
		await IppNetworkPrintSystemTests.WithAddressStoreAsync(options =>
		{
			IEnumerable<DiscoveredIppPrinter> printers = Enumerable.Range(1, 300).Select(number => new DiscoveredIppPrinter
			{
				InstanceName = "Printer " + number,
				PrinterUri   = new Uri($"ipp://10.0.{number / 250}.{number % 250}:631/ipp/print")
			});

			new IppAddressStore(options.AddressStorePath!, NullLogger.Instance).Save(printers);

			// Act
			using IppNetworkPrintSystem system = IppNetworkPrintSystemTests.CreatePrintSystem(new FakeIppClient(), options: options);

			// Assert
			Assert.Equal(IppNetworkPrintSystem.MaxRememberedPrinters, IppNetworkPrintSystemTests.LoadStore(options).Count);

			return Task.CompletedTask;
		});
	}

	/// <summary>
	/// Runs a test with an address store in a directory of its own, deleted afterwards.
	/// </summary>
	/// <param name="test">The test, given options that name the store.</param>
	/// <returns>A task that completes with the test.</returns>
	private static async Task WithAddressStoreAsync(Func<IppNetworkOptions, Task> test)
	{
		string directory = Path.Combine(Path.GetTempPath(), "latiprimere-tests", Guid.NewGuid().ToString("N"));

		try
		{
			await test(new IppNetworkOptions { AddressStorePath = Path.Combine(directory, "printers.json") });
		}
		finally
		{
			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}
	}

	/// <summary>
	/// Reads the address store a print system wrote.
	/// </summary>
	/// <param name="options">The options that name it.</param>
	/// <returns>The printers in it.</returns>
	private static IReadOnlyList<DiscoveredIppPrinter> LoadStore(IppNetworkOptions options)
	{
		return new IppAddressStore(options.AddressStorePath!, NullLogger.Instance).Load();
	}

	/// <summary>
	/// Creates a discovery that finds numbered printers.
	/// </summary>
	/// <param name="name">The name each starts with.</param>
	/// <param name="count">How many.</param>
	/// <returns>The discovery.</returns>
	private static FakeIppPrinterDiscovery CreateDiscovery(string name, int count)
	{
		FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery();
		IppNetworkPrintSystemTests.AddPrinters(discovery, name, count);

		return discovery;
	}

	/// <summary>
	/// Adds numbered printers, each at an address of its own, to a discovery.
	/// </summary>
	/// <param name="discovery">The discovery.</param>
	/// <param name="name">The name each starts with.</param>
	/// <param name="count">How many.</param>
	private static void AddPrinters(FakeIppPrinterDiscovery discovery, string name, int count)
	{
		// Each set of names gets addresses of its own, from the length and first letter of its name
		int set = ((name.Length * 31) + name[0]) % 200;

		for (int number = 1; number <= count; number++)
		{
			discovery.AddPrinter($"{name} {number}", new Uri($"ipp://10.{set}.{number / 250}.{number % 250}:631/ipp/print"));
		}
	}

	/// <summary>
	/// Creates a network print system over fakes.
	/// </summary>
	/// <param name="client">The fake IPP client, which the print system owns.</param>
	/// <param name="discovery">The fake discovery, or <see langword="null"/> for one that finds nothing.</param>
	/// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
	/// <param name="knownAddressTimeout">
	/// How long a remembered address has to answer, or <see langword="null"/> for the default.
	/// </param>
	/// <param name="timeProvider">The clock, or <see langword="null"/> for the system's.</param>
	/// <returns>The print system.</returns>
	private static IppNetworkPrintSystem CreatePrintSystem
	(
		FakeIppClient            client,
		FakeIppPrinterDiscovery? discovery = null,
		IppNetworkOptions?       options = null,
		TimeSpan?                knownAddressTimeout = null,
		TimeProvider?            timeProvider = null
	)
	{
		IppPrintContext context = new IppPrintContextBuilder()
			.WithClient(client)
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.WithTimeProvider(timeProvider ?? TimeProvider.System)
			.Build();

		return new IppNetworkPrintSystem
		(
			context,
			discovery ?? new FakeIppPrinterDiscovery(),
			options ?? new IppNetworkOptions(),
			client
		)
		{
			KnownAddressTimeout = knownAddressTimeout ?? IppNetworkPrintSystem.DefaultKnownAddressTimeout
		};
	}
}
