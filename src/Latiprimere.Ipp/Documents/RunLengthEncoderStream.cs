namespace Latiprimere.Ipp.Documents;

/// <summary>
/// A write-only stream that encodes what it is given for PDF's <c>RunLengthDecode</c> filter (ISO 32000-1, 7.4.5),
/// which PCLm printers may prefer: a length byte of 0 to 127 copies the next 1 to 128 bytes, one of 129 to 255 repeats
/// the next byte 128 down to 2 times, and 128 ends the data, which disposing writes. It never disposes the other stream.
/// </summary>
/// <remarks>
/// Each write is encoded on its own, so a run across two writes is encoded as two; still correct, and rare, as
/// writers give whole lines or strips.
/// </remarks>
internal sealed class RunLengthEncoderStream : Stream
{
	/// <summary>The most bytes one copy or one repeat covers.</summary>
	internal const int MaxRun = 128;

	/// <summary>The length byte that ends the data.</summary>
	internal const byte EndOfData = 128;

	/// <summary>The shortest run worth a repeat: two bytes, instead of three or more in a copy.</summary>
	private const int MinRepeat = 3;

	private readonly Stream inner; // Where the encoded bytes go, not owned
	private bool isEnded;          // Whether the end of data has been written

	/// <summary>
	/// Initialises a new instance of the <see cref="RunLengthEncoderStream"/> class.
	/// </summary>
	/// <param name="inner">Where the encoded bytes go. It isn't disposed with this stream.</param>
	/// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
	public RunLengthEncoderStream(Stream inner)
	{
		ArgumentNullException.ThrowIfNull(inner);

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
			return !this.isEnded;
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

	/// <summary>
	/// Encodes bytes, without the end of data.
	/// </summary>
	/// <param name="data">The bytes.</param>
	/// <returns>The encoded bytes: at most one more for each 128, plus two.</returns>
	internal static byte[] Encode(ReadOnlySpan<byte> data)
	{
		byte[] encoded = new byte[data.Length + (data.Length / RunLengthEncoderStream.MaxRun) + 2];
		int length = 0;
		int index = 0;

		while (index < data.Length)
		{
			int run = RunLengthEncoderStream.CountRun(data, index);

			if (run >= RunLengthEncoderStream.MinRepeat)
			{
				// 257 - run gives 129 for 128 repeats, down to 254 for 3
				encoded[length++] = (byte)(257 - run);
				encoded[length++] = data[index];
				index += run;

				continue;
			}

			// Copy bytes until the next run worth a repeat, or 128 of them
			int start = index;

			while (index < data.Length
				   && index - start < RunLengthEncoderStream.MaxRun
				   && RunLengthEncoderStream.CountRun(data, index) < RunLengthEncoderStream.MinRepeat)
			{
				index++;
			}

			encoded[length++] = (byte)(index - start - 1);
			data[start..index].CopyTo(encoded.AsSpan(length));
			length += index - start;
		}

		return encoded[..length];
	}

	/// <inheritdoc/>
	public override void Write(byte[] buffer, int offset, int count)
	{
		this.Write(buffer.AsSpan(offset, count));
	}

	/// <inheritdoc/>
	public override void Write(ReadOnlySpan<byte> buffer)
	{
		ObjectDisposedException.ThrowIf(this.isEnded, this);

		if (!buffer.IsEmpty)
		{
			this.inner.Write(RunLengthEncoderStream.Encode(buffer));
		}
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

	/// <summary>
	/// Writes the end of data, once.
	/// </summary>
	/// <param name="disposing">Whether called from <see cref="Stream.Dispose()"/>.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing && !this.isEnded)
		{
			this.inner.WriteByte(RunLengthEncoderStream.EndOfData);
			this.isEnded = true;
		}

		base.Dispose(disposing);
	}

	/// <summary>
	/// Counts how many times the byte at an index repeats from there, up to 128.
	/// </summary>
	/// <param name="data">The bytes.</param>
	/// <param name="index">Where the run starts.</param>
	/// <returns>The run's length, at least 1.</returns>
	private static int CountRun(ReadOnlySpan<byte> data, int index)
	{
		int run = 1;

		while (index + run < data.Length && run < RunLengthEncoderStream.MaxRun && data[index + run] == data[index])
		{
			run++;
		}

		return run;
	}
}
