using Xunit;

namespace Latiprimere.Cups.Tests;

public sealed class CupsEndpointTests
{
	[Fact]
	public void GivenConfiguredSocketPathWhenResolveThenUsesSocketExplicitly()
	{
		// Arrange
		const string socketPath = "/var/run/custom-cups.sock";

		// Act
		CupsEndpoint endpoint = CupsEndpoint.Resolve
		(
			socketPath,
			null,
			CupsEndpointTests.SocketNeverExists,
			CupsPlatform.Other
		);

		// Assert
		Assert.Equal(socketPath, endpoint.SocketPath);
		Assert.Equal(new Uri("ipp://localhost:631/"), endpoint.ServerUri);
		Assert.True(endpoint.IsExplicit);
	}

	[Theory]
	[InlineData("printserver", "ipp://printserver:631/")]
	[InlineData("printserver:8631", "ipp://printserver:8631/")]
	[InlineData("192.168.1.20:631", "ipp://192.168.1.20:631/")]
	[InlineData("[::1]:8631", "ipp://[::1]:8631/")]
	[InlineData("  localhost:631  ", "ipp://localhost:631/")]
	[InlineData("printserver/version=1.1", "ipp://printserver:631/")]
	[InlineData("printserver:8631/version=2.0", "ipp://printserver:8631/")]
	public void GivenConfiguredHostWhenResolveThenUsesTcpExplicitly(string server, string expectedUri)
	{
		// Arrange — server is a host with an optional port, in CUPS_SERVER form, optionally with libcups'
		// /version= suffix, which is ignored (provided by InlineData above)
		string configuredServer = server;

		// Act
		CupsEndpoint endpoint = CupsEndpoint.Resolve
		(
			configuredServer,
			null,
			CupsEndpointTests.SocketAlwaysExists,
			CupsPlatform.Linux
		);

		// Assert
		Assert.Equal(new Uri(expectedUri), endpoint.ServerUri);
		Assert.Null(endpoint.SocketPath);
		Assert.True(endpoint.IsExplicit);
	}

	[Theory]
	[InlineData("printserver/queues")]
	[InlineData("printserver:notaport")]
	[InlineData("http://printserver")]
	[InlineData("ann@printserver")]
	public void GivenInvalidConfiguredServerWhenResolveThenThrowsArgumentException(string server)
	{
		// Arrange — server is not a socket path or host[:port]; a user name isn't part of the form either
		// (provided by InlineData above)
		string configuredServer = server;

		// Act
		Func<CupsEndpoint> resolve = () => CupsEndpoint.Resolve
		(
			configuredServer,
			null,
			CupsEndpointTests.SocketNeverExists,
			CupsPlatform.Linux
		);

		// Assert
		Assert.Throws<ArgumentException>(resolve);
	}

	[Fact]
	public void GivenInvalidEnvironmentServerWhenResolveThenReturnsEndpointThatRecordsIt()
	{
		// Arrange — CUPS_SERVER comes from the user's environment, so it mustn't stop the application starting
		const string environmentServer = "printserver:notaport";

		// Act
		CupsEndpoint endpoint = CupsEndpoint.Resolve
		(
			null,
			environmentServer,
			CupsEndpointTests.SocketNeverExists,
			CupsPlatform.Linux
		);

		// Assert
		Assert.Equal(environmentServer, endpoint.InvalidEnvironmentServer);
	}

	[Fact]
	public void GivenOnlyEnvironmentServerWhenResolveThenUsesEnvironment()
	{
		// Arrange
		const string environmentServer = "printserver:631";

		// Act
		CupsEndpoint endpoint = CupsEndpoint.Resolve
		(
			null,
			environmentServer,
			CupsEndpointTests.SocketAlwaysExists,
			CupsPlatform.Linux
		);

		// Assert
		Assert.Equal(new Uri("ipp://printserver:631/"), endpoint.ServerUri);
		Assert.True(endpoint.IsExplicit);
	}

	[Fact]
	public void GivenConfiguredAndEnvironmentServerWhenResolveThenConfiguredWins()
	{
		// Arrange
		const string configuredServer = "configured:631";
		const string environmentServer = "environment:631";

		// Act
		CupsEndpoint endpoint = CupsEndpoint.Resolve
		(
			configuredServer,
			environmentServer,
			CupsEndpointTests.SocketNeverExists,
			CupsPlatform.Other
		);

		// Assert
		Assert.Equal("configured", endpoint.ServerUri.Host);
	}

	[Fact]
	public void GivenConfiguredServerAndInvalidEnvironmentServerWhenResolveThenConfiguredWinsWithoutTheFailure()
	{
		// Arrange — the environment value is never used, so it mustn't make CUPS fail
		const string configuredServer = "configured:631";
		const string environmentServer = "environment:notaport";

		// Act
		CupsEndpoint endpoint = CupsEndpoint.Resolve
		(
			configuredServer,
			environmentServer,
			CupsEndpointTests.SocketNeverExists,
			CupsPlatform.Linux
		);

		// Assert
		Assert.Equal("configured", endpoint.ServerUri.Host);
		Assert.Null(endpoint.InvalidEnvironmentServer);
	}

	[Theory]
	[InlineData("Linux", "/run/cups/cups.sock")]
	[InlineData("MacOS", "/private/var/run/cupsd")]
	public void GivenLocalSocketExistsWhenResolveThenUsesPlatformSocket(string platformName, string expectedSocketPath)
	{
		// Arrange — each platform name and its scheduler socket (provided by InlineData above)
		CupsPlatform platform = Enum.Parse<CupsPlatform>(platformName);

		bool socketExists(string path)
		{
			return path == expectedSocketPath;
		}

		// Act
		CupsEndpoint endpoint = CupsEndpoint.Resolve(null, null, socketExists, platform);

		// Assert
		Assert.Equal(expectedSocketPath, endpoint.SocketPath);
		Assert.False(endpoint.IsExplicit);
	}

	[Theory]
	[InlineData("Linux")]
	[InlineData("Other")]
	public void GivenNoSocketAndNoServerWhenResolveThenFallsBackToLocalTcp(string platformName)
	{
		// Arrange — a platform whose scheduler socket is missing, or that has none (provided by InlineData above)
		CupsPlatform currentPlatform = Enum.Parse<CupsPlatform>(platformName);

		// Act
		CupsEndpoint endpoint = CupsEndpoint.Resolve("  ", null, CupsEndpointTests.SocketNeverExists, currentPlatform);

		// Assert
		Assert.Equal(new Uri("ipp://localhost:631/"), endpoint.ServerUri);
		Assert.Null(endpoint.SocketPath);
		Assert.False(endpoint.IsExplicit);
	}

	[Fact]
	public void GivenQueueNameWithSpaceWhenGetPrinterUriThenEscapesIt()
	{
		// Arrange
		CupsEndpoint endpoint = CupsEndpoint.Resolve
		(
			"printserver:631",
			null,
			CupsEndpointTests.SocketNeverExists,
			CupsPlatform.Other
		);

		// Act
		Uri printerUri = endpoint.GetPrinterUri("Office Laser");

		// Assert
		Assert.Equal("ipp://printserver:631/printers/Office%20Laser", printerUri.AbsoluteUri);
	}

	/// <summary>
	/// A socket check that always finds the socket.
	/// </summary>
	/// <param name="path">The socket path, which isn't checked.</param>
	/// <returns>Always <see langword="true"/>.</returns>
	private static bool SocketAlwaysExists(string path)
	{
		return true;
	}

	/// <summary>
	/// A socket check that never finds the socket.
	/// </summary>
	/// <param name="path">The socket path, which isn't checked.</param>
	/// <returns>Always <see langword="false"/>.</returns>
	private static bool SocketNeverExists(string path)
	{
		return false;
	}
}
