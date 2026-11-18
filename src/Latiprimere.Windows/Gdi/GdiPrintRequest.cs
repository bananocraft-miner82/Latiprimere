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

	/// <summary>Gets the DMPAPER_* number of the chosen paper size, or <see langword="null"/> to keep the
	/// printer's.</summary>
	public short? PaperId { get; init; }

	/// <summary>Gets the DMBIN_* number of the chosen tray, or <see langword="null"/> to keep the printer's.</summary>
	public short? BinId { get; init; }

	/// <summary>Gets a DEVMODE saved from the driver (<c>windows:devmode</c>), with its private data, to start from
	/// instead of the driver's defaults; or <see langword="null"/>.</summary>
	public byte[]? SavedDevMode { get; init; }

	/// <summary>Gets Print Tickets, as UTF-8 XML, to merge in order onto the saved or default DEVMODE through the
	/// driver before the options are applied: the one from <c>windows:print-ticket</c>, then one made from the
	/// <c>windows:feature:</c> and <c>windows:parameter:</c> keys. Empty if there are none.</summary>
	public IReadOnlyList<byte[]> PrintTickets { get; init; } = Array.Empty<byte[]>();
}
