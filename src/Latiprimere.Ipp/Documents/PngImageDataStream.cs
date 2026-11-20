namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Reads a PNG's image data as one stream: the data of its <c>IDAT</c> chunks, one after another, which together
/// are a zlib stream.
/// </summary>
internal sealed class PngImageDataStream : Stream
{
	private readonly byte[] data;                                 // The whole file
	private readonly IReadOnlyList<(int Offset, int Length)> chunks; // Each IDAT chunk's data in the file
	private int chunk;                                            // The chunk being read
	private int position;                                         // How far into that chunk

	/// <summary>
	/// Initialises a new instance of the <see cref="PngImageDataStream"/> class.
	/// </summary>
	/// <param name="header">The PNG.</param>
	public PngImageDataStream(PngHeader header)
	{
		ArgumentNullException.ThrowIfNull(header);

		this.data = header.Data;
		this.chunks = header.ImageData;
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
		return this.Read(buffer.AsSpan(offset, count));
	}

	/// <inheritdoc/>
	public override int Read(Span<byte> buffer)
	{
		// Empty IDAT chunks are allowed, so skip past any
		while (this.chunk < this.chunks.Count && this.position == this.chunks[this.chunk].Length)
		{
			this.chunk++;
			this.position = 0;
		}

		if (this.chunk == this.chunks.Count || buffer.IsEmpty)
		{
			return 0;
		}

		(int offset, int length) = this.chunks[this.chunk];
		int count = Math.Min(buffer.Length, length - this.position);
		this.data.AsSpan(offset + this.position, count).CopyTo(buffer);
		this.position += count;

		return count;
	}

	/// <inheritdoc/>
	public override void Flush()
	{
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
