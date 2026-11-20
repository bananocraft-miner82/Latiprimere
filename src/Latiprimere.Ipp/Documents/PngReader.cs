using System.Buffers.Binary;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Reads a PNG's chunks (PNG specification, third edition, 5): its header, palette, transparency, resolution, EXIF
/// orientation and image data, checking each chunk's CRC.
/// </summary>
/// <remarks>
/// Other ancillary chunks, such as text, gamma, colour profiles and an animated PNG's frames, are skipped, so an
/// animated PNG prints its default image. An unknown critical chunk is refused, as the specification asks.
/// </remarks>
internal static class PngReader
{
	/// <summary>The most pixels an image may have across or down, as for raster pages.</summary>
	internal const int MaxDimension = 100_000;

	/// <summary>
	/// The most pixels an image may have in all: an A3 page at 1200 dpi. A few megabytes of compressed data can
	/// declare far more, which would take minutes to decode and gigabytes to render, so larger images are refused
	/// before anything is decoded.
	/// </summary>
	internal const long MaxPixels = 300_000_000;

	private const double MetresPerInch = 0.0254;

	/// <summary>Gets the eight bytes every PNG starts with.</summary>
	private static ReadOnlySpan<byte> Signature
	{
		get
		{
			return [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
		}
	}

	/// <summary>
	/// Reads a PNG.
	/// </summary>
	/// <param name="data">The whole PNG file.</param>
	/// <returns>Its chunks.</returns>
	/// <exception cref="PrintException">It isn't a PNG, is damaged, or is too large.</exception>
	public static PngHeader Read(byte[] data)
	{
		ArgumentNullException.ThrowIfNull(data);

		if (!data.AsSpan().StartsWith(PngReader.Signature))
		{
			throw new PrintException(ExceptionMessages.ImageNotPng);
		}

		int position = PngReader.Signature.Length;
		PngHeader? header = null;
		byte[]? palette = null;
		byte[]? transparency = null;
		double? dpiX = null;
		double? dpiY = null;
		int orientation = 1;
		List<(int Offset, int Length)> imageData = [];

		while (position < data.Length)
		{
			// Length, type, data, CRC
			if (position + 12 > data.Length)
			{
				throw new PrintException(ExceptionMessages.ImagePngDataDamaged);
			}

			uint length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position));
			string type = System.Text.Encoding.ASCII.GetString(data, position + 4, 4);

			if (length > int.MaxValue || position + 12L + length > data.Length)
			{
				throw new PrintException(ExceptionMessages.FormatImagePngChunkDamaged(type));
			}

			int dataStart = position + 8;
			ReadOnlySpan<byte> chunk = data.AsSpan(dataStart, (int)length);
			uint crc = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(dataStart + (int)length));

			if (PngCrc.Compute(data.AsSpan(position + 4, 4 + (int)length)) != crc)
			{
				throw new PrintException(ExceptionMessages.FormatImagePngChunkDamaged(type));
			}

			position = dataStart + (int)length + 4;

			// The header comes first, and once
			if ((header is null) != (type == "IHDR"))
			{
				throw new PrintException(ExceptionMessages.FormatImagePngChunkDamaged(type));
			}

			switch (type)
			{
				case "IHDR":
					header = PngReader.ReadHeader(data, chunk);
					break;

				case "PLTE" when chunk.Length is > 0 and <= 768 && chunk.Length % 3 == 0:
					palette = chunk.ToArray();
					break;

				case "PLTE":
					throw new PrintException(ExceptionMessages.FormatImagePngChunkDamaged(type));

				case "tRNS":
					transparency = chunk.ToArray();
					break;

				case "pHYs" when chunk.Length == 9 && chunk[8] == 1:
					// Pixels per metre
					dpiX = BinaryPrimitives.ReadUInt32BigEndian(chunk) * PngReader.MetresPerInch;
					dpiY = BinaryPrimitives.ReadUInt32BigEndian(chunk[4..]) * PngReader.MetresPerInch;
					break;

				case "eXIf":
					orientation = ExifReader.Read(chunk).Orientation;
					break;

				case "IDAT":
					imageData.Add((dataStart, (int)length));
					break;

				case "IEND":
					position = data.Length;
					break;

				default:
					// A critical chunk's first letter is upper case; it can't be skipped
					if (char.IsAsciiLetterUpper(type[0]))
					{
						throw new PrintException(ExceptionMessages.FormatImagePngChunkDamaged(type));
					}

					break;
			}
		}

		if (header is null || imageData.Count == 0)
		{
			throw new PrintException(ExceptionMessages.ImagePngDataDamaged);
		}

		if (header.ColorType == 3 && palette is null)
		{
			throw new PrintException(ExceptionMessages.FormatImagePngChunkDamaged("PLTE"));
		}

		return header with
		{
			Palette      = header.ColorType == 3 ? palette : null,
			Transparency = transparency,
			DpiX         = dpiX,
			DpiY         = dpiY,
			Orientation  = orientation,
			ImageData    = imageData
		};
	}

	/// <summary>
	/// Checks an image's size is within <see cref="MaxDimension"/> each way and <see cref="MaxPixels"/> in all.
	/// </summary>
	/// <param name="width">Its width in pixels.</param>
	/// <param name="height">Its height in pixels.</param>
	/// <returns>Whether it can be printed.</returns>
	internal static bool IsPrintableSize(long width, long height)
	{
		return width <= PngReader.MaxDimension && height <= PngReader.MaxDimension && width * height <= PngReader.MaxPixels;
	}

	/// <summary>
	/// Reads the <c>IHDR</c> chunk, checking its bit depth suits its colour type.
	/// </summary>
	/// <param name="data">The whole file.</param>
	/// <param name="chunk">The chunk's data.</param>
	/// <returns>The header, without the later chunks.</returns>
	/// <exception cref="PrintException">The header isn't valid, or the image is too large.</exception>
	private static PngHeader ReadHeader(byte[] data, ReadOnlySpan<byte> chunk)
	{
		if (chunk.Length != 13)
		{
			throw new PrintException(ExceptionMessages.FormatImagePngChunkDamaged("IHDR"));
		}

		uint width = BinaryPrimitives.ReadUInt32BigEndian(chunk);
		uint height = BinaryPrimitives.ReadUInt32BigEndian(chunk[4..]);
		int bitDepth = chunk[8];
		int colorType = chunk[9];

		bool valid = colorType switch
		{
			0 => bitDepth is 1 or 2 or 4 or 8 or 16,
			3 => bitDepth is 1 or 2 or 4 or 8,
			2 or 4 or 6 => bitDepth is 8 or 16,
			_ => false,
		};

		// Compression, filter method and interlace method
		if (!valid || width == 0 || height == 0 || chunk[10] != 0 || chunk[11] != 0 || chunk[12] > 1)
		{
			throw new PrintException(ExceptionMessages.FormatImagePngHeaderInvalid(width, height, bitDepth, colorType));
		}

		if (!PngReader.IsPrintableSize(width, height))
		{
			throw new PrintException(ExceptionMessages.FormatImageTooLarge(width, height));
		}

		return new PngHeader
		{
			Data       = data,
			Width      = (int)width,
			Height     = (int)height,
			BitDepth   = bitDepth,
			ColorType  = colorType,
			Interlaced = chunk[12] == 1,
			ImageData  = []
		};
	}
}
