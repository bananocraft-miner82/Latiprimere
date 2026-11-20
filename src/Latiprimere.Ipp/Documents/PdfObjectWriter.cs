using System.Globalization;
using System.IO.Compression;
using System.Text;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Writes a PDF file's objects as they are made, then its cross-reference table and trailer (ISO 32000-1, 7.5;
/// ADR 0045). The output needn't seek: offsets are counted as bytes are written.
/// </summary>
/// <remarks>
/// <para>
/// Object numbers are reserved first and written in any order, so an object can refer to one written later, as a
/// page does to its page tree. A stream whose length isn't known in advance gets an indirect <c>/Length</c>, an
/// object written after it, so its data never has to be held whole.
/// </para>
/// <para>
/// Not safe to use from several threads at once.
/// </para>
/// </remarks>
internal sealed class PdfObjectWriter : IDisposable
{
	/// <summary>A comment of four bytes over 127, which marks a file as binary for programs that guess (7.5.2).</summary>
	private static readonly byte[] BinaryMarker = [(byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n'];

	private readonly CountingStream output;                         // The file, counting its bytes
	private readonly List<long?> offsets = new List<long?> { null }; // Each object's offset by number; null until written
	private bool isFinished;                                        // Whether the trailer is written

	/// <summary>
	/// Initialises a new instance of the <see cref="PdfObjectWriter"/> class, and writes the file's header.
	/// </summary>
	/// <param name="output">Where the file goes. It isn't disposed with the writer.</param>
	/// <param name="headerComments">Comments to write after the version line, without their <c>%</c>, such as
	/// <c>PCLm 1.0</c>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="output"/> or <paramref name="headerComments"/> is
	/// <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">A comment has a line break, or a character outside ASCII.</exception>
	public PdfObjectWriter(Stream output, IReadOnlyList<string> headerComments)
	{
		ArgumentNullException.ThrowIfNull(output);
		ArgumentNullException.ThrowIfNull(headerComments);

		foreach (string comment in headerComments)
		{
			if (comment is null || comment.Any(character => character is '\r' or '\n' or > '~' or < ' '))
			{
				throw new ArgumentException(ExceptionMessages.FormatPdfHeaderCommentInvalid(comment), nameof(headerComments));
			}
		}

		this.output = new CountingStream(output);
		this.WriteAscii("%PDF-1.7\n");

		foreach (string comment in headerComments)
		{
			this.WriteAscii("%" + comment + "\n");
		}

		this.output.Write(PdfObjectWriter.BinaryMarker);
	}

	/// <summary>
	/// Gets how many bytes have been written.
	/// </summary>
	public long BytesWritten
	{
		get
		{
			return this.output.BytesWritten;
		}
	}

	/// <summary>
	/// Reserves the next object number, for an object written now or later.
	/// </summary>
	/// <returns>The number.</returns>
	/// <exception cref="InvalidOperationException">The file is finished.</exception>
	public int ReserveObject()
	{
		this.ThrowIfFinished();
		this.offsets.Add(null);

		return this.offsets.Count - 1;
	}

	/// <summary>
	/// Writes an object that isn't a stream.
	/// </summary>
	/// <param name="number">Its reserved number.</param>
	/// <param name="body">Its value in PDF syntax, such as <c>&lt;&lt; /Type /Catalog /Pages 2 0 R &gt;&gt;</c>.</param>
	/// <exception cref="InvalidOperationException">The number isn't reserved, is written already, or the file is
	/// finished.</exception>
	public void WriteObject(int number, string body)
	{
		ArgumentNullException.ThrowIfNull(body);

		this.BeginObject(number);
		this.WriteAscii(body);
		this.WriteAscii("\nendobj\n");
	}

	/// <summary>
	/// Writes a stream object whose data is known, with its length.
	/// </summary>
	/// <param name="number">Its reserved number.</param>
	/// <param name="dictionaryEntries">Its dictionary's entries, other than <c>/Length</c>, in PDF syntax.</param>
	/// <param name="data">Its data, as stored.</param>
	/// <exception cref="InvalidOperationException">The number isn't reserved, is written already, or the file is
	/// finished.</exception>
	public void WriteStream(int number, string dictionaryEntries, ReadOnlySpan<byte> data)
	{
		ArgumentNullException.ThrowIfNull(dictionaryEntries);

		this.BeginObject(number);
		this.WriteAscii("<< " + PdfObjectWriter.WithSpace(dictionaryEntries) + "/Length " + data.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
		this.output.Write(data);
		this.WriteAscii("\nendstream\nendobj\n");
	}

	/// <summary>
	/// Writes a stream object whose data is written as it comes, compressed as asked, with an indirect length.
	/// </summary>
	/// <param name="number">Its reserved number.</param>
	/// <param name="dictionaryEntries">Its dictionary's entries, other than <c>/Length</c> and the encoding's
	/// <c>/Filter</c>, in PDF syntax.</param>
	/// <param name="encoding">How to compress the data; with <see cref="PdfStreamEncoding.None"/>, the entries name
	/// any filter.</param>
	/// <param name="writeData">Writes the data to the stream it is given, which compresses it.</param>
	/// <exception cref="InvalidOperationException">The number isn't reserved, is written already, or the file is
	/// finished.</exception>
	public void WriteStream(int number, string dictionaryEntries, PdfStreamEncoding encoding, Action<Stream> writeData)
	{
		ArgumentNullException.ThrowIfNull(dictionaryEntries);
		ArgumentNullException.ThrowIfNull(writeData);

		string filter = encoding switch
		{
			PdfStreamEncoding.Flate     => "/Filter /FlateDecode ",
			PdfStreamEncoding.RunLength => "/Filter /RunLengthDecode ",
			_                           => string.Empty,
		};

		int lengthNumber = this.ReserveObject();

		this.BeginObject(number);
		this.WriteAscii("<< " + PdfObjectWriter.WithSpace(dictionaryEntries) + filter + "/Length " + lengthNumber.ToString(CultureInfo.InvariantCulture) + " 0 R >>\nstream\n");

		long start = this.output.BytesWritten;

		using (Stream encoder = this.CreateEncoder(encoding))
		{
			writeData(encoder);
		}

		long length = this.output.BytesWritten - start;

		this.WriteAscii("\nendstream\nendobj\n");
		this.WriteObject(lengthNumber, length.ToString(CultureInfo.InvariantCulture));
	}

	/// <summary>
	/// Writes the cross-reference table and the trailer, ending the file. Every reserved object must be written.
	/// </summary>
	/// <param name="rootNumber">The document catalog's number.</param>
	/// <param name="infoNumber">The document information dictionary's number, or <see langword="null"/>.</param>
	/// <exception cref="InvalidOperationException">A reserved object isn't written, or the file is finished
	/// already.</exception>
	public void Finish(int rootNumber, int? infoNumber)
	{
		this.ThrowIfFinished();

		int missing = this.offsets.FindIndex(1, offset => offset is null);

		if (missing > 0)
		{
			throw new InvalidOperationException(ExceptionMessages.FormatPdfObjectNotWritten(missing));
		}

		long tableOffset = this.output.BytesWritten;
		string count = this.offsets.Count.ToString(CultureInfo.InvariantCulture);
		StringBuilder table = new StringBuilder();

		// Each entry is exactly 20 bytes, its end of line a space and a line feed (7.5.4)
		table.Append("xref\n0 ").Append(count).Append("\n0000000000 65535 f \n");

		foreach (long? offset in this.offsets.Skip(1))
		{
			table.Append(offset!.Value.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
		}

		string info = infoNumber is null ? string.Empty : " /Info " + infoNumber.Value.ToString(CultureInfo.InvariantCulture) + " 0 R";

		table.Append("trailer\n<< /Size ").Append(count)
			 .Append(" /Root ").Append(rootNumber.ToString(CultureInfo.InvariantCulture)).Append(" 0 R").Append(info)
			 .Append(" >>\nstartxref\n").Append(tableOffset.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");

		this.WriteAscii(table.ToString());
		this.output.Flush();
		this.isFinished = true;
	}

	/// <summary>
	/// Releases the counting stream. The output isn't closed: it belongs to the caller.
	/// </summary>
	public void Dispose()
	{
		this.output.Dispose();
	}

	/// <summary>
	/// Ends dictionary entries with a space, so the next entry can follow, unless there are none.
	/// </summary>
	/// <param name="entries">The entries.</param>
	/// <returns>The entries and a space, or empty.</returns>
	private static string WithSpace(string entries)
	{
		return entries.Length == 0 ? string.Empty : entries + " ";
	}

	/// <summary>
	/// Records an object's offset and writes its opening line.
	/// </summary>
	/// <param name="number">Its reserved number.</param>
	/// <exception cref="InvalidOperationException">The number isn't reserved, is written already, or the file is
	/// finished.</exception>
	private void BeginObject(int number)
	{
		this.ThrowIfFinished();

		if (number < 1 || number >= this.offsets.Count || this.offsets[number] is not null)
		{
			throw new InvalidOperationException(ExceptionMessages.FormatPdfObjectNotReserved(number));
		}

		this.offsets[number] = this.output.BytesWritten;
		this.WriteAscii(number.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
	}

	/// <summary>
	/// Creates the stream a stream object's data is written to, which compresses it into the file and doesn't close
	/// the file when disposed.
	/// </summary>
	/// <param name="encoding">How to compress the data.</param>
	/// <returns>The stream; disposing it writes the compression's end.</returns>
	private Stream CreateEncoder(PdfStreamEncoding encoding)
	{
		return encoding switch
		{
			PdfStreamEncoding.Flate     => new ZLibStream(this.output, CompressionLevel.Optimal, leaveOpen: true),
			PdfStreamEncoding.RunLength => new RunLengthEncoderStream(this.output),
			_                           => new CountingStream(this.output),
		};
	}

	/// <summary>
	/// Writes text that is ASCII, as PDF's syntax is.
	/// </summary>
	/// <param name="text">The text.</param>
	private void WriteAscii(string text)
	{
		this.output.Write(Encoding.ASCII.GetBytes(text));
	}

	/// <summary>
	/// Refuses to write to a finished file.
	/// </summary>
	/// <exception cref="InvalidOperationException">The file is finished.</exception>
	private void ThrowIfFinished()
	{
		if (this.isFinished)
		{
			throw new InvalidOperationException(ExceptionMessages.PdfFileFinished);
		}
	}
}
