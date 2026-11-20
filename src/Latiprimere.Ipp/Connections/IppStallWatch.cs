namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Watches one request for a printer that stops taking part: each write of the request's body must finish within the
/// stall timeout, and so must the wait for an answer once the whole body is sent. Otherwise the request is cancelled.
/// </summary>
/// <remarks>
/// The time between writes isn't counted, so a document rendered page by page as it is sent (ADR 0035) may take as
/// long as it needs to render. A write that doesn't finish means the printer has stopped reading what it was sent.
/// </remarks>
internal sealed class IppStallWatch : IDisposable
{
	private readonly CancellationTokenSource source; // Cancels the request, for the caller or on a stall
	private readonly CancellationToken callerToken;  // The caller's own token
	private readonly Lock gate = new Lock();         // Keeps the timer from being set once the source is disposed
	private volatile bool isAwaitingResponse;        // Backs IsAwaitingResponse
	private bool isDisposed;                         // Whether the source is disposed

	/// <summary>
	/// Initialises a new instance of the <see cref="IppStallWatch"/> class.
	/// </summary>
	/// <param name="stallTimeout">The longest a write, or the wait for an answer, may take.</param>
	/// <param name="callerToken">The caller's token, which cancels the request as well.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="stallTimeout"/> is not positive, or is longer than
	/// about 24 days.</exception>
	public IppStallWatch(TimeSpan stallTimeout, CancellationToken callerToken)
	{
		IppStallWatch.ValidateStallTimeout(stallTimeout);

		this.StallTimeout = stallTimeout;
		this.callerToken  = callerToken;
		this.source       = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
	}

	/// <summary>
	/// Gets the longest a write, or the wait for an answer, may take.
	/// </summary>
	public TimeSpan StallTimeout { get; }

	/// <summary>
	/// Gets the token that cancels the request, when the caller cancels or the printer stalls.
	/// </summary>
	public CancellationToken Token
	{
		get
		{
			return this.source.Token;
		}
	}

	/// <summary>
	/// Gets a value indicating whether the whole body has been sent, so what is being waited for is the answer.
	/// </summary>
	public bool IsAwaitingResponse
	{
		get
		{
			return this.isAwaitingResponse;
		}
	}

	/// <summary>
	/// Gets a value indicating whether the request was cancelled because the printer stalled, rather than by the
	/// caller.
	/// </summary>
	public bool HasStalled
	{
		get
		{
			// Only the timer cancels the source without the caller's token being cancelled first
			return this.source.IsCancellationRequested && !this.callerToken.IsCancellationRequested;
		}
	}

	/// <summary>
	/// Checks a stall timeout is one the watch accepts.
	/// </summary>
	/// <param name="stallTimeout">The timeout to check.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="stallTimeout"/> is not positive, or is longer than
	/// about 24 days.</exception>
	internal static void ValidateStallTimeout(TimeSpan stallTimeout)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(stallTimeout, TimeSpan.Zero);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(stallTimeout, TimeSpan.FromMilliseconds(int.MaxValue));
	}

	/// <summary>
	/// Starts timing a write.
	/// </summary>
	public void StartWrite()
	{
		this.SetTimer(this.StallTimeout);
	}

	/// <summary>
	/// Stops timing a write that has finished.
	/// </summary>
	public void EndWrite()
	{
		this.SetTimer(Timeout.InfiniteTimeSpan);
	}

	/// <summary>
	/// Records that the whole body is sent, and starts timing the wait for the answer.
	/// </summary>
	public void EndBody()
	{
		this.isAwaitingResponse = true;
		this.SetTimer(this.StallTimeout);
	}

	/// <summary>
	/// Stops the timer and releases it.
	/// </summary>
	public void Dispose()
	{
		lock (this.gate)
		{
			if (this.isDisposed)
			{
				return;
			}

			this.isDisposed = true;
			this.source.Dispose();
		}
	}

	/// <summary>
	/// Sets when the request is cancelled, unless it already has been or the watch is disposed: a write may still be
	/// finishing when the request ends, as when a printer answers before reading the whole body.
	/// </summary>
	/// <param name="delay">The time from now, or <see cref="Timeout.InfiniteTimeSpan"/> for never.</param>
	private void SetTimer(TimeSpan delay)
	{
		lock (this.gate)
		{
			if (!this.isDisposed && !this.source.IsCancellationRequested)
			{
				this.source.CancelAfter(delay);
			}
		}
	}
}
