namespace Latiprimere.Testing.Fakes;

/// <summary>
/// A readable stream that can't seek, like a network or pipe stream.
/// </summary>
public sealed class ForwardOnlyStream : Stream
{
	private readonly MemoryStream inner; // The content, read from front to back

	/// <summary>
	/// Initialises a new instance of the <see cref="ForwardOnlyStream"/> class.
	/// </summary>
	/// <param name="content">The bytes the stream returns.</param>
	public ForwardOnlyStream(byte[] content)
	{
		this.inner = new MemoryStream(content);
	}

	/// <inheritdoc/>
	public override bool CanRead
	{
		get
		{
			return true;
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
			return false;
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
	public override int Read(byte[] buffer, int offset, int count)
	{
		return this.inner.Read(buffer, offset, count);
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
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			this.inner.Dispose();
		}

		base.Dispose(disposing);
	}
}
