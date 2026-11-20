namespace Latiprimere.Ipp.Documents;

/// <summary>
/// The CRC-32 a PNG chunk ends with, over its type and data (PNG specification, third edition, 5.5 and Annex D).
/// </summary>
internal static class PngCrc
{
	private static readonly uint[] Table = PngCrc.CreateTable(); // The CRC of each byte value

	/// <summary>
	/// Computes the CRC of some bytes.
	/// </summary>
	/// <param name="bytes">The bytes: a chunk's type, then its data.</param>
	/// <returns>The CRC.</returns>
	public static uint Compute(ReadOnlySpan<byte> bytes)
	{
		uint crc = 0xFFFFFFFF;

		foreach (byte value in bytes)
		{
			crc = PngCrc.Table[(crc ^ value) & 0xFF] ^ (crc >> 8);
		}

		return crc ^ 0xFFFFFFFF;
	}

	/// <summary>
	/// Builds the table for the reflected polynomial 0xEDB88320.
	/// </summary>
	/// <returns>The table.</returns>
	private static uint[] CreateTable()
	{
		uint[] table = new uint[256];

		for (uint value = 0; value < 256; value++)
		{
			uint crc = value;

			for (int bit = 0; bit < 8; bit++)
			{
				crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
			}

			table[value] = crc;
		}

		return table;
	}
}
