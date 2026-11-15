using System.Runtime.InteropServices;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Mirrors <c>BITMAPINFOHEADER</c>. For 32-bit uncompressed bitmaps no colour table follows, so this header alone
/// can be passed where a <c>BITMAPINFO</c> is expected.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfoHeader
{
	/// <summary><c>biSize</c>: the size of this structure.</summary>
	public uint Size;

	/// <summary><c>biWidth</c>.</summary>
	public int Width;

	/// <summary><c>biHeight</c>: negative for a top-down bitmap.</summary>
	public int Height;

	/// <summary><c>biPlanes</c>: always 1.</summary>
	public ushort Planes;

	/// <summary><c>biBitCount</c>.</summary>
	public ushort BitCount;

	/// <summary><c>biCompression</c>: BI_RGB for uncompressed.</summary>
	public uint Compression;

	/// <summary><c>biSizeImage</c>: may be zero for uncompressed bitmaps.</summary>
	public uint SizeImage;

	/// <summary><c>biXPelsPerMeter</c>.</summary>
	public int XPixelsPerMeter;

	/// <summary><c>biYPelsPerMeter</c>.</summary>
	public int YPixelsPerMeter;

	/// <summary><c>biClrUsed</c>.</summary>
	public uint ColorsUsed;

	/// <summary><c>biClrImportant</c>.</summary>
	public uint ColorsImportant;
}
