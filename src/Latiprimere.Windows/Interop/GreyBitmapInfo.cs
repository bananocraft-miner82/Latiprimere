using System.Runtime.InteropServices;

namespace Latiprimere.Windows.Interop;

/// <summary>
/// Mirrors <c>BITMAPINFO</c> for an 8-bit bitmap: the header, then a colour table of the 256 greys, black to white,
/// so each byte of a grey band is its own grey (ADR 0054).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct GreyBitmapInfo
{
	/// <summary>The number of colours in the table.</summary>
	internal const int ColorCount = 256;

	/// <summary><c>bmiHeader</c>.</summary>
	public BitmapInfoHeader Header;

	/// <summary><c>bmiColors</c>: <c>RGBQUAD</c>s, as 0x00RRGGBB.</summary>
	public fixed uint Colors[GreyBitmapInfo.ColorCount];

	/// <summary>
	/// Creates the bitmap information for a top-down band of grey.
	/// </summary>
	/// <param name="width">The band's width in pixels.</param>
	/// <param name="height">The band's rows.</param>
	/// <returns>The bitmap information.</returns>
	internal static GreyBitmapInfo Create(int width, int height)
	{
		GreyBitmapInfo info = new GreyBitmapInfo
		{
			Header = new BitmapInfoHeader
			{
				Size        = (uint)sizeof(BitmapInfoHeader),
				Width       = width,
				Height      = -height,
				Planes      = 1,
				BitCount    = GdiConstants.GreyBitsPerPixel,
				Compression = GdiConstants.BitmapUncompressed,
				ColorsUsed  = GreyBitmapInfo.ColorCount
			}
		};

		for (uint grey = 0; grey < GreyBitmapInfo.ColorCount; grey++)
		{
			info.Colors[grey] = (grey << 16) | (grey << 8) | grey;
		}

		return info;
	}
}
