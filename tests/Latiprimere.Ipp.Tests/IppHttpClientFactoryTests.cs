using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppHttpClientFactoryTests
{
	[Fact]
	public void GivenAnyTransportWhenCreatedThenClientHasNoOverallTimeout()
	{
		// Arrange — HttpClient's default 100 s would also cut off uploading a large document

		// Act
		using HttpClient tcpClient = IppHttpClientFactory.CreateForTcp(TimeSpan.FromSeconds(5));
		using HttpClient socketClient = IppHttpClientFactory.CreateForUnixSocket("/run/cups/cups.sock");

		// Assert
		Assert.Equal(Timeout.InfiniteTimeSpan, tcpClient.Timeout);
		Assert.Equal(Timeout.InfiniteTimeSpan, socketClient.Timeout);
	}

	[Fact]
	public void GivenPositiveConnectTimeoutWhenCreateForTcpThenReturnsClient()
	{
		// Arrange
		TimeSpan connectTimeout = TimeSpan.FromSeconds(5);

		// Act
		using HttpClient client = IppHttpClientFactory.CreateForTcp(connectTimeout);

		// Assert
		Assert.NotNull(client);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1000)]
	public void GivenConnectTimeoutNotPositiveWhenCreateForTcpThenThrowsArgumentOutOfRangeException(int milliseconds)
	{
		// Arrange — milliseconds is zero or negative (provided by InlineData above)
		TimeSpan connectTimeout = TimeSpan.FromMilliseconds(milliseconds);

		// Act
		Func<HttpClient> createClient = () => IppHttpClientFactory.CreateForTcp(connectTimeout);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(createClient);
	}

	[Fact]
	public void GivenConnectTimeoutBeyondHandlerLimitWhenValidateThenThrowsArgumentOutOfRangeException()
	{
		// Arrange — SocketsHttpHandler accepts at most int.MaxValue milliseconds
		TimeSpan connectTimeout = TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromMilliseconds(1);

		// Act
		Action validate = () => IppHttpClientFactory.ValidateConnectTimeout(connectTimeout);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(validate);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void GivenBlankSocketPathWhenCreateForUnixSocketThenThrowsArgumentException(string socketPath)
	{
		// Arrange — socketPath is empty or whitespace (provided by InlineData above)
		string path = socketPath;

		// Act
		Func<HttpClient> createClient = () => IppHttpClientFactory.CreateForUnixSocket(path);

		// Assert
		Assert.Throws<ArgumentException>(createClient);
	}

	[Fact]
	public void GivenSocketPathWhenCreateForUnixSocketThenReturnsClient()
	{
		// Arrange
		const string socketPath = "/run/cups/cups.sock";

		// Act
		using HttpClient client = IppHttpClientFactory.CreateForUnixSocket(socketPath);

		// Assert
		Assert.NotNull(client);
	}
}
