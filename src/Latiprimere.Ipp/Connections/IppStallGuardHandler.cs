namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Cancels a request whose printer stops taking part: it stops reading the body, or doesn't answer once the body is
/// sent, for longer than the stall timeout. Without it, a printer that stalls partway through an upload holds a
/// Print-Job forever, as the HTTP client has no overall timeout (see <see cref="IppHttpClientFactory"/>).
/// </summary>
/// <remarks>
/// It limits a lack of progress, not the whole request, so a large job to a printer that reads it only as fast as it
/// prints is never cut short while the printer keeps reading.
/// </remarks>
internal sealed class IppStallGuardHandler : DelegatingHandler
{
	private readonly TimeSpan stallTimeout; // The longest a write, or the wait for an answer, may take

	/// <summary>
	/// Initialises a new instance of the <see cref="IppStallGuardHandler"/> class.
	/// </summary>
	/// <param name="stallTimeout">The longest a write of a request's body, or the wait for the answer once it is sent,
	/// may take.</param>
	/// <param name="innerHandler">The handler that sends the requests.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="stallTimeout"/> is not positive, or is longer than
	/// about 24 days.</exception>
	/// <exception cref="ArgumentNullException"><paramref name="innerHandler"/> is <see langword="null"/>.</exception>
	public IppStallGuardHandler(TimeSpan stallTimeout, HttpMessageHandler innerHandler)
		: base(innerHandler)
	{
		IppStallWatch.ValidateStallTimeout(stallTimeout);
		ArgumentNullException.ThrowIfNull(innerHandler);

		this.stallTimeout = stallTimeout;
	}

	/// <summary>
	/// Sends the request, cancelling it if the printer stalls.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The response.</returns>
	/// <exception cref="IppTransferStalledException">The printer stopped reading the body, or didn't answer once it
	/// was sent, for longer than the stall timeout.</exception>
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (request.Content is null)
		{
			return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
		}

		using IppStallWatch watch = new IppStallWatch(this.stallTimeout, cancellationToken);
		request.Content = new StallGuardedContent(request.Content, watch);

		try
		{
			return await base.SendAsync(request, watch.Token).ConfigureAwait(false);
		}
		catch (Exception exception) when (watch.HasStalled)
		{
			// However the cancelled connection failed, the cause was the stall; the caller's own cancellation and
			// every other failure pass through unchanged
			throw new IppTransferStalledException(watch.IsAwaitingResponse, this.stallTimeout, exception);
		}
	}
}
