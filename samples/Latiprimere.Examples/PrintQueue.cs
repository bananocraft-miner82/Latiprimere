using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;

namespace LatiprimereExamples;

/// <summary>
/// Holds print requests between the web endpoint that accepts them and the worker that prints them. Part of
/// "Printing from a web app or background service".
/// </summary>
internal sealed class PrintQueue
{
	/// <summary>
	/// The most requests that can wait at once. Beyond this, new requests are turned away.
	/// </summary>
	private const int Capacity = 100;

	private readonly Channel<PrintRequest> channel = Channel.CreateBounded<PrintRequest>(PrintQueue.Capacity); // Requests waiting to print

	/// <summary>
	/// Adds a request to the queue, unless it is full.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns><see langword="true"/> if the request was queued; <see langword="false"/> if the queue is full.</returns>
	public bool TryEnqueue(PrintRequest request)
	{
		return this.channel.Writer.TryWrite(request);
	}

	/// <summary>
	/// Reads requests as they arrive, until <paramref name="cancellationToken"/> is cancelled.
	/// </summary>
	/// <param name="cancellationToken">A token to stop reading.</param>
	/// <returns>The requests, in the order they were queued.</returns>
	public IAsyncEnumerable<PrintRequest> ReadAllAsync(CancellationToken cancellationToken)
	{
		return this.channel.Reader.ReadAllAsync(cancellationToken);
	}
}
