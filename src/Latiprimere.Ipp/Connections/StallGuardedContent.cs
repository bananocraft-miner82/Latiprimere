using System.Net;

namespace Latiprimere.Ipp.Connections;

/// <summary>
/// Wraps a request's body so an <see cref="IppStallWatch"/> times each write of it, and the wait for the answer once
/// it is all sent.
/// </summary>
internal sealed class StallGuardedContent : HttpContent
{
	private readonly HttpContent inner;   // The body, owned and disposed with this content
	private readonly IppStallWatch watch; // Times the writes and the wait

	/// <summary>
	/// Initialises a new instance of the <see cref="StallGuardedContent"/> class, with the body's headers.
	/// </summary>
	/// <param name="inner">The body. This content owns it.</param>
	/// <param name="watch">The watch that times the writes and the wait.</param>
	/// <exception cref="ArgumentNullException"><paramref name="inner"/> or <paramref name="watch"/> is
	/// <see langword="null"/>.</exception>
	public StallGuardedContent(HttpContent inner, IppStallWatch watch)
	{
		ArgumentNullException.ThrowIfNull(inner);
		ArgumentNullException.ThrowIfNull(watch);

		this.inner = inner;
		this.watch = watch;

		// Content-Type and the rest; the length, when the body knows it, comes from TryComputeLength
		foreach (KeyValuePair<string, IEnumerable<string>> header in inner.Headers)
		{
			this.Headers.TryAddWithoutValidation(header.Key, header.Value);
		}
	}

	/// <inheritdoc/>
	protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
	{
		return this.SerializeToStreamAsync(stream, context, CancellationToken.None);
	}

	/// <inheritdoc/>
	protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
	{
		StallGuardedStream guarded = new StallGuardedStream(stream, this.watch);

		await this.inner.CopyToAsync(guarded, context, cancellationToken).ConfigureAwait(false);

		// Flushed here, so a printer that stops reading the last of it counts as a stalled upload, not a late answer
		await guarded.FlushAsync(cancellationToken).ConfigureAwait(false);

		this.watch.EndBody();
	}

	/// <inheritdoc/>
	protected override bool TryComputeLength(out long length)
	{
		long? innerLength = this.inner.Headers.ContentLength;
		length = innerLength ?? 0;

		return innerLength is not null;
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			this.inner.Dispose();
		}

		base.Dispose(disposing);
	}
}
