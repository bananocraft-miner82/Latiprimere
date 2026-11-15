namespace Latiprimere.Windows.Gdi;

/// <summary>
/// A pixel buffer reused across pages, grown only when a page needs a larger band.
/// </summary>
internal sealed class BandBuffer
{
	private byte[] pixels = Array.Empty<byte>(); // The current buffer

	/// <summary>
	/// Returns a buffer of at least the given size.
	/// </summary>
	/// <param name="byteCount">The bytes needed.</param>
	/// <returns>The buffer.</returns>
	public byte[] Get(long byteCount)
	{
		if (this.pixels.Length < byteCount)
		{
			this.pixels = new byte[byteCount];
		}

		return this.pixels;
	}
}
