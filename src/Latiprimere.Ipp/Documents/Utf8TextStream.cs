using System.Text;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Reads text in its own encoding and gives it as UTF-8 without a byte order mark, a buffer at a time, so text sent to
/// CUPS is re-encoded as it uploads rather than held whole (ADR 0048).
/// </summary>
/// <remarks>
/// A byte order mark at the start of the text overrides the encoding given, and is left out. The text is read from
/// its stream's position; that stream isn't disposed with this.
/// </remarks>
internal sealed class Utf8TextStream : Stream
{
	private const int CharacterBufferSize = 4096;

	private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private readonly StreamReader reader;  // Decodes the text
	private readonly Encoder encoder = Utf8TextStream.Utf8.GetEncoder(); // Keeps a surrogate split across reads
	private readonly char[] characters = new char[Utf8TextStream.CharacterBufferSize]; // The characters last read
	private readonly byte[] encoded = new byte[Utf8TextStream.Utf8.GetMaxByteCount(Utf8TextStream.CharacterBufferSize)]; // Their UTF-8
	private int encodedStart;              // The first byte of encoded not yet given
	private int encodedEnd;                // One past the last
	private bool finished;                 // Whether the text has ended and the encoder been flushed

	/// <summary>
	/// Initialises a new instance of the <see cref="Utf8TextStream"/> class.
	/// </summary>
	/// <param name="source">The text, from its position.</param>
	/// <param name="encoding">Its encoding.</param>
	public Utf8TextStream(Stream source, Encoding encoding)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(encoding);

		this.reader = new StreamReader(source, encoding, detectEncodingFromByteOrderMarks: true, Utf8TextStream.CharacterBufferSize, leaveOpen: true);
	}

	/// <summary>Gets the encoding the text is given in: UTF-8 without a byte order mark.</summary>
	public static Encoding Encoding
	{
		get
		{
			return Utf8TextStream.Utf8;
		}
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
		while (this.encodedStart == this.encodedEnd)
		{
			if (this.finished || buffer.IsEmpty)
			{
				return 0;
			}

			int read = this.reader.Read(this.characters, 0, this.characters.Length);
			this.finished = read == 0;
			this.encodedStart = 0;
			this.encodedEnd = this.encoder.GetBytes(this.characters, 0, read, this.encoded, 0, flush: this.finished);
		}

		int count = Math.Min(buffer.Length, this.encodedEnd - this.encodedStart);
		this.encoded.AsSpan(this.encodedStart, count).CopyTo(buffer);
		this.encodedStart += count;

		return count;
	}

	/// <inheritdoc/>
	public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
	{
		while (this.encodedStart == this.encodedEnd)
		{
			if (this.finished || buffer.IsEmpty)
			{
				return 0;
			}

			int read = await this.reader.ReadAsync(this.characters.AsMemory(), cancellationToken).ConfigureAwait(false);
			this.finished = read == 0;
			this.encodedStart = 0;
			this.encodedEnd = this.encoder.GetBytes(this.characters, 0, read, this.encoded, 0, flush: this.finished);
		}

		int count = Math.Min(buffer.Length, this.encodedEnd - this.encodedStart);
		this.encoded.AsMemory(this.encodedStart, count).CopyTo(buffer);
		this.encodedStart += count;

		return count;
	}

	/// <inheritdoc/>
	public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		return this.ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
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

	/// <summary>
	/// Releases the reader; the text's own stream stays open.
	/// </summary>
	/// <param name="disposing">Whether called from <see cref="Stream.Dispose()"/>.</param>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			this.reader.Dispose();
		}

		base.Dispose(disposing);
	}
}
