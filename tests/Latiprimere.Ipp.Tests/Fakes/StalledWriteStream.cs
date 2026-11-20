namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// A stream that takes a set number of bytes, then stops: every later write waits until it is cancelled, as writes
/// to a printer that has stopped reading do once the network's buffers are full.
/// </summary>
internal sealed class StalledWriteStream : Stream
{
	private readonly long capacity; // The bytes taken before writes stop finishing
	private long written;           // The bytes taken so far

	/// <summary>
	/// Initialises a new instance of the <see cref="StalledWriteStream"/> class.
	/// </summary>
	/// <param name="capacity">The bytes taken before writes stop finishing.</param>
	public StalledWriteStream(long capacity)
	{
		this.capacity = capacity;
	}

	/// <summary>Gets the bytes taken so far.</summary>
	public long Written
	{
		get
		{
			return Interlocked.Read(ref this.written);
		}
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
	public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
	{
		if (this.Written + buffer.Length > this.capacity)
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
		}

		Interlocked.Add(ref this.written, buffer.Length);
	}

	/// <inheritdoc/>
	public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		return this.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
	}

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public override void Flush()
	{
	}

	/// <inheritdoc/>
	public override Task FlushAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
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
