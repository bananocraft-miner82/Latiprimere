namespace Latiprimere.Windows.Gdi;

/// <summary>
/// A printer device context's geometry, in device pixels, as reported by <c>GetDeviceCaps</c>.
/// </summary>
/// <param name="DpiX">Horizontal dots per inch.</param>
/// <param name="DpiY">Vertical dots per inch.</param>
/// <param name="PaperWidth">The whole paper's width.</param>
/// <param name="PaperHeight">The whole paper's height.</param>
/// <param name="OffsetX">The unprintable margin on the left: device coordinate 0 is this far from the paper's
/// edge.</param>
/// <param name="OffsetY">The unprintable margin at the top.</param>
internal readonly record struct PrintableArea
(
	int DpiX,
	int DpiY,
	int PaperWidth,
	int PaperHeight,
	int OffsetX,
	int OffsetY
);
