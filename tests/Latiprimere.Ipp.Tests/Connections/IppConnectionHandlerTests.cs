using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Latiprimere.Ipp.Connections;
using Latiprimere.Ipp.Tests.Fakes;
using Xunit;

namespace Latiprimere.Ipp.Tests.Connections;

public sealed class IppConnectionHandlerTests
{
	[Fact]
	public async Task GivenTlsWhenHttpRequestSentThenSendsItOverHttpsOnTheSamePort()
	{
		// Arrange
		StubHttpMessageHandler stub = IppConnectionHandlerTests.CreateStub();
		using HttpClient client = IppConnectionHandlerTests.CreateClient(new IppConnectionSecurity { UseTls = true }, stub);

		// Act
		await IppConnectionHandlerTests.PostAsync(client, "http://printserver:631/printers/Office");

		// Assert
		Assert.Equal(new Uri("https://printserver:631/printers/Office"), stub.LastRequestUri);
	}

	[Fact]
	public async Task GivenTlsWhenRequestOnImpliedHttpPortSentThenUsesImpliedHttpsPort()
	{
		// Arrange
		StubHttpMessageHandler stub = IppConnectionHandlerTests.CreateStub();
		using HttpClient client = IppConnectionHandlerTests.CreateClient(new IppConnectionSecurity { UseTls = true }, stub);

		// Act
		await IppConnectionHandlerTests.PostAsync(client, "http://printserver/printers/Office");

		// Assert
		Assert.Equal(new Uri("https://printserver/printers/Office"), stub.LastRequestUri);
	}

	[Fact]
	public async Task GivenNoTlsWhenHttpRequestSentThenLeavesItOnHttp()
	{
		// Arrange — credentials alone, as with CupsEncryption.Never
		StubHttpMessageHandler stub = IppConnectionHandlerTests.CreateStub();
		IppConnectionSecurity security = new IppConnectionSecurity { Credentials = new NetworkCredential("ann", "secret") };
		using HttpClient client = IppConnectionHandlerTests.CreateClient(security, stub);

		// Act
		await IppConnectionHandlerTests.PostAsync(client, "http://printserver:631/printers/Office");

		// Assert
		Assert.Equal(new Uri("http://printserver:631/printers/Office"), stub.LastRequestUri);
	}

	[Fact]
	public async Task GivenCredentialsWhenRequestSentThenSendsThemAsBasicAuthenticationInUtf8()
	{
		// Arrange
		StubHttpMessageHandler stub = IppConnectionHandlerTests.CreateStub();

		IppConnectionSecurity security = new IppConnectionSecurity
		{
			UseTls      = true,
			Credentials = new NetworkCredential("zoë", "pa:ss")
		};

		using HttpClient client = IppConnectionHandlerTests.CreateClient(security, stub);

		// Act
		await IppConnectionHandlerTests.PostAsync(client, "http://printserver:631/printers/Office");

		// Assert — a colon in the password is fine: the first colon separates the two
		AuthenticationHeaderValue? authorization = stub.LastAuthorization;
		Assert.Equal("Basic", authorization?.Scheme);
		Assert.Equal("zoë:pa:ss", Encoding.UTF8.GetString(Convert.FromBase64String(authorization?.Parameter ?? string.Empty)));
	}

	[Fact]
	public async Task GivenNoCredentialsWhenRequestSentThenSendsNoAuthorization()
	{
		// Arrange
		StubHttpMessageHandler stub = IppConnectionHandlerTests.CreateStub();
		using HttpClient client = IppConnectionHandlerTests.CreateClient(new IppConnectionSecurity { UseTls = true }, stub);

		// Act
		await IppConnectionHandlerTests.PostAsync(client, "http://printserver:631/printers/Office");

		// Assert
		Assert.Null(stub.LastAuthorization);
	}

	/// <summary>
	/// Creates a handler that answers every request with an empty 200 response.
	/// </summary>
	/// <returns>The handler.</returns>
	private static StubHttpMessageHandler CreateStub()
	{
		return new StubHttpMessageHandler(HttpStatusCode.OK, "application/ipp", Array.Empty<byte>());
	}

	/// <summary>
	/// Creates an HTTP client that sends through the handler under test to the stub.
	/// </summary>
	/// <param name="security">How the handler under test protects requests.</param>
	/// <param name="stub">The stub that answers.</param>
	/// <returns>The client, which owns both handlers.</returns>
	private static HttpClient CreateClient(IppConnectionSecurity security, StubHttpMessageHandler stub)
	{
		return new HttpClient(new IppConnectionHandler(security, stub), disposeHandler: true);
	}

	/// <summary>
	/// Posts an empty IPP request.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <param name="uri">Where to post it.</param>
	/// <returns>A task that completes with the response.</returns>
	private static async Task PostAsync(HttpClient client, string uri)
	{
		using ByteArrayContent content = new ByteArrayContent(Array.Empty<byte>());
		using HttpResponseMessage response = await client.PostAsync(new Uri(uri), content, TestContext.Current.CancellationToken);
	}
}
