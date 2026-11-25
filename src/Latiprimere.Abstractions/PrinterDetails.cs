namespace Latiprimere;

/// <summary>
/// What a printer reports about itself beyond printing: its web pages, pictures of it, its firmware, and how it can be
/// identified.
/// </summary>
/// <remarks>
/// <para>
/// An empty list, or a <see langword="null"/> value, means the print system didn't say. IPP Everywhere printers report
/// most of these; the Windows spooler reports none, so a Windows queue has them only through its IPP printer.
/// </para>
/// <para>
/// The web addresses come from the printer and are only <c>http</c> or <c>https</c>. Treat them as the printer's own
/// say-so: an application that fetches an icon should expect it from the printer's host, and not trust it more than
/// it trusts the printer.
/// </para>
/// </remarks>
public sealed record PrinterDetails
{
	/// <summary>
	/// Gets the identifier of the printer described, as in <see cref="PrinterInfo.Id"/>.
	/// </summary>
	public required string PrinterId { get; init; }

	/// <summary>
	/// Gets the printer's own web page, usually where it is set up, or <see langword="null"/> if not reported. For a
	/// CUPS queue, it is the queue's page on the CUPS server.
	/// </summary>
	public Uri? WebPage { get; init; }

	/// <summary>
	/// Gets a web page where the printer's supplies, such as ink or toner, are shown or ordered, or
	/// <see langword="null"/> if not reported.
	/// </summary>
	public Uri? SuppliesWebPage { get; init; }

	/// <summary>
	/// Gets pictures of the printer, smallest first, as IPP Everywhere printers list them: 48, 128 and 512 pixels
	/// square, as PNG.
	/// </summary>
	public IReadOnlyList<Uri> Icons { get; init; } = Array.Empty<Uri>();

	/// <summary>
	/// Gets the parts of the printer's firmware and their versions.
	/// </summary>
	public IReadOnlyList<PrinterFirmware> Firmware { get; init; } = Array.Empty<PrinterFirmware>();

	/// <summary>
	/// Gets the ways the printer can make itself known with <see cref="IPrinter.IdentifyAsync"/>. Empty if it can't be
	/// identified, or didn't say.
	/// </summary>
	public IReadOnlyList<IdentifyAction> IdentifyActions { get; init; } = Array.Empty<IdentifyAction>();
}
