namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// An HTTP handler whose every request fails with a given exception, as a real handler does when it can't connect.
/// </summary>
internal sealed class FailingHttpMessageHandler : HttpMessageHandler
{
	private readonly Exception failure; // Thrown for every request

	/// <summary>
	/// Initialises a new instance of the <see cref="FailingHttpMessageHandler"/> class.
	/// </summary>
	/// <param name="failure">The exception every request throws.</param>
	public FailingHttpMessageHandler(Exception failure)
	{
		this.failure = failure;
	}

	/// <summary>
	/// Fails the request.
	/// </summary>
	/// <param name="request">Not used.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>Never returns.</returns>
	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		return Task.FromException<HttpResponseMessage>(this.failure);
	}
}
