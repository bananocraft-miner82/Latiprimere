namespace Latiprimere.Ipp.Documents;

/// <summary>
/// The colour space of a <see cref="PdfImage"/>.
/// </summary>
internal enum PdfColorSpace
{
	/// <summary>One component, from black to white: <c>DeviceGray</c>.</summary>
	Gray = 0,

	/// <summary>Three components, red, green and blue: <c>DeviceRGB</c>.</summary>
	Rgb = 1,

	/// <summary>Four components, cyan, magenta, yellow and black: <c>DeviceCMYK</c>, as some JPEGs are.</summary>
	Cmyk = 2,
}
