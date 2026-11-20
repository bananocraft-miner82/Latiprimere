namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// A stream that can only be written to, front to back, as a pipe or an HTTP request's body, passing what it is given
/// to another stream it doesn't own.
/// </summary>
internal sealed class WriteOnlyStream : Stream
{
	private readonly Stream inner; // Where the bytes go, not owned

	/// <summary>
	/// Initialises a new instance of the <see cref="WriteOnlyStream"/> class.
	/// </summary>
	/// <param name="inner">Where the bytes go.</param>
	public WriteOnlyStream(Stream inner)
	{
		this.inner = inner;
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
		this.inner.Write(buffer, offset, count);
	}

	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		this.inner.Write(buffer);
	}

	/// <inheritdoc/>
	public override void Flush()
	{
		this.inner.Flush();
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
