namespace Latiprimere.Ipp.Connections;

/// <summary>
/// The stream a request's body is written to, timing each write and flush with an <see cref="IppStallWatch"/>.
/// </summary>
/// <remarks>
/// Large writes are passed on in pieces of at most <see cref="MaxWriteSize"/>, each timed on its own. Windows reports a
/// single large write to a socket as finished as soon as it has queued it, even while the other end reads nothing:
/// a 64 MB document, written at once as SharpIppNext writes a seekable stream, was "sent" in 24 ms to a printer that
/// had stopped reading. In pieces, each finished write means the network took it, so the timing follows the printer.
/// </remarks>
internal sealed class StallGuardedStream : Stream
{
	/// <summary>
	/// The most passed on in one write: 64 KiB, small enough that a write waits once the socket's buffers are full.
	/// </summary>
	internal const int MaxWriteSize = 64 * 1024;

	private readonly Stream inner;        // The connection's request stream, not owned
	private readonly IppStallWatch watch; // Times each write

	/// <summary>
	/// Initialises a new instance of the <see cref="StallGuardedStream"/> class.
	/// </summary>
	/// <param name="inner">The stream to write to. It isn't disposed with this one.</param>
	/// <param name="watch">The watch that times each write.</param>
	/// <exception cref="ArgumentNullException"><paramref name="inner"/> or <paramref name="watch"/> is
	/// <see langword="null"/>.</exception>
	public StallGuardedStream(Stream inner, IppStallWatch watch)
	{
		ArgumentNullException.ThrowIfNull(inner);
		ArgumentNullException.ThrowIfNull(watch);

		this.inner = inner;
		this.watch = watch;
	}

	/// <inheritdoc/>
	public override bool CanRead
	{
		get
		{
			return false;
		}
	}

	/// <inheritdoc/>
	public override bool CanSeek
	{
		get
		{
			return false;
		}
	}

	/// <inheritdoc/>
	public override bool CanWrite
	{
		get
		{
			return true;
		}
	}

	/// <inheritdoc/>
	public override long Length
	{
		get
		{
			throw new NotSupportedException();
		}
	}

	/// <inheritdoc/>
	public override long Position
	{
		get
		{
			throw new NotSupportedException();
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		this.Write(buffer.AsSpan(offset, count));
	}

	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		while (!buffer.IsEmpty)
		{
			ReadOnlySpan<byte> piece = buffer[..Math.Min(buffer.Length, StallGuardedStream.MaxWriteSize)];
			this.watch.StartWrite();

			try
			{
				this.inner.Write(piece);
			}
			finally
			{
				this.watch.EndWrite();
			}

			buffer = buffer[piece.Length..];
		}
	}

	/// <inheritdoc/>
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		return this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
	}

	/// <inheritdoc/>
	public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		while (!buffer.IsEmpty)
		{
			ReadOnlyMemory<byte> piece = buffer[..Math.Min(buffer.Length, StallGuardedStream.MaxWriteSize)];
			this.watch.StartWrite();

			try
			{
				await this.inner.WriteAsync(piece, cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				this.watch.EndWrite();
			}

			buffer = buffer[piece.Length..];
		}
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		this.watch.StartWrite();

		try
		{
			this.inner.Flush();
		}
		finally
		{
			this.watch.EndWrite();
		}
	}

	/// <inheritdoc/>
	public override async Task FlushAsync(CancellationToken cancellationToken)
	{
		this.watch.StartWrite();

		try
		{
			await this.inner.FlushAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			this.watch.EndWrite();
		}
	}

	/// <inheritdoc/>
	public override int Read(byte[] buffer, int offset, int count)
	{
		throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override long Seek(long offset, SeekOrigin origin)
	{
		throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void SetLength(long value)
	{
		throw new NotSupportedException();
	}
}
