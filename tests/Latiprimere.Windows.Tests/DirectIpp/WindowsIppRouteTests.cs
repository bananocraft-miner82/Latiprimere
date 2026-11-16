using Latiprimere.Ipp;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Windows.DirectIpp;
using Xunit;

namespace Latiprimere.Windows.Tests.DirectIpp;

public sealed class WindowsIppRouteTests
{
	[Theory]
	[InlineData("http://printer:631/ipp/print", "ipp://printer:631/ipp/print")]
	[InlineData("http://printer/ipp", "ipp://printer:80/ipp")]
	[InlineData("https://printer/ipp/print", "ipps://printer:443/ipp/print")]
	[InlineData("ipp://192.168.1.20/ipp/print", "ipp://192.168.1.20/ipp/print")]
	[InlineData("ipps://printer:8443/ipp/print", "ipps://printer:8443/ipp/print")]
	public void GivenQueueAddedByUrlWhenFindPrinterUriThenUsesItsPortAsTheIppUri(string portName, string expected)
	{
		// Arrange — an http URL without a port keeps 80, which ipp wouldn't assume (provided by InlineData above)
		using WindowsIppRoute route = WindowsIppRouteTests.CreateRoute();

		// Act
		Uri? printerUri = route.FindPrinterUri("Office", portName);

		// Assert
		Assert.Equal(new Uri(expected), printerUri);
	}

	[Theory]
	[InlineData("WSD-a316ac12-318f-4e7e-ae4c-f523327f4f2d")]
	[InlineData("192.168.178.123")]
	[InlineData("PORTPROMPT:")]
	[InlineData("FILE:")]
	[InlineData("ftp://printer/queue")]
	[InlineData(null)]
	public void GivenPortThatDoesNotNameThePrinterWhenFindPrinterUriThenReturnsNull(string? portName)
	{
		// Arrange — WSD, TCP/IP and local ports, whose printer isn't guessed (provided by InlineData above)
		using WindowsIppRoute route = WindowsIppRouteTests.CreateRoute();

		// Act
		Uri? printerUri = route.FindPrinterUri("Office", portName);

		// Assert
		Assert.Null(printerUri);
	}

	[Fact]
	public void GivenConfiguredUriWhenFindPrinterUriThenItWinsOverThePortAndIgnoresCase()
	{
		// Arrange
		using WindowsIppRoute route = WindowsIppRouteTests.CreateRoute(("Office", new Uri("http://10.0.0.5/ipp/print")));

		// Act
		Uri? printerUri = route.FindPrinterUri("OFFICE", "http://other:631/ipp/print");

		// Assert
		Assert.Equal(new Uri("ipp://10.0.0.5:80/ipp/print"), printerUri);
	}

	[Theory]
	[InlineData(@"\\server\Office", @"http://printer:631/ipp/print")]
	[InlineData(@"\\server\Configured", null)]
	public void GivenQueueOnPrintServerWhenFindPrinterUriThenReturnsNull(string printerName, string? portName)
	{
		// Arrange — its printer may not be reachable from here, even when configured (provided by InlineData above)
		using WindowsIppRoute route = WindowsIppRouteTests.CreateRoute((@"\\server\Configured", new Uri("ipp://printer/ipp/print")));

		// Act
		Uri? printerUri = route.FindPrinterUri(printerName, portName);

		// Assert
		Assert.Null(printerUri);
	}

	[Theory]
	[InlineData("ftp://printer/queue")]
	[InlineData("file:///C:/printer")]
	public void GivenConfiguredUriIppCantUseWhenCreatedThenThrowsArgumentException(string uri)
	{
		// Arrange — schemes other than ipp, ipps, http and https (provided by InlineData above)
		IppPrintContext context = new IppPrintContextBuilder().Build();
		KeyValuePair<string, Uri>[] configured = [new KeyValuePair<string, Uri>("Office", new Uri(uri))];

		// Act
		Func<WindowsIppRoute> create = () => new WindowsIppRoute(context, configured);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(create);
		Assert.Contains("'Office'", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenRouteWhenDisposedThenDisposesItsClient()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		WindowsIppRoute route = new WindowsIppRoute(new IppPrintContextBuilder().WithClient(client).Build(), []);

		// Act
		route.Dispose();

		// Assert
		Assert.True(client.IsDisposed);
	}

	/// <summary>
	/// Creates a route over a fake client, with the given configured URIs.
	/// </summary>
	/// <param name="configured">Queue names and their URIs.</param>
	/// <returns>The route. The caller disposes it.</returns>
	private static WindowsIppRoute CreateRoute(params (string Name, Uri Uri)[] configured)
	{
		IppPrintContext context = new IppPrintContextBuilder().Build();

		return new WindowsIppRoute(context, configured.Select(entry => new KeyValuePair<string, Uri>(entry.Name, entry.Uri)));
	}
}
