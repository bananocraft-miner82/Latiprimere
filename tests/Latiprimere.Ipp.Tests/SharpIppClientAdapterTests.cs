using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Tests.Fakes;
using SharpIpp.Exceptions;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class SharpIppClientAdapterTests
{
	private static readonly Uri PrinterUri = new Uri("ipp://192.0.2.10/ipp/print"); // A printer that can't be reached

	[Fact]
	public async Task GivenConnectTimeoutWhenRequestSentThenThrowsPrintExceptionKeepingTheTimeout()
	{
		// Arrange — what SocketsHttpHandler throws when ConnectTimeout runs out
		TaskCanceledException timeout = new TaskCanceledException
		(
			"The operation was canceled.",
			new TimeoutException("A connection could not be established within the configured ConnectTimeout.")
		);

		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateFailingClient(timeout);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.Contains(SharpIppClientAdapterTests.PrinterUri.ToString(), exception.Message, StringComparison.Ordinal);
		Assert.Same(timeout, exception.InnerException);
		Assert.Null(exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenCancellationWithoutTimeoutWhenRequestSentThenCancellationPassesThrough()
	{
		// Arrange — the caller's own cancellation carries no TimeoutException
		TaskCanceledException cancellation = new TaskCanceledException("The operation was canceled.");
		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateFailingClient(cancellation);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		TaskCanceledException exception = await Assert.ThrowsAsync<TaskCanceledException>(request);
		Assert.Same(cancellation, exception);
	}

	[Fact]
	public async Task GivenConnectionRefusedWhenRequestSentThenThrowsPrintException()
	{
		// Arrange — what SocketsHttpHandler throws when nothing listens at the address
		HttpRequestException refused = new HttpRequestException
		(
			"No connection could be made because the target machine actively refused it.",
			new SocketException((int)SocketError.ConnectionRefused)
		);

		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateFailingClient(refused);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.Same(refused, exception.InnerException);
	}

	[Theory]
	[InlineData(false, "needs a user name and password")]
	[InlineData(true, "did not accept the user name and password")]
	public async Task GivenHttpUnauthorizedWhenRequestSentThenThrowsPrintAuthenticationException(bool sendsCredentials, string expected)
	{
		// Arrange — sendsCredentials is whether credentials were sent, and expected what the message says (provided
		// by InlineData above). CUPS answers 401 with an HTML page
		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateHtmlClient(HttpStatusCode.Unauthorized, sendsCredentials);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(request);
		Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
		Assert.Contains(SharpIppClientAdapterTests.PrinterUri.ToString(), exception.Message, StringComparison.Ordinal);
		Assert.Equal(401, exception.NativeErrorCode);
	}

	[Theory]
	[InlineData(false, "needs a user name and password, sent over an encrypted connection")]
	[InlineData(true, "encryption is turned off")]
	public async Task GivenHttpUpgradeRequiredWhenRequestSentThenThrowsPrintAuthenticationException(bool sendsCredentials, string expected)
	{
		// Arrange — sendsCredentials is whether credentials were sent, and expected what the message says (provided
		// by InlineData above). CUPS answers 426 when it wants TLS before it asks for a password
		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateHtmlClient(HttpStatusCode.UpgradeRequired, sendsCredentials);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(request);
		Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
		Assert.Equal(426, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenHttpForbiddenWhenRequestSentThenThrowsPrintExceptionSayingTheUserIsNotAllowed()
	{
		// Arrange — CUPS answers 403 to an authenticated user the policy doesn't allow
		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateHtmlClient(HttpStatusCode.Forbidden, sendsCredentials: true);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert — the credentials were accepted, so asking for others wouldn't help
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.IsNotType<PrintAuthenticationException>(exception);
		Assert.Contains("does not allow this user", exception.Message, StringComparison.Ordinal);
		Assert.Equal(403, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenIppNotAuthenticatedStatusWhenRequestSentThenThrowsPrintAuthenticationException()
	{
		// Arrange — a printer answering with IPP's own client-error-not-authenticated (0x0402)
		byte[] response = SharpIppClientAdapterTests.CreateIppResponse(0x0402);
		StubHttpMessageHandler stub = new StubHttpMessageHandler(HttpStatusCode.OK, "application/ipp", response);
		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true));

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(request);
		Assert.Equal(0x0402, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenQueryAnsweredAfterResponseTimeoutWhenSentThenThrowsPrintExceptionAboutTheTimeout()
	{
		// Arrange — a printer that takes longer to answer than the response timeout allows
		StubHttpMessageHandler stub = new StubHttpMessageHandler
		(
			HttpStatusCode.OK,
			"application/ipp",
			SharpIppClientAdapterTests.CreateIppResponse(0x0000),
			TimeSpan.FromSeconds(30)
		);

		using SharpIppClientAdapter client = new SharpIppClientAdapter
		(
			new HttpClient(stub, disposeHandler: true),
			sendsCredentials: false,
			TimeSpan.FromMilliseconds(100)
		);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.Contains("did not answer in time", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenPrintJobTakingLongerThanResponseTimeoutWhenSentThenWaitsForTheAnswer()
	{
		// Arrange — uploading a document can take far longer than a query, so Print-Job has no response timeout
		StubHttpMessageHandler stub = new StubHttpMessageHandler
		(
			HttpStatusCode.OK,
			"application/ipp",
			SharpIppClientAdapterTests.CreateIppResponse(0x0000),
			TimeSpan.FromMilliseconds(500)
		);

		using SharpIppClientAdapter client = new SharpIppClientAdapter
		(
			new HttpClient(stub, disposeHandler: true),
			sendsCredentials: false,
			TimeSpan.FromMilliseconds(100)
		);

		using MemoryStream document = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

		PrintJobRequest request = new PrintJobRequest
		{
			Document            = document,
			OperationAttributes = new PrintJobOperationAttributes
			{
				PrinterUri         = new IppValue<Uri>(SharpIppClientAdapterTests.PrinterUri),
				RequestingUserName = new StringWithLanguage("ann")
			}
		};

		// Act
		PrintJobResponse response = await client.PrintJobAsync(request, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(IppStatusCode.SuccessfulOk, response.StatusCode);
	}

	[Fact]
	public async Task GivenNoDefaultDestinationWhenCupsDefaultPrinterRequestedThenReturnsNull()
	{
		// Arrange — CUPS answers CUPS-Get-Default with client-error-not-found (0x0406) when no default is set, as on a
		// Mac with no printer chosen as default
		byte[] response = SharpIppClientAdapterTests.CreateIppResponse(0x0406);
		StubHttpMessageHandler stub = new StubHttpMessageHandler(HttpStatusCode.OK, "application/ipp", response);
		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true));

		// Act
		string? name = await client.GetCupsDefaultPrinterNameAsync
		(
			new Uri("ipp://printserver:631/"),
			"ann",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.Null(name);
	}

	[Fact]
	public async Task GivenOtherErrorStatusWhenCupsDefaultPrinterRequestedThenThrowsPrintException()
	{
		// Arrange — client-error-bad-request (0x0400): only not-found means "no default"
		byte[] response = SharpIppClientAdapterTests.CreateIppResponse(0x0400);
		StubHttpMessageHandler stub = new StubHttpMessageHandler(HttpStatusCode.OK, "application/ipp", response);
		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true));

		// Act
		Func<Task> request = () => client.GetCupsDefaultPrinterNameAsync
		(
			new Uri("ipp://printserver:631/"),
			"ann",
			TestContext.Current.CancellationToken
		);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.Equal(0x0400, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenTlsHandshakeFailureWhenRequestSentThenThrowsPrintExceptionAboutTheCertificate()
	{
		// Arrange — what SocketsHttpHandler throws when the server's certificate isn't trusted
		HttpRequestException handshakeFailure = new HttpRequestException
		(
			HttpRequestError.SecureConnectionError,
			"The SSL connection could not be established, see inner exception.",
			new AuthenticationException("The remote certificate is invalid because of errors in the certificate chain: UntrustedRoot")
		);

		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateFailingClient(handshakeFailure);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.Contains("encrypted connection", exception.Message, StringComparison.Ordinal);
		Assert.Contains("certificate", exception.Message, StringComparison.Ordinal);
		Assert.Same(handshakeFailure, exception.InnerException);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivenDefaultDestinationWhenCupsDefaultPrinterRequestedThenReturnsItsName(bool withLanguage)
	{
		// Arrange — withLanguage is whether CUPS sends the name as nameWithLanguage (0x36) rather than
		// nameWithoutLanguage (0x42) (provided by InlineData above)
		byte[] response = withLanguage
			? SharpIppClientAdapterTests.CreateIppResponse(0, 0x36, SharpIppClientAdapterTests.EncodeWithLanguage("en", "Office"))
			: SharpIppClientAdapterTests.CreateIppResponse(0, 0x42, Encoding.ASCII.GetBytes("Office"));

		StubHttpMessageHandler stub = new StubHttpMessageHandler(HttpStatusCode.OK, "application/ipp", response);
		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true));

		// Act
		string? name = await client.GetCupsDefaultPrinterNameAsync
		(
			new Uri("ipp://printserver:631/"),
			"ann",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.Equal("Office", name);
	}

	[Fact]
	public async Task GivenSuccessWithoutPrinterNameWhenCupsDefaultPrinterRequestedThenReturnsNull()
	{
		// Arrange — successful-ok, but no printer attributes
		byte[] response = SharpIppClientAdapterTests.CreateIppResponse(0);
		StubHttpMessageHandler stub = new StubHttpMessageHandler(HttpStatusCode.OK, "application/ipp", response);
		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true));

		// Act
		string? name = await client.GetCupsDefaultPrinterNameAsync
		(
			new Uri("ipp://printserver:631/"),
			"ann",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.Null(name);
	}

	[Theory]
	[InlineData(new byte[] { 1, 1, 0 })]
	[InlineData(new byte[] { 1, 1, 0, 0, 0, 0, 0, 1, 0x01, 0x47, 0, 50 })]
	public async Task GivenMalformedIppAnswerWhenRequestSentThenThrowsPrintExceptionNamingThePrinter(byte[] response)
	{
		// Arrange — response ends partway through its header, or through an attribute name (provided by InlineData
		// above). A PrintException lets the catalog skip the print system instead of failing (ADR 0008)
		StubHttpMessageHandler stub = new StubHttpMessageHandler(HttpStatusCode.OK, "application/ipp", response);
		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true));

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert — no IPP status to report, so none is named
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.Contains("can't read", exception.Message, StringComparison.Ordinal);
		Assert.Contains(SharpIppClientAdapterTests.PrinterUri.ToString(), exception.Message, StringComparison.Ordinal);
		Assert.Null(exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenWebPageAnswerWhenRequestSentThenThrowsPrintExceptionSayingTheAddressIsNotIpp()
	{
		// Arrange — a URI that points at a printer's or CUPS's web interface, which answers 200 with HTML
		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateHtmlClient(HttpStatusCode.OK, sendsCredentials: false);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert — says where to look, rather than reporting an IPP status the page never sent
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.Contains("not with IPP", exception.Message, StringComparison.Ordinal);
		Assert.Contains("web interface", exception.Message, StringComparison.Ordinal);
		Assert.Contains(SharpIppClientAdapterTests.PrinterUri.ToString(), exception.Message, StringComparison.Ordinal);
		Assert.DoesNotContain("IPP status", exception.Message, StringComparison.Ordinal);
		Assert.Null(exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenHttpUnauthorizedWithIppBodyWhenRequestSentThenThrowsPrintAuthenticationException()
	{
		// Arrange — a refusal that comes with an IPP body, which SharpIppNext decodes before throwing
		byte[] response = SharpIppClientAdapterTests.CreateIppResponse(0x0402);
		StubHttpMessageHandler stub = new StubHttpMessageHandler(HttpStatusCode.Unauthorized, "application/ipp", response);
		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true));

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert — reported by its HTTP status, as an HTML refusal would be
		PrintAuthenticationException exception = await Assert.ThrowsAsync<PrintAuthenticationException>(request);
		Assert.Equal(401, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenIppNotAuthorizedStatusWhenRequestSentThenThrowsPrintExceptionSayingTheUserIsNotAllowed()
	{
		// Arrange — a printer answering with IPP's own client-error-not-authorized (0x0403)
		byte[] response = SharpIppClientAdapterTests.CreateIppResponse(0x0403);
		StubHttpMessageHandler stub = new StubHttpMessageHandler(HttpStatusCode.OK, "application/ipp", response);
		using SharpIppClientAdapter client = new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true));

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert — the user is known but not allowed, so asking for other credentials wouldn't help
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.IsNotType<PrintAuthenticationException>(exception);
		Assert.Contains("does not allow this user", exception.Message, StringComparison.Ordinal);
		Assert.Equal(0x0403, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenRequestRefusedWithIppRequestExceptionWhenSentThenThrowsPrintExceptionWithItsStatus()
	{
		// Arrange — SharpIppNext's exception for a request refused before or while it was sent
		IppRequestException refusal = new IppRequestException
		(
			"Refused.",
			new IppRequestMessage(),
			IppStatusCode.ClientErrorDocumentFormatNotSupported
		);

		using SharpIppClientAdapter client = SharpIppClientAdapterTests.CreateFailingClient(refusal);

		// Act
		Func<Task> request = () => SharpIppClientAdapterTests.GetAttributesAsync(client);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(request);
		Assert.Equal((int)IppStatusCode.ClientErrorDocumentFormatNotSupported, exception.NativeErrorCode);
		Assert.Same(refusal, exception.InnerException);
	}

	/// <summary>
	/// Creates an adapter whose every request is answered with an HTML page and the given status, as CUPS refuses
	/// requests.
	/// </summary>
	/// <param name="statusCode">The HTTP status.</param>
	/// <param name="sendsCredentials">Whether the adapter is told credentials are sent.</param>
	/// <returns>The adapter. The caller disposes it.</returns>
	private static SharpIppClientAdapter CreateHtmlClient(HttpStatusCode statusCode, bool sendsCredentials)
	{
		byte[] page = Encoding.UTF8.GetBytes("<!DOCTYPE HTML><html><body>Refused</body></html>");
		StubHttpMessageHandler stub = new StubHttpMessageHandler(statusCode, "text/html", page);

		return new SharpIppClientAdapter(new HttpClient(stub, disposeHandler: true), sendsCredentials);
	}

	/// <summary>
	/// Creates an IPP response with the given status and only the required operation attributes.
	/// </summary>
	/// <param name="statusCode">The IPP status code.</param>
	/// <returns>The encoded response.</returns>
	private static byte[] CreateIppResponse(short statusCode)
	{
		return SharpIppClientAdapterTests.CreateIppResponse(statusCode, printerNameTag: 0, printerName: null);
	}

	/// <summary>
	/// Creates an IPP response with the given status, the required operation attributes and, optionally, a printer
	/// attributes group holding <c>printer-name</c>, as CUPS answers CUPS-Get-Default.
	/// </summary>
	/// <param name="statusCode">The IPP status code.</param>
	/// <param name="printerNameTag">The value tag of <c>printer-name</c>.</param>
	/// <param name="printerName">The encoded value of <c>printer-name</c>, or <see langword="null"/> for no printer
	/// attributes group.</param>
	/// <returns>The encoded response.</returns>
	private static byte[] CreateIppResponse(short statusCode, byte printerNameTag, byte[]? printerName)
	{
		using MemoryStream stream = new MemoryStream();
		using BinaryWriter writer = new BinaryWriter(stream);

		// Version 1.1, status, request ID 1
		writer.Write(new byte[] { 1, 1 });
		SharpIppClientAdapterTests.WriteBigEndian(writer, statusCode);
		writer.Write(new byte[] { 0, 0, 0, 1 });

		// Operation attributes: charset and natural language
		writer.Write((byte)0x01);
		SharpIppClientAdapterTests.WriteAttribute(writer, 0x47, "attributes-charset", "utf-8");
		SharpIppClientAdapterTests.WriteAttribute(writer, 0x48, "attributes-natural-language", "en");

		// Printer attributes, if any, then the end tag
		if (printerName is not null)
		{
			writer.Write((byte)0x04);
			SharpIppClientAdapterTests.WriteAttribute(writer, printerNameTag, "printer-name", printerName);
		}

		writer.Write((byte)0x03);
		writer.Flush();

		return stream.ToArray();
	}

	/// <summary>
	/// Encodes a text value with its language, as IPP's <c>nameWithLanguage</c> and <c>textWithLanguage</c> carry it.
	/// </summary>
	/// <param name="language">The natural language.</param>
	/// <param name="text">The text.</param>
	/// <returns>The encoded value.</returns>
	private static byte[] EncodeWithLanguage(string language, string text)
	{
		using MemoryStream stream = new MemoryStream();
		using BinaryWriter writer = new BinaryWriter(stream);

		SharpIppClientAdapterTests.WriteBigEndian(writer, (short)language.Length);
		writer.Write(Encoding.ASCII.GetBytes(language));
		SharpIppClientAdapterTests.WriteBigEndian(writer, (short)text.Length);
		writer.Write(Encoding.ASCII.GetBytes(text));
		writer.Flush();

		return stream.ToArray();
	}

	/// <summary>
	/// Writes one IPP attribute with a single text value.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="tag">The value tag.</param>
	/// <param name="name">The attribute name.</param>
	/// <param name="value">The value.</param>
	private static void WriteAttribute(BinaryWriter writer, byte tag, string name, string value)
	{
		SharpIppClientAdapterTests.WriteAttribute(writer, tag, name, Encoding.ASCII.GetBytes(value));
	}

	/// <summary>
	/// Writes one IPP attribute with a single encoded value.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="tag">The value tag.</param>
	/// <param name="name">The attribute name.</param>
	/// <param name="value">The encoded value.</param>
	private static void WriteAttribute(BinaryWriter writer, byte tag, string name, byte[] value)
	{
		writer.Write(tag);
		SharpIppClientAdapterTests.WriteBigEndian(writer, (short)name.Length);
		writer.Write(Encoding.ASCII.GetBytes(name));
		SharpIppClientAdapterTests.WriteBigEndian(writer, (short)value.Length);
		writer.Write(value);
	}

	/// <summary>
	/// Writes a 16-bit value in network byte order, as IPP encodes them.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="value">The value.</param>
	private static void WriteBigEndian(BinaryWriter writer, short value)
	{
		writer.Write((byte)((value >> 8) & 0xFF));
		writer.Write((byte)(value & 0xFF));
	}

	/// <summary>
	/// Creates an adapter whose every request fails with the given exception.
	/// </summary>
	/// <param name="failure">The exception the HTTP handler throws.</param>
	/// <returns>The adapter. The caller disposes it.</returns>
	private static SharpIppClientAdapter CreateFailingClient(Exception failure)
	{
		HttpClient httpClient = new HttpClient(new FailingHttpMessageHandler(failure), disposeHandler: true);

		return new SharpIppClientAdapter(httpClient);
	}

	/// <summary>
	/// Asks the printer for its attributes through the adapter.
	/// </summary>
	/// <param name="client">The adapter.</param>
	/// <returns>The printer's attributes, if the request succeeds.</returns>
	private static Task<PrinterDescriptionAttributes?> GetAttributesAsync(SharpIppClientAdapter client)
	{
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();

		return IppPrinterQueries.GetAttributesAsync
		(
			context,
			SharpIppClientAdapterTests.PrinterUri,
			new[] { "printer-name" },
			TestContext.Current.CancellationToken
		);
	}
}
