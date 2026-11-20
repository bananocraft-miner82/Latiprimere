namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Receives a decoded PNG row.
/// </summary>
/// <param name="y">The row's number in the image, or in the interlacing pass.</param>
/// <param name="pixels">Its 8-bit pixels, valid only during the call.</param>
internal delegate void PngRowAction(int y, ReadOnlySpan<byte> pixels);
