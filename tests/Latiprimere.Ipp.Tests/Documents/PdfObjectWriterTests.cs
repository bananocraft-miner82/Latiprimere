using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Latiprimere.Ipp.Documents;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

public sealed class PdfObjectWriterTests
{
	[Fact]
	public void GivenHeaderCommentWhenWrittenThenFileStartsWithVersionCommentAndBinaryMarker()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();

		// Act
		using (PdfObjectWriter writer = new PdfObjectWriter(output, ["PCLm 1.0"]))
		{
			int root = writer.ReserveObject();
			writer.WriteObject(root, "<< /Type /Catalog >>");
			writer.Finish(root, infoNumber: null);
		}

		// Assert — four bytes over 127 on the third line mark the file as binary
		byte[] file = output.ToArray();
		Assert.StartsWith("%PDF-1.7\n%PCLm 1.0\n%", Encoding.Latin1.GetString(file), StringComparison.Ordinal);
		Assert.Equal(new byte[] { 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' }, file[20..25]);
		Assert.EndsWith("%%EOF\n", Encoding.Latin1.GetString(file), StringComparison.Ordinal);
	}

	[Fact]
	public void GivenObjectsWrittenOutOfOrderWhenFinishedThenEveryCrossReferencePointsAtItsObject()
	{
		// Arrange — objects reserved in one order and written in another, a stream among them
		using MemoryStream output = new MemoryStream();

		using (PdfObjectWriter writer = new PdfObjectWriter(output, []))
		{
			int first = writer.ReserveObject();
			int second = writer.ReserveObject();
			int third = writer.ReserveObject();

			// Act
			writer.WriteObject(third, "(third)");
			writer.WriteStream(second, "/Kind /Test", PdfStreamEncoding.Flate, stream => stream.Write(new byte[1000]));
			writer.WriteObject(first, "<< /Type /Catalog >>");
			writer.Finish(first, infoNumber: null);
		}

		// Assert — the stream's length is an object of its own, so there are four, each entry 20 bytes
		string file = Encoding.Latin1.GetString(output.ToArray());
		int table = PdfObjectWriterTests.ReadStartXref(file);
		Assert.StartsWith("xref\n0 5\n0000000000 65535 f \n", file[table..], StringComparison.Ordinal);

		for (int number = 1; number <= 4; number++)
		{
			string entry = file.Substring(table + "xref\n0 5\n".Length + (number * 20), 20);
			Assert.Matches(@"^\d{10} 00000 n \n$", entry);

			int offset = int.Parse(entry[..10], CultureInfo.InvariantCulture);
			Assert.StartsWith(number.ToString(CultureInfo.InvariantCulture) + " 0 obj\n", file[offset..], StringComparison.Ordinal);
		}

		Assert.Contains("trailer\n<< /Size 5 /Root 1 0 R >>", file, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public void GivenStreamWrittenAsItComesWhenFinishedThenItsIndirectLengthIsItsDataLength(int encoding)
	{
		// Arrange — every encoding (provided by InlineData above)
		using MemoryStream output = new MemoryStream();
		byte[] data = Encoding.ASCII.GetBytes(new string('a', 500) + "abcdef");

		using (PdfObjectWriter writer = new PdfObjectWriter(output, []))
		{
			int root = writer.ReserveObject();
			int stream = writer.ReserveObject();

			// Act
			writer.WriteStream(stream, string.Empty, (PdfStreamEncoding)encoding, target => target.Write(data));
			writer.WriteObject(root, "<< >>");
			writer.Finish(root, infoNumber: null);
		}

		// Assert
		string file = Encoding.Latin1.GetString(output.ToArray());
		Match dictionary = Regex.Match(file, @"2 0 obj\n<< (?:/Filter /\w+ )?/Length (\d+) 0 R >>\nstream\n");
		Assert.True(dictionary.Success, file[..200]);

		int start = dictionary.Index + dictionary.Length;
		int end = file.IndexOf("\nendstream", start, StringComparison.Ordinal);
		string lengthObject = Regex.Match(file, dictionary.Groups[1].Value + @" 0 obj\n(\d+)\nendobj").Groups[1].Value;
		Assert.Equal((end - start).ToString(CultureInfo.InvariantCulture), lengthObject);
	}

	[Fact]
	public void GivenStreamWithKnownDataWhenWrittenThenItsLengthIsDirect()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();
		using PdfObjectWriter writer = new PdfObjectWriter(output, []);
		int number = writer.ReserveObject();

		// Act
		writer.WriteStream(number, "/Filter /DCTDecode", "JPEG"u8);

		// Assert
		string file = Encoding.Latin1.GetString(output.ToArray());
		Assert.Contains("1 0 obj\n<< /Filter /DCTDecode /Length 4 >>\nstream\nJPEG\nendstream\nendobj\n", file, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenReservedObjectNotWrittenWhenFinishedThenThrowsInvalidOperationException()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();
		using PdfObjectWriter writer = new PdfObjectWriter(output, []);
		int root = writer.ReserveObject();
		_ = writer.ReserveObject();
		writer.WriteObject(root, "<< >>");

		// Act
		Action finish = () => writer.Finish(root, infoNumber: null);

		// Assert
		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(finish);
		Assert.Contains("2", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenObjectWrittenTwiceWhenWrittenAgainThenThrowsInvalidOperationException()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();
		using PdfObjectWriter writer = new PdfObjectWriter(output, []);
		int number = writer.ReserveObject();
		writer.WriteObject(number, "1");

		// Act
		Action writeAgain = () => writer.WriteObject(number, "2");
		Action writeUnreserved = () => writer.WriteObject(number + 5, "3");

		// Assert
		Assert.Throws<InvalidOperationException>(writeAgain);
		Assert.Throws<InvalidOperationException>(writeUnreserved);
	}

	[Fact]
	public void GivenFinishedFileWhenWrittenToThenThrowsInvalidOperationException()
	{
		// Arrange
		using MemoryStream output = new MemoryStream();
		using PdfObjectWriter writer = new PdfObjectWriter(output, []);
		int root = writer.ReserveObject();
		writer.WriteObject(root, "<< >>");
		writer.Finish(root, infoNumber: null);

		// Act
		Action reserve = () => writer.ReserveObject();
		Action finishAgain = () => writer.Finish(root, infoNumber: null);

		// Assert
		Assert.Throws<InvalidOperationException>(reserve);
		Assert.Throws<InvalidOperationException>(finishAgain);
	}

	[Theory]
	[InlineData("two\nlines")]
	[InlineData("café")]
	public void GivenHeaderCommentThatIsntOneAsciiLineWhenConstructedThenThrowsArgumentException(string comment)
	{
		// Arrange — a line break, or a character outside ASCII (provided by InlineData above)
		using MemoryStream output = new MemoryStream();

		// Act
		Func<PdfObjectWriter> create = () => new PdfObjectWriter(output, [comment]);

		// Assert
		Assert.Throws<ArgumentException>(create);
	}

	/// <summary>
	/// Reads the offset after <c>startxref</c>.
	/// </summary>
	/// <param name="file">The file.</param>
	/// <returns>The cross-reference table's offset.</returns>
	private static int ReadStartXref(string file)
	{
		Match match = Regex.Match(file, @"startxref\n(\d+)\n%%EOF\n$");
		Assert.True(match.Success);

		return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
	}
}
