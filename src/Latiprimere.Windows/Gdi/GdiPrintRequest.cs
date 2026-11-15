using Latiprimere.Rendering;

namespace Latiprimere.Windows.Gdi;

/// <summary>
/// A rendered document to send to a printer through GDI.
/// </summary>
internal sealed record GdiPrintRequest
{
	/// <summary>Gets the spooler's name for the printer.</summary>
	public required string PrinterName { get; init; }

	/// <summary>Gets the name shown in the print queue.</summary>
	public required string DocumentName { get; init; }

	/// <summary>Gets a file to print to instead of the printer's port, or <see langword="null"/>.</summary>
	public string? OutputFile { get; init; }

	/// <summary>Gets the document to render.</summary>
	public required IRasterDocument Document { get; init; }

	/// <summary>Gets how to print it.</summary>
	public required PrintOptions Options { get; init; }
}
