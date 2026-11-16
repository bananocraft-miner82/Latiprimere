using System.Net;
using System.Net.Http.Headers;

namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// An HTTP handler that answers every request with the same status and body, and records what was sent.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
	private readonly HttpStatusCode statusCode; // The status of every response
	private readonly string mediaType;          // The Content-Type of every response
	private readonly byte[] body;               // The body of every response
	private readonly TimeSpan delay;            // How long to wait before answering

	/// <summary>
	/// Initialises a new instance of the <see cref="StubHttpMessageHandler"/> class.
	/// </summary>
	/// <param name="statusCode">The status of every response.</param>
	/// <param name="mediaType">The Content-Type of every response.</param>
	/// <param name="body">The body of every response.</param>
	/// <param name="delay">How long to wait before answering, as a slow printer does; the request's
	/// cancellation ends the wait.</param>
	public StubHttpMessageHandler(HttpStatusCode statusCode, string mediaType, byte[] body, TimeSpan delay = default)
	{
		this.statusCode = statusCode;
		this.mediaType = mediaType;
		this.body = body;
		this.delay = delay;
	}

	/// <summary>Gets the URI of the last request, or <see langword="null"/> before the first.</summary>
	public Uri? LastRequestUri { get; private set; }

	/// <summary>Gets the Authorization header of the last request, or <see langword="null"/> if it had none.</summary>
	public AuthenticationHeaderValue? LastAuthorization { get; private set; }

	/// <summary>Gets the body of the last request, or <see langword="null"/> before the first.</summary>
	public byte[]? LastRequestBody { get; private set; }

	/// <summary>
	/// Records the request and answers it.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">Ends the delay, if there is one.</param>
	/// <returns>The response.</returns>
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		this.LastRequestUri = request.RequestUri;
		this.LastAuthorization = request.Headers.Authorization;
		this.LastRequestBody = request.Content is null
			? null
			: await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

		if (this.delay > TimeSpan.Zero)
		{
			await Task.Delay(this.delay, cancellationToken).ConfigureAwait(false);
		}

		ByteArrayContent content = new ByteArrayContent(this.body);
		content.Headers.ContentType = new MediaTypeHeaderValue(this.mediaType);

		return new HttpResponseMessage(this.statusCode) { Content = content };
	}
}
