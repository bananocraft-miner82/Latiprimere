using System.IO.Compression;
using Latiprimere.Ipp.Resources;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Decodes a PNG's pixels into 8-bit grey or colour rows, with transparency laid over white (PNG specification,
/// third edition, 7 to 9): every colour type and bit depth, the five row filters, and Adam7 interlacing.
/// </summary>
/// <remarks>
/// <para>
/// Rows are decoded one at a time and written as they are, so an image that isn't interlaced is never held whole.
/// An interlaced image arrives in seven passes over the whole image, so it is put together in memory first.
/// </para>
/// <para>
/// An opaque PNG of 8-bit grey or colour that isn't interlaced isn't decoded at all: its image data goes into the
/// PDF as it is, as PDF's Flate filter with the PNG predictors reads PNG's own filtered rows (ISO 32000-1, 7.4.4.4).
/// </para>
/// </remarks>
internal static class PngDecoder
{
	// Adam7's passes: the first column and row each covers, and the steps between them
	private static readonly (int X, int Y, int StepX, int StepY)[] Passes =
	[
		(0, 0, 8, 8),
		(4, 0, 8, 8),
		(0, 4, 4, 8),
		(2, 0, 4, 4),
		(0, 2, 2, 4),
		(1, 0, 2, 2),
		(0, 1, 1, 2),
	];

	/// <summary>
	/// Reads a PNG, ready to place on a page.
	/// </summary>
	/// <param name="data">The whole PNG file.</param>
	/// <returns>The image. Its samples are decoded as the PDF writer asks for them.</returns>
	/// <exception cref="PrintException">It isn't a PNG, is damaged, or is too large.</exception>
	public static PageImage Read(byte[] data)
	{
		PngHeader png = PngReader.Read(data);
		PdfColorSpace colorSpace = png.IsGrey ? PdfColorSpace.Gray : PdfColorSpace.Rgb;
		PdfImage image = PngDecoder.CanPassThrough(png)
			? new PdfImage
			{
				Width            = png.Width,
				Height           = png.Height,
				ColorSpace       = colorSpace,
				Encoding         = PdfStreamEncoding.None,
				Filter           = "FlateDecode",
				DecodeParameters = FormattableString.Invariant($"<< /Predictor 15 /Colors {png.Channels} /BitsPerComponent 8 /Columns {png.Width} >>"),
				WriteData        = stream => new PngImageDataStream(png).CopyTo(stream)
			}
			: new PdfImage
			{
				Width      = png.Width,
				Height     = png.Height,
				ColorSpace = colorSpace,
				WriteData  = stream => PngDecoder.WriteRows(png, stream)
			};

		return new PageImage
		{
			Width       = png.Width,
			Height      = png.Height,
			DpiX        = png.DpiX,
			DpiY        = png.DpiY,
			Orientation = png.Orientation,
			Image       = image
		};
	}

	/// <summary>
	/// Decodes every row, from the top, as 8-bit grey for colour types 0 and 4, otherwise 8-bit colour.
	/// </summary>
	/// <param name="png">The PNG.</param>
	/// <param name="output">Where the rows go.</param>
	/// <exception cref="PrintException">The image data ends early or can't be decompressed.</exception>
	public static void WriteRows(PngHeader png, Stream output)
	{
		ArgumentNullException.ThrowIfNull(png);
		ArgumentNullException.ThrowIfNull(output);

		int outputChannels = png.IsGrey ? 1 : 3;

		try
		{
			using ZLibStream inflater = new ZLibStream(new PngImageDataStream(png), CompressionMode.Decompress);

			if (!png.Interlaced)
			{
				PngDecoder.DecodePass(png, inflater, png.Width, png.Height, (y, pixels) => output.Write(pixels));

				return;
			}

			// Each pass covers every few pixels of the whole image, so it is put together before any row is written
			int width = png.Width * outputChannels;
			byte[] image = new byte[(long)width * png.Height];

			foreach ((int startX, int startY, int stepX, int stepY) in PngDecoder.Passes)
			{
				int passWidth = png.Width > startX ? (png.Width - startX + stepX - 1) / stepX : 0;
				int passHeight = png.Height > startY ? (png.Height - startY + stepY - 1) / stepY : 0;

				if (passWidth == 0 || passHeight == 0)
				{
					continue;
				}

				PngDecoder.DecodePass(png, inflater, passWidth, passHeight, (y, pixels) =>
				{
					int rowStart = (startY + (y * stepY)) * width;

					for (int x = 0; x < passWidth; x++)
					{
						int target = rowStart + ((startX + (x * stepX)) * outputChannels);
						pixels.Slice(x * outputChannels, outputChannels).CopyTo(image.AsSpan(target));
					}
				});
			}

			for (int y = 0; y < png.Height; y++)
			{
				output.Write(image, y * width, width);
			}
		}
		catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
		{
			throw new PrintException(ExceptionMessages.ImagePngDataDamaged, exception);
		}
	}

	/// <summary>
	/// Checks whether a PNG's image data can go into a PDF as it is: 8-bit grey or colour, opaque, not interlaced.
	/// </summary>
	/// <param name="png">The PNG.</param>
	/// <returns>Whether it can.</returns>
	internal static bool CanPassThrough(PngHeader png)
	{
		ArgumentNullException.ThrowIfNull(png);

		return !png.Interlaced && png.BitDepth == 8 && png.ColorType is 0 or 2 && png.Transparency is null;
	}

	/// <summary>
	/// Decodes the rows of the whole image, or of one interlacing pass, unfiltering each against the one before.
	/// </summary>
	/// <param name="png">The PNG.</param>
	/// <param name="inflater">The decompressed image data, read from where this pass starts.</param>
	/// <param name="width">The pass's width in pixels.</param>
	/// <param name="height">The pass's height in pixels.</param>
	/// <param name="row">Receives each row's number in the pass and its 8-bit output pixels.</param>
	/// <exception cref="EndOfStreamException">The data ends early.</exception>
	/// <exception cref="InvalidDataException">The data can't be decompressed, or a row's filter type is unknown.</exception>
	private static void DecodePass(PngHeader png, Stream inflater, int width, int height, PngRowAction row)
	{
		int bitsPerPixel = png.Channels * png.BitDepth;
		int stride = (int)(((long)width * bitsPerPixel + 7) / 8);
		int bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
		byte[] previous = new byte[stride];
		byte[] current = new byte[stride];
		byte[] filter = new byte[1];
		byte[] pixels = new byte[width * (png.IsGrey ? 1 : 3)];

		for (int y = 0; y < height; y++)
		{
			inflater.ReadExactly(filter);
			inflater.ReadExactly(current);
			PngDecoder.Unfilter(filter[0], current, previous, bytesPerPixel);
			PngDecoder.ConvertRow(png, current, width, pixels);
			row(y, pixels);

			(previous, current) = (current, previous);
		}
	}

	/// <summary>
	/// Undoes a row's filter in place (PNG specification, 7.3 and 9).
	/// </summary>
	/// <param name="type">The filter type: 0 none, 1 sub, 2 up, 3 average, 4 Paeth.</param>
	/// <param name="row">The row, filtered; unfiltered on return.</param>
	/// <param name="previous">The row before, unfiltered; all zeros for the first.</param>
	/// <param name="bytesPerPixel">The bytes in a pixel, at least 1.</param>
	/// <exception cref="InvalidDataException">The filter type is unknown.</exception>
	private static void Unfilter(byte type, Span<byte> row, ReadOnlySpan<byte> previous, int bytesPerPixel)
	{
		switch (type)
		{
			case 0:
				break;

			case 1:
				for (int index = bytesPerPixel; index < row.Length; index++)
				{
					row[index] += row[index - bytesPerPixel];
				}

				break;

			case 2:
				for (int index = 0; index < row.Length; index++)
				{
					row[index] += previous[index];
				}

				break;

			case 3:
				for (int index = 0; index < row.Length; index++)
				{
					int left = index >= bytesPerPixel ? row[index - bytesPerPixel] : 0;
					row[index] += (byte)((left + previous[index]) / 2);
				}

				break;

			case 4:
				for (int index = 0; index < row.Length; index++)
				{
					int left = index >= bytesPerPixel ? row[index - bytesPerPixel] : 0;
					int upperLeft = index >= bytesPerPixel ? previous[index - bytesPerPixel] : 0;
					row[index] += PngDecoder.Paeth(left, previous[index], upperLeft);
				}

				break;

			default:
				throw new InvalidDataException();
		}
	}

	/// <summary>
	/// Predicts a byte from its neighbours as the Paeth filter does: whichever of left, up and upper left is
	/// nearest their sum less the upper left.
	/// </summary>
	/// <param name="left">The byte to the left.</param>
	/// <param name="up">The byte above.</param>
	/// <param name="upperLeft">The byte above and to the left.</param>
	/// <returns>The prediction.</returns>
	private static byte Paeth(int left, int up, int upperLeft)
	{
		int estimate = left + up - upperLeft;
		int toLeft = Math.Abs(estimate - left);
		int toUp = Math.Abs(estimate - up);
		int toUpperLeft = Math.Abs(estimate - upperLeft);

		if (toLeft <= toUp && toLeft <= toUpperLeft)
		{
			return (byte)left;
		}

		return toUp <= toUpperLeft ? (byte)up : (byte)upperLeft;
	}

	/// <summary>
	/// Turns an unfiltered row into 8-bit grey or colour pixels, scaling samples to 8 bits, looking up palette
	/// entries, and laying transparency over white.
	/// </summary>
	/// <param name="png">The PNG.</param>
	/// <param name="row">The unfiltered row.</param>
	/// <param name="width">The pixels in it.</param>
	/// <param name="output">The 8-bit pixels: one byte each in grey, three in colour.</param>
	private static void ConvertRow(PngHeader png, ReadOnlySpan<byte> row, int width, Span<byte> output)
	{
		byte[]? transparency = png.Transparency;

		for (int x = 0; x < width; x++)
		{
			switch (png.ColorType)
			{
				case 0:
				{
					int grey = PngDecoder.Sample(row, png.BitDepth, x);
					bool clear = transparency is { Length: >= 2 } && grey == ((transparency[0] << 8) | transparency[1]);
					output[x] = clear ? (byte)255 : PngDecoder.To8Bits(grey, png.BitDepth);
					break;
				}

				case 2:
				{
					int red = PngDecoder.Sample(row, png.BitDepth, x * 3);
					int green = PngDecoder.Sample(row, png.BitDepth, (x * 3) + 1);
					int blue = PngDecoder.Sample(row, png.BitDepth, (x * 3) + 2);
					bool clear = transparency is { Length: >= 6 }
								 && red == ((transparency[0] << 8) | transparency[1])
								 && green == ((transparency[2] << 8) | transparency[3])
								 && blue == ((transparency[4] << 8) | transparency[5]);

					output[x * 3] = clear ? (byte)255 : PngDecoder.To8Bits(red, png.BitDepth);
					output[(x * 3) + 1] = clear ? (byte)255 : PngDecoder.To8Bits(green, png.BitDepth);
					output[(x * 3) + 2] = clear ? (byte)255 : PngDecoder.To8Bits(blue, png.BitDepth);
					break;
				}

				case 3:
				{
					int index = PngDecoder.Sample(row, png.BitDepth, x);
					byte[] palette = png.Palette!;

					// An index past the palette's end is an error the specification lets decoders treat as black
					bool inPalette = (index * 3) + 2 < palette.Length;
					int alpha = transparency is not null && index < transparency.Length ? transparency[index] : 255;

					output[x * 3] = PngDecoder.OverWhite(inPalette ? palette[index * 3] : 0, alpha);
					output[(x * 3) + 1] = PngDecoder.OverWhite(inPalette ? palette[(index * 3) + 1] : 0, alpha);
					output[(x * 3) + 2] = PngDecoder.OverWhite(inPalette ? palette[(index * 3) + 2] : 0, alpha);
					break;
				}

				case 4:
				{
					int grey = PngDecoder.To8Bits(PngDecoder.Sample(row, png.BitDepth, x * 2), png.BitDepth);
					int alpha = PngDecoder.To8Bits(PngDecoder.Sample(row, png.BitDepth, (x * 2) + 1), png.BitDepth);
					output[x] = PngDecoder.OverWhite(grey, alpha);
					break;
				}

				default:
				{
					int alpha = PngDecoder.To8Bits(PngDecoder.Sample(row, png.BitDepth, (x * 4) + 3), png.BitDepth);

					for (int channel = 0; channel < 3; channel++)
					{
						int value = PngDecoder.To8Bits(PngDecoder.Sample(row, png.BitDepth, (x * 4) + channel), png.BitDepth);
						output[(x * 3) + channel] = PngDecoder.OverWhite(value, alpha);
					}

					break;
				}
			}
		}
	}

	/// <summary>
	/// Reads a sample from an unfiltered row: packed from the high bits for depths below 8, two bytes, most
	/// significant first, for 16.
	/// </summary>
	/// <param name="row">The row.</param>
	/// <param name="bitDepth">The bits in each sample.</param>
	/// <param name="index">The sample's number in the row.</param>
	/// <returns>The sample's value.</returns>
	private static int Sample(ReadOnlySpan<byte> row, int bitDepth, int index)
	{
		switch (bitDepth)
		{
			case 8:
				return row[index];

			case 16:
				return (row[index * 2] << 8) | row[(index * 2) + 1];

			default:
				int bit = index * bitDepth;
				int shift = 8 - bitDepth - (bit % 8);

				return (row[bit / 8] >> shift) & ((1 << bitDepth) - 1);
		}
	}

	/// <summary>
	/// Scales a sample to 8 bits, so its lowest value is 0 and its highest 255.
	/// </summary>
	/// <param name="value">The sample.</param>
	/// <param name="bitDepth">Its bits.</param>
	/// <returns>The 8-bit value.</returns>
	private static byte To8Bits(int value, int bitDepth)
	{
		return bitDepth switch
		{
			8  => (byte)value,
			16 => (byte)(((value * 255) + 32767) / 65535),
			_  => (byte)(value * 255 / ((1 << bitDepth) - 1)),
		};
	}

	/// <summary>
	/// Lays a value over white paper.
	/// </summary>
	/// <param name="value">The 8-bit value.</param>
	/// <param name="alpha">Its opacity, 0 clear to 255 opaque.</param>
	/// <returns>What shows on white paper.</returns>
	private static byte OverWhite(int value, int alpha)
	{
		return (byte)(((value * alpha) + (255 * (255 - alpha)) + 127) / 255);
	}
}
