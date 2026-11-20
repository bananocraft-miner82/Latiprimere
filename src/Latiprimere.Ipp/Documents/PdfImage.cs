namespace Latiprimere.Ipp.Documents;

/// <summary>
/// An image for <see cref="PdfDocumentWriter.DrawImage(PdfImage, PdfMatrix)"/>: its size, colour space and samples, which are written
/// as the image is drawn, so they needn't be held whole.
/// </summary>
internal sealed record PdfImage
{
	/// <summary>Gets the image's width in samples.</summary>
	public required int Width { get; init; }

	/// <summary>Gets the image's height in samples.</summary>
	public required int Height { get; init; }

	/// <summary>Gets the image's colour space.</summary>
	public required PdfColorSpace ColorSpace { get; init; }

	/// <summary>Gets the bits in each sample: 1, 2, 4, 8 or 16. Defaults to 8.</summary>
	public int BitsPerComponent { get; init; } = 8;

	/// <summary>
	/// Gets how the writer compresses the samples. Defaults to <see cref="PdfStreamEncoding.Flate"/>;
	/// <see cref="PdfStreamEncoding.None"/> for data compressed already, which names its <see cref="Filter"/>.
	/// </summary>
	public PdfStreamEncoding Encoding { get; init; } = PdfStreamEncoding.Flate;

	/// <summary>
	/// Gets the filter of data compressed already and written as it is, such as <c>DCTDecode</c> for a JPEG, or
	/// <see langword="null"/>. Only with <see cref="PdfStreamEncoding.None"/>.
	/// </summary>
	public string? Filter { get; init; }

	/// <summary>
	/// Gets the filter's parameters, as a PDF dictionary such as <c>&lt;&lt; /Predictor 15 /Columns 64 &gt;&gt;</c>, or
	/// <see langword="null"/>.
	/// </summary>
	public string? DecodeParameters { get; init; }

	/// <summary>
	/// Gets how samples map to colour values, as a PDF array such as <c>[1 0 1 0 1 0 1 0]</c> for a CMYK JPEG stored
	/// inverted, or <see langword="null"/> for the usual mapping.
	/// </summary>
	public string? Decode { get; init; }

	/// <summary>
	/// Gets what writes the samples, row by row from the top, or the compressed data, to the stream it is given.
	/// </summary>
	public required Action<Stream> WriteData { get; init; }
}
