using System.Buffers.Binary;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Reads a JPEG's header, to place it on a page without decoding it: PDF's <c>DCTDecode</c> filter carries the JPEG
/// as it is (ISO 32000-1, 7.4.8), so the PDF reader or printer decodes it.
/// </summary>
/// <remarks>
/// It reads the segments before the first scan (ITU-T T.81, B.1): the frame header for the size, precision and
/// components; JFIF's APP0 and EXIF's APP1 for the resolution and orientation; and Adobe's APP14, which marks CMYK
/// stored inverted.
/// </remarks>
internal static class JpegReader
{
	private const byte MarkerPrefix = 0xFF;
	private const byte StartOfImage = 0xD8;
	private const byte StartOfScan = 0xDA;
	private const byte EndOfImage = 0xD9;
	private const byte App0 = 0xE0;
	private const byte App1 = 0xE1;
	private const byte App14 = 0xEE;
	private const double CentimetresPerInch = 2.54;

	/// <summary>
	/// Reads a JPEG.
	/// </summary>
	/// <param name="data">The whole JPEG file.</param>
	/// <returns>The image, its samples the JPEG as it is.</returns>
	/// <exception cref="PrintException">It isn't a JPEG, is damaged, or is coded in a way PDF can't carry.</exception>
	public static PageImage Read(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);

		if (data.Length < 4 || data[0] != JpegReader.MarkerPrefix || data[1] != JpegReader.StartOfImage)
		{
			throw new PrintException(ExceptionMessages.ImageNotJpeg);
		}

		int position = 2;
		ExifInfo exif = ExifInfo.None;
		double? jfifDpiX = null;
		double? jfifDpiY = null;
		bool adobe = false;
		(int Width, int Height, int Components)? frame = null;

		while (true)
		{
			// Markers may be padded with any number of 0xFF bytes
			while (position < data.Length && data[position] != JpegReader.MarkerPrefix)
			{
				position++;
			}

			while (position < data.Length && data[position] == JpegReader.MarkerPrefix)
			{
				position++;
			}

			if (position >= data.Length)
			{
				throw new PrintException(ExceptionMessages.ImageJpegDamaged);
			}

			byte marker = data[position++];

			// Restart markers and TEM stand alone, without a length
			if (marker is (>= 0xD0 and <= 0xD7) or 0x01)
			{
				continue;
			}

			if (marker is JpegReader.StartOfScan or JpegReader.EndOfImage)
			{
				break;
			}

			if (position + 2 > data.Length)
			{
				throw new PrintException(ExceptionMessages.ImageJpegDamaged);
			}

			int length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(position, 2));

			if (length < 2 || position + length > data.Length)
			{
				throw new PrintException(ExceptionMessages.ImageJpegDamaged);
			}

			ReadOnlySpan<byte> segment = data.AsSpan(position + 2, length - 2);
			position += length;

			if (JpegReader.IsFrame(marker))
			{
				frame = JpegReader.ReadFrame(marker, segment);
			}
			else if (marker == JpegReader.App0 && segment.Length >= 12 && segment.StartsWith("JFIF\0"u8))
			{
				(jfifDpiX, jfifDpiY) = JpegReader.ReadJfifDensity(segment);
			}
			else if (marker == JpegReader.App1 && segment.StartsWith("Exif\0\0"u8))
			{
				exif = ExifReader.Read(segment[6..]);
			}
			else if (marker == JpegReader.App14 && segment.StartsWith("Adobe"u8))
			{
				adobe = true;
			}
		}

		if (frame is null)
		{
			throw new PrintException(ExceptionMessages.ImageJpegDamaged);
		}

		(int width, int height, int components) = frame.Value;

		return new PageImage
		{
			Width       = width,
			Height      = height,
			DpiX        = exif.DpiX ?? jfifDpiX,
			DpiY        = exif.DpiY ?? jfifDpiY,
			Orientation = exif.Orientation,
			Image       = new PdfImage
			{
				Width      = width,
				Height     = height,
				ColorSpace = components switch
				{
					1 => PdfColorSpace.Gray,
					3 => PdfColorSpace.Rgb,
					_ => PdfColorSpace.Cmyk,
				},
				Encoding   = PdfStreamEncoding.None,
				Filter     = "DCTDecode",
				// Adobe's applications store CMYK inverted, and mark it with their APP14 segment
				Decode     = components == 4 && adobe ? "[1 0 1 0 1 0 1 0]" : null,
				WriteData  = stream => stream.Write(data)
			}
		};
	}

	/// <summary>
	/// Checks whether a marker starts a frame (SOF0 to SOF15, less DHT, JPG and DAC, which share the range).
	/// </summary>
	/// <param name="marker">The marker.</param>
	/// <returns>Whether it is a frame header.</returns>
	private static bool IsFrame(byte marker)
	{
		return marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);
	}

	/// <summary>
	/// Reads a frame header, refusing codings PDF's <c>DCTDecode</c> can't carry: only baseline, extended and
	/// progressive Huffman coding, at 8 bits a sample.
	/// </summary>
	/// <param name="marker">The SOF marker.</param>
	/// <param name="segment">The header, after its length.</param>
	/// <returns>The size and number of components.</returns>
	/// <exception cref="PrintException">The coding, precision, size or components can't be printed.</exception>
	private static (int Width, int Height, int Components) ReadFrame(byte marker, ReadOnlySpan<byte> segment)
	{
		if (segment.Length < 6)
		{
			throw new PrintException(ExceptionMessages.ImageJpegDamaged);
		}

		// SOF0, SOF1 and SOF2: lossless, hierarchical and arithmetic coding aren't in PDF readers' JPEG decoders
		if (marker is not (0xC0 or 0xC1 or 0xC2))
		{
			throw new PrintException(ExceptionMessages.ImageJpegCodingUnsupported);
		}

		int precision = segment[0];
		int height = BinaryPrimitives.ReadUInt16BigEndian(segment[1..]);
		int width = BinaryPrimitives.ReadUInt16BigEndian(segment[3..]);
		int components = segment[5];

		if (precision != 8)
		{
			throw new PrintException(ExceptionMessages.FormatImageJpegPrecisionUnsupported(precision));
		}

		if (components is not (1 or 3 or 4))
		{
			throw new PrintException(ExceptionMessages.FormatImageJpegComponentsUnsupported(components));
		}

		// A height of 0 is given later, in a DNL segment, which PDF readers needn't support
		if (width == 0 || height == 0)
		{
			throw new PrintException(ExceptionMessages.ImageJpegDamaged);
		}

		// The JPEG isn't decoded here, but the PDF reader or printer would decode every pixel it declares
		if (!PngReader.IsPrintableSize(width, height))
		{
			throw new PrintException(ExceptionMessages.FormatImageTooLarge(width, height));
		}

		return (width, height, components);
	}

	/// <summary>
	/// Reads JFIF's pixel density: dots per inch, dots per centimetre, or only an aspect ratio, which gives no size.
	/// </summary>
	/// <param name="segment">The APP0 segment, after its length.</param>
	/// <returns>The resolutions, or <see langword="null"/> when it gives none.</returns>
	private static (double? DpiX, double? DpiY) ReadJfifDensity(ReadOnlySpan<byte> segment)
	{
		int unit = segment[7];
		int x = BinaryPrimitives.ReadUInt16BigEndian(segment[8..]);
		int y = BinaryPrimitives.ReadUInt16BigEndian(segment[10..]);

		return unit switch
		{
			1 => (x, y),
			2 => (x * JpegReader.CentimetresPerInch, y * JpegReader.CentimetresPerInch),
			_ => (null, null),
		};
	}
}
