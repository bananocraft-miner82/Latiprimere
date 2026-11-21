using System.Buffers.Binary;
using System.Text;

namespace Latiprimere.Ipp.Tests.Fakes;

/// <summary>
/// An IPP response that never ends, as a hostile or broken printer could send: a valid header, then attributes
/// without end, in one of three shapes. It counts the bytes read from it, and ends after <see cref="StopAfter"/>
/// bytes so a test that reads it all still finishes.
/// </summary>
internal sealed class EndlessIppResponseStream : Stream
{
	private readonly byte[] header;   // The response's start, up to and including printer-name's first value
	private readonly byte[] repeated; // Another value of printer-name, sent again and again
	private long position;            // Bytes read so far

	/// <summary>
	/// Initialises a new instance of the <see cref="EndlessIppResponseStream"/> class.
	/// </summary>
	/// <param name="stopAfter">How many bytes to give before ending.</param>
	/// <param name="shape">What repeats.</param>
	/// <param name="valueLength">The length of each repeated value, in bytes.</param>
	public EndlessIppResponseStream(long stopAfter, EndlessIppResponseShape shape, int valueLength)
	{
		this.StopAfter = stopAfter;

		using MemoryStream start = new MemoryStream();

		// Version 1.1, status successful-ok, request ID 1
		start.Write([1, 1, 0, 0, 0, 0, 0, 1]);

		// Operation attributes: charset and natural language
		start.WriteByte(0x01);
		EndlessIppResponseStream.WriteAttribute(start, 0x47, "attributes-charset", "utf-8");
		EndlessIppResponseStream.WriteAttribute(start, 0x48, "attributes-natural-language", "en");

		// Printer attributes: printer-name, whose further values follow without end
		start.WriteByte(0x04);
		EndlessIppResponseStream.WriteAttribute(start, 0x42, "printer-name", "Printer");
		this.header = start.ToArray();

		using MemoryStream value = new MemoryStream();

		if (shape == EndlessIppResponseShape.MoreGroups)
		{
			value.WriteByte(0x04);
		}

		// Another value of printer-name has no name of its own; a new attribute does
		string name = shape == EndlessIppResponseShape.MoreValues ? string.Empty : "printer-info";
		EndlessIppResponseStream.WriteAttribute(value, 0x42, name, new string('x', valueLength));
		this.repeated = value.ToArray();
	}

	/// <summary>Gets how many bytes are given before the stream ends.</summary>
	public long StopAfter { get; }

	/// <summary>Gets how many bytes have been read.</summary>
	public long BytesRead
	{
		get
		{
			return Interlocked.Read(ref this.position);
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
		int written = 0;

		while (written < buffer.Length && this.position < this.StopAfter)
		{
			// Where the next byte comes from: the header, then the repeated value, over and over
			byte next = this.position < this.header.Length
				? this.header[this.position]
				: this.repeated[(this.position - this.header.Length) % this.repeated.Length];

			buffer[written++] = next;
			Interlocked.Increment(ref this.position);
		}

		return written;
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
	/// Writes an attribute with a string value.
	/// </summary>
	/// <param name="stream">Where to write it.</param>
	/// <param name="tag">The value tag.</param>
	/// <param name="name">The name; empty for another value of the attribute before.</param>
	/// <param name="value">The value.</param>
	private static void WriteAttribute(Stream stream, byte tag, string name, string value)
	{
		Span<byte> length = stackalloc byte[2];
		byte[] nameBytes = Encoding.ASCII.GetBytes(name);
		byte[] valueBytes = Encoding.ASCII.GetBytes(value);

		stream.WriteByte(tag);
		BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)nameBytes.Length);
		stream.Write(length);
		stream.Write(nameBytes);
		BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)valueBytes.Length);
		stream.Write(length);
		stream.Write(valueBytes);
	}
}
