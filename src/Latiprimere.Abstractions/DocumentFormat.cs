namespace Latiprimere;

/// <summary>
/// Enumerates the document formats that can be submitted to a printer.
/// </summary>
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
}
