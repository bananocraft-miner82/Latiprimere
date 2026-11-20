namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// An HTTP handler that answers each request as a test's function decides, such as by reading the body slowly or
/// never answering.
/// </summary>
internal sealed class DelegateHttpMessageHandler : HttpMessageHandler
{
	private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer; // Answers each request

	/// <summary>
	/// Initialises a new instance of the <see cref="DelegateHttpMessageHandler"/> class.
	/// </summary>
	/// <param name="answer">Answers each request, given the request and the token it was sent with.</param>
	public DelegateHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> answer)
	{
		this.answer = answer;
	}

	/// <inheritdoc/>
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		return this.answer(request, cancellationToken);
	}
}
