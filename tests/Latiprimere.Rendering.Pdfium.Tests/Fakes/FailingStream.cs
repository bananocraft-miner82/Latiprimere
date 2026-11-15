namespace Latiprimere.Rendering.Pdfium.Tests.Fakes;

/// <summary>
/// A stream that can't seek, returns some data, then fails as a broken connection or disk would.
/// </summary>
internal sealed class FailingStream : Stream
{
	private readonly MemoryStream inner; // The data returned before the failure

	/// <summary>
	/// Initialises a new instance of the <see cref="FailingStream"/> class.
	/// </summary>
	/// <param name="content">The data returned before the failure.</param>
	public FailingStream(byte[] content)
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
	public override void Flush()
	{
	}

	/// <summary>
	/// Returns the data, then throws once it has all been read.
	/// </summary>
	/// <param name="buffer">The buffer to fill.</param>
	/// <param name="offset">Where in the buffer to start.</param>
	/// <param name="count">The most bytes to read.</param>
	/// <returns>The number of bytes read.</returns>
	/// <exception cref="IOException">All the data has been read.</exception>
	public override int Read(byte[] buffer, int offset, int count)
	{
		int read = this.inner.Read(buffer, offset, count);

		if (read == 0)
		{
			throw new IOException("The connection was lost.");
		}

		return read;
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
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			this.inner.Dispose();
		}

		base.Dispose(disposing);
	}
}
