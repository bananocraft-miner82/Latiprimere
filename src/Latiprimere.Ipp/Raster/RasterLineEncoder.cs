using System.Buffers;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// The line compression PWG raster and Apple raster share (PWG 5102.4, section 4.3): a byte saying how many times
/// the line repeats, then the line as runs of repeated pixels and runs of different ones.
/// </summary>
/// <remarks>
/// <para>
/// A "pixel" here is the unit the format compresses: one byte for 1-bit black (eight pixels) and for 8-bit grey, and
/// three bytes for 24-bit colour.
/// </para>
/// <para>
/// After the repeat byte, each run starts with a control byte: 0 to 127 means the next pixel repeated that many
/// times plus one (1 to 128); 129 to 255 means 257 minus that many different pixels follow (2 to 128). 128 is never
/// written: Apple raster reads it as "the rest of the line is white", and PWG raster leaves it undefined.
/// </para>
/// </remarks>
internal static class RasterLineEncoder
{
	/// <summary>The most lines one repeat byte can stand for.</summary>
	internal const int MaxLineRepeat = 256;

	/// <summary>The most pixels one run can hold.</summary>
	internal const int MaxRun = 128;

	/// <summary>The control byte that starts a run of different pixels, minus its length: 257 − length.</summary>
	private const int LiteralBase = 257;

	/// <summary>
	/// Encodes a line, with how many times it repeats.
	/// </summary>
	/// <param name="line">The line's bytes, a whole number of pixels.</param>
	/// <param name="bytesPerPixel">The bytes in one pixel unit: 1 or 3.</param>
	/// <param name="repeat">How many consecutive lines are this one: 1 to <see cref="MaxLineRepeat"/>.</param>
	/// <param name="output">Where the encoded bytes go.</param>
	internal static void Encode(ReadOnlySpan<byte> line, int bytesPerPixel, int repeat, IBufferWriter<byte> output)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(bytesPerPixel, 1);
		ArgumentOutOfRangeException.ThrowIfNotEqual(line.Length % bytesPerPixel, 0);
		ArgumentOutOfRangeException.ThrowIfLessThan(repeat, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(repeat, RasterLineEncoder.MaxLineRepeat);
		ArgumentNullException.ThrowIfNull(output);

		RasterLineEncoder.WriteByte(output, (byte)(repeat - 1));

		int pixels = line.Length / bytesPerPixel;
		int index = 0;

		while (index < pixels)
		{
			int run = RasterLineEncoder.CountRepeats(line, bytesPerPixel, index, pixels);

			if (run >= 2)
			{
				// A run of one pixel repeated
				RasterLineEncoder.WriteByte(output, (byte)(run - 1));
				output.Write(line.Slice(index * bytesPerPixel, bytesPerPixel));
				index += run;

				continue;
			}

			// Different pixels, up to where a repeated run starts
			int start = index;
			int count = 0;

			while (index < pixels && count < RasterLineEncoder.MaxRun
				   && RasterLineEncoder.CountRepeats(line, bytesPerPixel, index, pixels) < 2)
			{
				index++;
				count++;
			}

			// One different pixel is written as a run of one, since a literal run holds at least two
			RasterLineEncoder.WriteByte(output, count == 1 ? (byte)0 : (byte)(RasterLineEncoder.LiteralBase - count));
			output.Write(line.Slice(start * bytesPerPixel, count * bytesPerPixel));
		}
	}

	/// <summary>
	/// Counts how many pixels from a position are the same as the first, up to <see cref="MaxRun"/>.
	/// </summary>
	/// <param name="line">The line.</param>
	/// <param name="bytesPerPixel">The bytes in a pixel unit.</param>
	/// <param name="index">The first pixel.</param>
	/// <param name="pixels">The pixels in the line.</param>
	/// <returns>The run's length, at least 1.</returns>
	private static int CountRepeats(ReadOnlySpan<byte> line, int bytesPerPixel, int index, int pixels)
	{
		ReadOnlySpan<byte> first = line.Slice(index * bytesPerPixel, bytesPerPixel);
		int run = 1;

		while (index + run < pixels
			   && run < RasterLineEncoder.MaxRun
			   && line.Slice((index + run) * bytesPerPixel, bytesPerPixel).SequenceEqual(first))
		{
			run++;
		}

		return run;
	}

	/// <summary>
	/// Writes one byte.
	/// </summary>
	/// <param name="output">Where to.</param>
	/// <param name="value">The byte.</param>
	private static void WriteByte(IBufferWriter<byte> output, byte value)
	{
		output.GetSpan(1)[0] = value;
		output.Advance(1);
	}
}
