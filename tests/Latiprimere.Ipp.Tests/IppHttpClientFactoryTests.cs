using System.Net;
using System.Net.Sockets;
using System.Text;
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

	[Theory]
	[InlineData(0)]
	[InlineData(-2)]
	public void GivenUploadStallTimeoutNotPositiveWhenCreateForTcpThenThrowsArgumentOutOfRangeException(int milliseconds)
	{
		// Arrange — zero, or negative other than infinite (provided by InlineData above)
		TimeSpan uploadStallTimeout = TimeSpan.FromMilliseconds(milliseconds);

		// Act
		Func<HttpClient> createClient = () => IppHttpClientFactory.CreateForTcp
		(
			TimeSpan.FromSeconds(5),
			Latiprimere.Ipp.Connections.IppConnectionSecurity.None,
			uploadStallTimeout
		);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(createClient);
	}

	[Fact]
	public void GivenInfiniteUploadStallTimeoutWhenValidateThenAccepts()
	{
		// Arrange — infinite leaves the stall guard out
		TimeSpan uploadStallTimeout = Timeout.InfiniteTimeSpan;

		// Act
		Exception? exception = Record.Exception(() => IppHttpClientFactory.ValidateUploadStallTimeout(uploadStallTimeout));

		// Assert
		Assert.Null(exception);
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

	[Fact]
	public async Task GivenPrinterThatRedirectsWhenRequestSentThenTheRedirectIsNotFollowed()
	{
		// Arrange — a "printer" that sends the request on to another address, which must never be connected to
		using TcpListener printer = new TcpListener(IPAddress.Loopback, 0);
		using TcpListener elsewhere = new TcpListener(IPAddress.Loopback, 0);
		printer.Start();
		elsewhere.Start();
		int printerPort = ((IPEndPoint)printer.LocalEndpoint).Port;
		int elsewherePort = ((IPEndPoint)elsewhere.LocalEndpoint).Port;
		CancellationToken cancellationToken = TestContext.Current.CancellationToken;

		Task serve = IppHttpClientFactoryTests.RedirectOnceAsync(printer, $"http://127.0.0.1:{elsewherePort}/ipp/print", cancellationToken);
		using HttpClient client = IppHttpClientFactory.CreateForTcp(TimeSpan.FromSeconds(10));
		using ByteArrayContent body = new ByteArrayContent([0x02, 0x00]);

		// Act — 307 keeps the POST, so a client that followed it would send the same request on, to a listener that
		// never answers: the time limit makes that a failure rather than a hang
		using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		limit.CancelAfter(TimeSpan.FromSeconds(10));
		using HttpResponseMessage response = await client.PostAsync(new Uri($"http://127.0.0.1:{printerPort}/ipp/print"), body, limit.Token);
		await serve;

		// Assert
		Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
		Assert.False(elsewhere.Pending());
	}

	/// <summary>
	/// Answers one HTTP request with a redirect.
	/// </summary>
	/// <param name="listener">The listener to accept the request on.</param>
	/// <param name="location">Where the redirect points.</param>
	/// <param name="cancellationToken">A token to stop waiting.</param>
	/// <returns>A task that completes once the redirect has been sent.</returns>
	private static async Task RedirectOnceAsync(TcpListener listener, string location, CancellationToken cancellationToken)
	{
		using TcpClient connection = await listener.AcceptTcpClientAsync(cancellationToken);
		using NetworkStream stream = connection.GetStream();
		byte[] buffer = new byte[4096];
		StringBuilder received = new StringBuilder();

		// Up to the end of the headers; the body is left unread, as the connection closes after the answer
		while (!received.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
		{
			int count = await stream.ReadAsync(buffer, cancellationToken);

			if (count == 0)
			{
				return;
			}

			received.Append(Encoding.ASCII.GetString(buffer, 0, count));
		}

		string response = $"HTTP/1.1 307 Temporary Redirect\r\nLocation: {location}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
		await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
	}
}
