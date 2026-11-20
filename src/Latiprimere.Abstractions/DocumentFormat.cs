namespace Latiprimere;

/// <summary>
/// Enumerates the document formats that can be submitted to a printer.
/// </summary>
/// <remarks>
/// Later versions may add formats, each with its own <see cref="PrintContent"/> factory. A print system that doesn't take one throws <see cref="NotSupportedException"/> from
/// <see cref="IPrinter.SubmitAsync"/>, so code that switches over these values should expect others.
/// </remarks>
public enum DocumentFormat
{
	/// <summary>
	/// A PDF document. The print system converts it for the printer where necessary.
	/// </summary>
	Pdf = 0,

	/// <summary>
	/// Raw printer-language data, such as ZPL, ESC/POS or PCL, sent to the printer unchanged.
	/// </summary>
	Raw = 1,

	/// <summary>
	/// A JPEG image, printed on one page as <see cref="PrintOptions.Scaling"/> says. The print system converts it for
	/// the printer where necessary.
	/// </summary>
	Jpeg = 2,

	/// <summary>
	/// A PNG image, printed on one page as <see cref="PrintOptions.Scaling"/> says, with any transparency over white.
	/// The print system converts it for the printer where necessary.
	/// </summary>
	Png = 3,

	/// <summary>
	/// Plain text, laid out on pages in a fixed-width font. The print system lays it out, or converts it for the
	/// printer.
	/// </summary>
	Text = 4,
}
