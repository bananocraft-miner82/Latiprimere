namespace Latiprimere.Ipp.Documents;

/// <summary>
/// How <see cref="PdfObjectWriter"/> compresses a stream's data as it writes it, and so the filter it names.
/// </summary>
internal enum PdfStreamEncoding
{
	/// <summary>Written as given: data already compressed, such as a JPEG, names its own filter.</summary>
	None = 0,

	/// <summary>Compressed with zlib, named <c>FlateDecode</c>.</summary>
	Flate = 1,

	/// <summary>Run-length encoded, named <c>RunLengthDecode</c>.</summary>
	RunLength = 2,
}
