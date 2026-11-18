using Latiprimere.Ipp;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Tests.Fakes;
using Xunit;

namespace Latiprimere.Windows.Tests.DirectIpp;

/// <summary>
/// WSD queues matched to printers announced over mDNS by the UUID their port records (ADR 0036).
/// </summary>
public sealed class WindowsIppRouteWsdTests : IDisposable
{
	/// <summary>The Canon TS7500i's WSD port on the development PC.</summary>
	private const string PortName = "WSD-adb74ae8-1d57-46ea-9170-4b578f4949be";

	/// <summary>The printer UUID that port records.</summary>
	private const string Uuid = "00000000-0000-1000-8000-00117d58570e";

	private static readonly Uri PrinterUri = new Uri("ipp://canon.local:631/ipp/print"); // Where it's announced

	private readonly FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery(); // The network
	private readonly FakeWinSpool spool = new FakeWinSpool();                           // The registry
	private readonly ManualTimeProvider clock = new ManualTimeProvider();               // The time
	private readonly WindowsIppRoute route;                                             // The route under test

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsIppRouteWsdTests"/> class.
	/// </summary>
	public WindowsIppRouteWsdTests()
	{
		IppPrintContext context = new IppPrintContextBuilder().Build() with { TimeProvider = this.clock };
		this.route = new WindowsIppRoute(context, [], this.discovery);
		this.spool.WsdPrinterUuids[WindowsIppRouteWsdTests.PortName] = WindowsIppRouteWsdTests.Uuid;
	}

	[Theory]
	[InlineData(Uuid)]
	[InlineData("urn:uuid:00000000-0000-1000-8000-00117D58570E")]
	public async Task GivenAWsdQueueWhosePrinterAnnouncesItsUuidWhenFindingThenItIsTheTarget(string announced)
	{
		// Arrange — announced with or without urn:uuid:, in either case (provided by InlineData above)
		this.discovery.AddPrinter("Canon TS7500i series", WindowsIppRouteWsdTests.PrinterUri, Latiprimere.Ipp.Discovery.DnsSdServiceMapper.NormalizeUuid(announced));

		// Act
		WindowsIppTarget? target = await this.route.FindTargetAsync("Canon TS7500i series", WindowsIppRouteWsdTests.PortName, this.spool, TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(target);
		Assert.Equal(WindowsIppRouteWsdTests.PrinterUri, target.PrinterUri);
		Assert.Equal(WindowsIppRouteWsdTests.Uuid, target.Uuid);
		Assert.Equal([WindowsIppRouteWsdTests.Uuid], this.discovery.SearchedUuids);
	}

	[Fact]
	public async Task GivenAMatchWhenFindingAgainThenItIsRememberedUntilForgotten()
	{
		// Arrange
		this.discovery.AddPrinter("Canon", WindowsIppRouteWsdTests.PrinterUri, WindowsIppRouteWsdTests.Uuid);
		CancellationToken token = TestContext.Current.CancellationToken;
		await this.route.FindTargetAsync("Canon", WindowsIppRouteWsdTests.PortName, this.spool, token);

		// Act
		WindowsIppTarget? remembered = await this.route.FindTargetAsync("Canon", WindowsIppRouteWsdTests.PortName, this.spool, token);
		int searchesWhileRemembered = this.discovery.SearchedUuids.Count;
		this.route.ForgetMatch(WindowsIppRouteWsdTests.Uuid);
		await this.route.FindTargetAsync("Canon", WindowsIppRouteWsdTests.PortName, this.spool, token);

		// Assert
		Assert.Equal(WindowsIppRouteWsdTests.PrinterUri, remembered?.PrinterUri);
		Assert.Equal(1, searchesWhileRemembered);
		Assert.Equal(2, this.discovery.SearchedUuids.Count);
	}

	[Fact]
	public async Task GivenNoPrinterAnnouncesTheUuidWhenFindingThenNoneAndNotSearchedAgainForAWhile()
	{
		// Arrange — another printer is announced
		this.discovery.AddPrinter("Other", new Uri("ipp://other.local/ipp/print"), "11111111-2222-3333-4444-555555555555");
		CancellationToken token = TestContext.Current.CancellationToken;

		// Act
		WindowsIppTarget? first = await this.route.FindTargetAsync("Canon", WindowsIppRouteWsdTests.PortName, this.spool, token);
		WindowsIppTarget? soon = await this.route.FindTargetAsync("Canon", WindowsIppRouteWsdTests.PortName, this.spool, token);
		int searchesSoon = this.discovery.SearchedUuids.Count;
		this.clock.Advance(WindowsIppRoute.UnmatchedRetryAfter);
		await this.route.FindTargetAsync("Canon", WindowsIppRouteWsdTests.PortName, this.spool, token);

		// Assert
		Assert.Null(first);
		Assert.Null(soon);
		Assert.Equal(1, searchesSoon);
		Assert.Equal(2, this.discovery.SearchedUuids.Count);
	}

	[Fact]
	public async Task GivenAnExactUriWhenFindingThenItIsUsedWithoutReadingThePortOrSearching()
	{
		// Arrange
		this.discovery.AddPrinter("Canon", WindowsIppRouteWsdTests.PrinterUri, WindowsIppRouteWsdTests.Uuid);

		// Act
		WindowsIppTarget? target = await this.route.FindTargetAsync("Office", "http://printer:631/ipp/print", this.spool, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(new Uri("ipp://printer:631/ipp/print"), target?.PrinterUri);
		Assert.Null(target?.Uuid);
		Assert.Empty(this.spool.WsdPortsRead);
		Assert.Empty(this.discovery.SearchedUuids);
	}

	[Theory]
	[InlineData(@"\\server\Canon", PortName)]
	[InlineData("Canon", "WSD-00000000-0000-0000-0000-000000000000")]
	[InlineData("Canon", "192.168.1.20")]
	[InlineData("Canon", null)]
	public async Task GivenAQueueWithoutAUuidOrOnAServerWhenFindingThenNoneWithoutSearching(string printerName, string? portName)
	{
		// Arrange — a print server's queue, a WSD port recording no UUID, a TCP/IP port, no port (provided by InlineData above)
		this.discovery.AddPrinter("Canon", WindowsIppRouteWsdTests.PrinterUri, WindowsIppRouteWsdTests.Uuid);

		// Act
		WindowsIppTarget? target = await this.route.FindTargetAsync(printerName, portName, this.spool, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(target);
		Assert.Empty(this.discovery.SearchedUuids);
	}

	[Fact]
	public async Task GivenNoDiscoveryWhenFindingAWsdQueueThenNone()
	{
		// Arrange — matching off, as the route was before ADR 0036
		using WindowsIppRoute exactOnly = new WindowsIppRoute(new IppPrintContextBuilder().Build(), []);

		// Act
		WindowsIppTarget? target = await exactOnly.FindTargetAsync("Canon", WindowsIppRouteWsdTests.PortName, this.spool, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(target);
		Assert.Empty(this.spool.WsdPortsRead);
	}

	[Fact]
	public async Task GivenTheSearchFailsWhenFindingThenNoneSoTheQueuePrints()
	{
		// Arrange — multicast unavailable, for one
		this.discovery.UuidSearchFailure = new System.Net.Sockets.SocketException(10049);

		// Act
		WindowsIppTarget? target = await this.route.FindTargetAsync("Canon", WindowsIppRouteWsdTests.PortName, this.spool, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(target);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.route.Dispose();
	}
}
