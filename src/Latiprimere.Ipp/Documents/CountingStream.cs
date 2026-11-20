namespace Latiprimere.Ipp.Documents;

/// <summary>
/// A write-only stream that passes what it is given to another and counts the bytes, so a PDF's offsets are known
/// without the output having to seek, as a pipe or an HTTP request can't. It never disposes the other stream.
/// </summary>
internal sealed class CountingStream : Stream
{
	private readonly Stream inner; // Where the bytes go, not owned

	/// <summary>
	/// Initialises a new instance of the <see cref="CountingStream"/> class.
	/// </summary>
	/// <param name="inner">Where the bytes go. It isn't disposed with this stream.</param>
	/// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
	public CountingStream(Stream inner)
	{
		ArgumentNullException.ThrowIfNull(inner);

		this.inner = inner;
	}

	/// <summary>
	/// Gets how many bytes have been written.
	/// </summary>
	public long BytesWritten { get; private set; }

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
		this.inner.Write(buffer);
		this.BytesWritten += buffer.Length;
	}

	/// <inheritdoc/>
	public override void WriteByte(byte value)
	{
		this.inner.WriteByte(value);
		this.BytesWritten++;
	}

	/// <inheritdoc/>
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		return this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
	}

	/// <inheritdoc/>
	public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		await this.inner.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
		this.BytesWritten += buffer.Length;
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		this.inner.Flush();
	}

	/// <inheritdoc/>
	public override Task FlushAsync(CancellationToken cancellationToken)
	{
		return this.inner.FlushAsync(cancellationToken);
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
