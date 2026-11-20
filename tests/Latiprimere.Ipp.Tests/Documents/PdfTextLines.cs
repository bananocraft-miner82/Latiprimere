using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Latiprimere.Ipp.Tests.Documents;

/// <summary>
/// Reads back the lines of text Latiprimere's PDF writer drew: each page's content stream, inflated, and its
/// <c>Td</c> positions and <c>Tj</c> strings, unescaped. Only for PDFs that writer made, whose syntax it knows.
/// </summary>
internal static partial class PdfTextLines
{
	/// <summary>
	/// Reads every page's lines.
	/// </summary>
	/// <param name="pdf">The PDF.</param>
	/// <returns>For each page, in order, its lines: where each starts, in points from the bottom left, and its text,
	/// each byte as the Latin-1 character of the same value.</returns>
	public static IReadOnlyList<IReadOnlyList<(double X, double Y, string Text)>> Read(byte[] pdf)
	{
		string file = Encoding.Latin1.GetString(pdf);
		List<IReadOnlyList<(double X, double Y, string Text)>> pages = [];

		// Content streams are the only streams with nothing in their dictionary but their filter and length
		foreach (Match stream in PdfTextLines.ContentStreamPattern().Matches(file))
		{
			string lengthObject = stream.Groups["length"].Value + " 0 obj\n";
			int lengthAt = file.IndexOf(lengthObject, stream.Index + stream.Length, StringComparison.Ordinal);
			Assert.True(lengthAt > 0);
			int valueStart = lengthAt + lengthObject.Length;
			int length = int.Parse(file.AsSpan(valueStart, file.IndexOf('\n', valueStart) - valueStart), CultureInfo.InvariantCulture);

			using ZLibStream inflater = new ZLibStream(new MemoryStream(pdf, stream.Index + stream.Length, length), CompressionMode.Decompress);
			using MemoryStream content = new MemoryStream();
			inflater.CopyTo(content);

			pages.Add(PdfTextLines.ReadLines(Encoding.Latin1.GetString(content.ToArray())));
		}

		return pages;
	}

	/// <summary>
	/// Reads the lines drawn in one content stream.
	/// </summary>
	/// <param name="content">The content stream.</param>
	/// <returns>The lines.</returns>
	private static List<(double X, double Y, string Text)> ReadLines(string content)
	{
		List<(double X, double Y, string Text)> lines = [];

		foreach (Match line in PdfTextLines.TextPattern().Matches(content))
		{
			lines.Add
			((
				double.Parse(line.Groups["x"].Value, CultureInfo.InvariantCulture),
				double.Parse(line.Groups["y"].Value, CultureInfo.InvariantCulture),
				PdfTextLines.Unescape(line.Groups["text"].Value)
			));
		}

		return lines;
	}

	/// <summary>
	/// Undoes a literal string's escapes: <c>\(</c>, <c>\)</c>, <c>\\</c> and three octal digits.
	/// </summary>
	/// <param name="escaped">The string, without its parentheses.</param>
	/// <returns>Its bytes, as Latin-1 characters.</returns>
	private static string Unescape(string escaped)
	{
		StringBuilder text = new StringBuilder();

		for (int index = 0; index < escaped.Length; index++)
		{
			if (escaped[index] != '\\')
			{
				text.Append(escaped[index]);
			}
			else if (char.IsAsciiDigit(escaped[index + 1]))
			{
				text.Append((char)Convert.ToInt32(escaped.Substring(index + 1, 3), 8));
				index += 3;
			}
			else
			{
				text.Append(escaped[++index]);
			}
		}

		return text.ToString();
	}

	/// <summary>
	/// Matches a content stream's dictionary, up to its data.
	/// </summary>
	/// <returns>The pattern.</returns>
	[GeneratedRegex(@"<< /Filter /FlateDecode /Length (?<length>\d+) 0 R >>\nstream\n", RegexOptions.CultureInvariant)]
	private static partial Regex ContentStreamPattern();

	/// <summary>
	/// Matches a line of text as the writer draws it.
	/// </summary>
	/// <returns>The pattern.</returns>
	[GeneratedRegex(@"BT /F1 [\d.]+ Tf (?<x>[\d.]+) (?<y>[\d.]+) Td \((?<text>(?:[^\\)]|\\.)*)\) Tj ET", RegexOptions.CultureInvariant)]
	private static partial Regex TextPattern();
}
