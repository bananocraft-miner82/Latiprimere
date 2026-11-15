namespace Latiprimere.Rendering.Pdfium.Tests.Fakes;

/// <summary>
/// A seekable stream that claims to be larger than 2 GB, without holding any data.
/// </summary>
internal sealed class OversizedStream : Stream
{
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
			return true;
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

	/// <summary>
	/// Gets the claimed length: one byte more than 2 GB.
	/// </summary>
	public override long Length
	{
		get
		{
			return int.MaxValue + 1L;
		}
	}

	/// <inheritdoc/>
	public override long Position { get; set; }

	/// <inheritdoc/>
	public override void Flush()
	{
	}

	/// <summary>
	/// Returns no data: nothing should read this far.
	/// </summary>
	/// <param name="buffer">Not used.</param>
	/// <param name="offset">Not used.</param>
	/// <param name="count">Not used.</param>
	/// <returns>Zero.</returns>
	public override int Read(byte[] buffer, int offset, int count)
	{
		return 0;
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
}
