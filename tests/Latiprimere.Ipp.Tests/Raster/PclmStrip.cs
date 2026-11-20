using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Latiprimere.Ipp.Tests.Raster;

/// <summary>
/// A strip of a PCLm page, decoded as a printer decodes it: from the image's stream, not by rendering the PDF.
/// </summary>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its lines.</param>
/// <param name="BytesPerPixel">1 for <c>DeviceGray</c>, 3 for <c>DeviceRGB</c>.</param>
/// <param name="Data">Its decoded samples, top line first.</param>
internal sealed partial record PclmStrip(int Width, int Height, int BytesPerPixel, byte[] Data)
{
	/// <summary>
	/// Reads every strip of a PCLm file, in the order they're written: each page's from the top, page by page.
	/// </summary>
	/// <param name="file">The file.</param>
	/// <returns>The strips.</returns>
	public static IReadOnlyList<PclmStrip> ReadAll(byte[] file)
	{
		string text = Encoding.Latin1.GetString(file);
		List<PclmStrip> strips = new List<PclmStrip>();

		foreach (Match image in PclmStrip.ImagePattern().Matches(text))
		{
			int width = int.Parse(image.Groups["width"].Value, CultureInfo.InvariantCulture);
			int height = int.Parse(image.Groups["height"].Value, CultureInfo.InvariantCulture);
			int bytesPerPixel = image.Groups["space"].Value == "DeviceRGB" ? 3 : 1;

			// The stream's length is an indirect object, written after it
			string lengthObject = image.Groups["length"].Value + " 0 obj\n";
			int lengthAt = text.IndexOf(lengthObject, image.Index + image.Length, StringComparison.Ordinal);
			Assert.True(lengthAt > 0);
			int lengthEnd = text.IndexOf('\n', lengthAt + lengthObject.Length);
			int length = int.Parse(text.AsSpan(lengthAt + lengthObject.Length, lengthEnd - lengthAt - lengthObject.Length), CultureInfo.InvariantCulture);

			byte[] encoded = file.AsSpan(image.Index + image.Length, length).ToArray();
			byte[] data = image.Groups["filter"].Value == "FlateDecode" ? PclmStrip.Inflate(encoded) : PclmStrip.RunLengthDecode(encoded);

			Assert.Equal(width * height * bytesPerPixel, data.Length);
			strips.Add(new PclmStrip(width, height, bytesPerPixel, data));
		}

		return strips;
	}

	/// <summary>
	/// Decodes as PDF's <c>FlateDecode</c> filter does: zlib.
	/// </summary>
	/// <param name="encoded">The encoded bytes.</param>
	/// <returns>The decoded bytes.</returns>
	private static byte[] Inflate(byte[] encoded)
	{
		using ZLibStream inflater = new ZLibStream(new MemoryStream(encoded), CompressionMode.Decompress);
		using MemoryStream decoded = new MemoryStream();
		inflater.CopyTo(decoded);

		return decoded.ToArray();
	}

	/// <summary>
	/// Decodes as PDF's <c>RunLengthDecode</c> filter does (ISO 32000-1, 7.4.5).
	/// </summary>
	/// <param name="encoded">The encoded bytes, ending with the end of data.</param>
	/// <returns>The decoded bytes.</returns>
	private static byte[] RunLengthDecode(byte[] encoded)
	{
		List<byte> decoded = new List<byte>();
		int index = 0;

		while (true)
		{
			byte length = encoded[index++];

			if (length == 128)
			{
				Assert.Equal(encoded.Length, index);

				return decoded.ToArray();
			}

			if (length < 128)
			{
				decoded.AddRange(encoded.AsSpan(index, length + 1).ToArray());
				index += length + 1;
			}
			else
			{
				decoded.AddRange(Enumerable.Repeat(encoded[index++], 257 - length));
			}
		}
	}

	/// <summary>
	/// Matches an image's dictionary as Latiprimere writes it, up to the start of its data.
	/// </summary>
	/// <returns>The pattern.</returns>
	[GeneratedRegex(@"/Subtype /Image /Width (?<width>\d+) /Height (?<height>\d+) /ColorSpace /(?<space>DeviceGray|DeviceRGB) /BitsPerComponent 8 /Filter /(?<filter>FlateDecode|RunLengthDecode) /Length (?<length>\d+) 0 R >>\nstream\n", RegexOptions.CultureInvariant)]
	private static partial Regex ImagePattern();
}
