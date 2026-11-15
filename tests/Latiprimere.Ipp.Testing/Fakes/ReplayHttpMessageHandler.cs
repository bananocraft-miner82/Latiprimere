using System.Net;
using System.Net.Http.Headers;

namespace Latiprimere.Ipp.Testing.Fakes;

/// <summary>
/// An HTTP handler that answers every request with the same recorded IPP response, so tests can run captured
/// printer responses through the real IPP client and decoder.
/// </summary>
public sealed class ReplayHttpMessageHandler : HttpMessageHandler
{
	private readonly byte[] responseBody; // The recorded IPP response

	/// <summary>
	/// Initialises a new instance of the <see cref="ReplayHttpMessageHandler"/> class.
	/// </summary>
	/// <param name="responseBody">The recorded IPP response.</param>
	public ReplayHttpMessageHandler(byte[] responseBody)
	{
		this.responseBody = responseBody;
	}

	/// <summary>Gets the number of requests answered.</summary>
	public int RequestCount { get; private set; }

	/// <summary>
	/// Answers with the recorded response.
	/// </summary>
	/// <param name="request">The request, which is counted.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>A 200 response with the recorded IPP body.</returns>
	protected override Task<HttpResponseMessage> SendAsync
	(
		HttpRequestMessage request,
		CancellationToken  cancellationToken
	)
	{
		this.RequestCount++;
		ByteArrayContent content = new ByteArrayContent(this.responseBody);
		content.Headers.ContentType = new MediaTypeHeaderValue("application/ipp");

		return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
	}
}
