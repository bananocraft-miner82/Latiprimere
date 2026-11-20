using System.Net;
using System.Net.Http.Headers;
using Latiprimere.Ipp.Connections;
using Latiprimere.Ipp.Tests.Fakes;
using Xunit;

namespace Latiprimere.Ipp.Tests.Connections;

public sealed class IppStallGuardHandlerTests
{
	private static readonly Uri PrinterUri = new Uri("http://192.0.2.10:631/ipp/print"); // Never contacted
	private static readonly TimeSpan ShortStall = TimeSpan.FromMilliseconds(200);     // Runs out quickly
	private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);                // Ends a test whose stall is missed

	[Fact]
	public async Task GivenPrinterStopsReadingWhenSentThenThrowsStallWhileSending()
	{
		// Arrange — a printer that takes the first kilobyte, then reads nothing more
		StalledWriteStream printer = new StalledWriteStream(capacity: 1024);
		using HttpMessageInvoker invoker = IppStallGuardHandlerTests.CreateInvoker
		(
			IppStallGuardHandlerTests.ShortStall,
			async (request, token) =>
			{
				await request.Content!.CopyToAsync(printer, token);

				return new HttpResponseMessage(HttpStatusCode.OK);
			}
		);

		using HttpRequestMessage request = IppStallGuardHandlerTests.CreateRequest(new ByteArrayContent(new byte[64 * 1024]));
		using CancellationTokenSource guard = IppStallGuardHandlerTests.CreateGuard();

		// Act
		Func<Task> send = () => invoker.SendAsync(request, guard.Token);

		// Assert
		IppTransferStalledException exception = await Assert.ThrowsAsync<IppTransferStalledException>(send);
		Assert.False(exception.IsAwaitingResponse);
		Assert.Equal(IppStallGuardHandlerTests.ShortStall, exception.StallTimeout);
		Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
		Assert.False(guard.IsCancellationRequested);
	}

	[Fact]
	public async Task GivenNoAnswerOnceBodySentWhenSentThenThrowsStallAwaitingResponse()
	{
		// Arrange — a printer that reads the whole body, then never answers
		using MemoryStream received = new MemoryStream();
		using HttpMessageInvoker invoker = IppStallGuardHandlerTests.CreateInvoker
		(
			IppStallGuardHandlerTests.ShortStall,
			async (request, token) =>
			{
				await request.Content!.CopyToAsync(received, token);
				await Task.Delay(Timeout.InfiniteTimeSpan, token);

				return new HttpResponseMessage(HttpStatusCode.OK);
			}
		);

		using HttpRequestMessage request = IppStallGuardHandlerTests.CreateRequest(new ByteArrayContent(new byte[1000]));
		using CancellationTokenSource guard = IppStallGuardHandlerTests.CreateGuard();

		// Act
		Func<Task> send = () => invoker.SendAsync(request, guard.Token);

		// Assert
		IppTransferStalledException exception = await Assert.ThrowsAsync<IppTransferStalledException>(send);
		Assert.True(exception.IsAwaitingResponse);
		Assert.Equal(1000, received.Length);
		Assert.False(guard.IsCancellationRequested);
	}

	[Fact]
	public async Task GivenPausesBetweenWritesLongerThanTimeoutWhenSentThenSucceeds()
	{
		// Arrange — a body written slowly, as pages render, to a printer that reads each write at once
		using MemoryStream received = new MemoryStream();
		using HttpMessageInvoker invoker = IppStallGuardHandlerTests.CreateInvoker
		(
			IppStallGuardHandlerTests.ShortStall,
			async (request, token) =>
			{
				await request.Content!.CopyToAsync(received, token);

				return new HttpResponseMessage(HttpStatusCode.OK);
			}
		);

		PausingContent content = new PausingContent(chunks: 3, chunkSize: 1000, pause: IppStallGuardHandlerTests.ShortStall * 3);
		using HttpRequestMessage request = IppStallGuardHandlerTests.CreateRequest(content);
		using CancellationTokenSource guard = IppStallGuardHandlerTests.CreateGuard();

		// Act
		using HttpResponseMessage response = await invoker.SendAsync(request, guard.Token);

		// Assert — the time spent between writes isn't a stall
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(3000, received.Length);
	}

	[Fact]
	public async Task GivenCallerCancelsWhileStalledWhenSentThenCancellationPassesThrough()
	{
		// Arrange — a stall timeout far off, and a caller who gives up first
		StalledWriteStream printer = new StalledWriteStream(capacity: 0);
		using HttpMessageInvoker invoker = IppStallGuardHandlerTests.CreateInvoker
		(
			TimeSpan.FromHours(1),
			async (request, token) =>
			{
				await request.Content!.CopyToAsync(printer, token);

				return new HttpResponseMessage(HttpStatusCode.OK);
			}
		);

		using HttpRequestMessage request = IppStallGuardHandlerTests.CreateRequest(new ByteArrayContent(new byte[10]));
		using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		caller.CancelAfter(TimeSpan.FromMilliseconds(100));

		// Act
		Func<Task> send = () => invoker.SendAsync(request, caller.Token);

		// Assert
		OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(send);
		Assert.IsNotType<IppTransferStalledException>(exception.InnerException);
	}

	[Fact]
	public async Task GivenOtherFailureWhenSentThenPassesThroughUnchanged()
	{
		// Arrange — a connection that fails while nothing is being timed
		HttpRequestException failure = new HttpRequestException(HttpRequestError.ConnectionError, "Refused.");
		using HttpMessageInvoker invoker = IppStallGuardHandlerTests.CreateInvoker
		(
			IppStallGuardHandlerTests.ShortStall,
			(request, token) => Task.FromException<HttpResponseMessage>(failure)
		);

		using HttpRequestMessage request = IppStallGuardHandlerTests.CreateRequest(new ByteArrayContent(new byte[10]));

		// Act
		Func<Task> send = () => invoker.SendAsync(request, TestContext.Current.CancellationToken);

		// Assert
		HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(send);
		Assert.Same(failure, exception);
	}

	[Fact]
	public async Task GivenBodyWithHeadersWhenSentThenHandlerBelowSeesTheSameHeaders()
	{
		// Arrange
		MediaTypeHeaderValue? contentType = null;
		long? contentLength = null;
		using HttpMessageInvoker invoker = IppStallGuardHandlerTests.CreateInvoker
		(
			IppStallGuardHandlerTests.ShortStall,
			(request, token) =>
			{
				contentType = request.Content!.Headers.ContentType;
				contentLength = request.Content.Headers.ContentLength;

				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			}
		);

		ByteArrayContent body = new ByteArrayContent(new byte[42]);
		body.Headers.ContentType = new MediaTypeHeaderValue("application/ipp");
		using HttpRequestMessage request = IppStallGuardHandlerTests.CreateRequest(body);

		// Act
		using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("application/ipp", contentType?.MediaType);
		Assert.Equal(42, contentLength);
	}

	[Fact]
	public async Task GivenRequestWithoutBodyWhenSentThenSentAsItIs()
	{
		// Arrange
		bool hadContent = true;
		using HttpMessageInvoker invoker = IppStallGuardHandlerTests.CreateInvoker
		(
			IppStallGuardHandlerTests.ShortStall,
			(request, token) =>
			{
				hadContent = request.Content is not null;

				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
			}
		);

		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, IppStallGuardHandlerTests.PrinterUri);

		// Act
		using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.False(hadContent);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void GivenTimeoutNotPositiveWhenConstructedThenThrowsArgumentOutOfRangeException(int milliseconds)
	{
		// Arrange — zero, or infinite (provided by InlineData above), which the factory handles by leaving the guard out
		TimeSpan stallTimeout = TimeSpan.FromMilliseconds(milliseconds);
		using HttpMessageHandler inner = new DelegateHttpMessageHandler((request, token) => Task.FromResult(new HttpResponseMessage()));

		// Act
		Func<IppStallGuardHandler> create = () => new IppStallGuardHandler(stallTimeout, inner);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(create);
	}

	/// <summary>
	/// Creates an invoker sending through a stall guard to a handler that answers as the test says.
	/// </summary>
	/// <param name="stallTimeout">The guard's stall timeout.</param>
	/// <param name="answer">Answers each request.</param>
	/// <returns>The invoker, which owns the handlers.</returns>
	private static HttpMessageInvoker CreateInvoker
	(
		TimeSpan stallTimeout,
		Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer
	)
	{
		IppStallGuardHandler handler = new IppStallGuardHandler(stallTimeout, new DelegateHttpMessageHandler(answer));

		return new HttpMessageInvoker(handler, disposeHandler: true);
	}

	/// <summary>
	/// Creates a POST to the printer with the given body.
	/// </summary>
	/// <param name="content">The body.</param>
	/// <returns>The request.</returns>
	private static HttpRequestMessage CreateRequest(HttpContent content)
	{
		return new HttpRequestMessage(HttpMethod.Post, IppStallGuardHandlerTests.PrinterUri)
		{
			Content = content
		};
	}

	/// <summary>
	/// Creates a token that ends the test with a cancellation, rather than letting it hang, if the stall is missed.
	/// </summary>
	/// <returns>The token's source.</returns>
	private static CancellationTokenSource CreateGuard()
	{
		CancellationTokenSource guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		guard.CancelAfter(IppStallGuardHandlerTests.Guard);

		return guard;
	}
}
