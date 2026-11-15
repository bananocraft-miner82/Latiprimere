using Latiprimere.Rendering;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows;

/// <summary>
/// The services shared by the Windows print system and its printers and jobs.
/// </summary>
internal sealed record WindowsPrintContext
{
	/// <summary>Gets the spooler.</summary>
	public required IWinSpool Spool { get; init; }

	/// <summary>Gets the GDI printer used for PDF documents.</summary>
	public required IGdiPrinter Gdi { get; init; }

	/// <summary>Gets the PDF rasteriser, or <see langword="null"/> if none is registered, in which case PDF is not
	/// supported.</summary>
	public IPdfRasterizer? PdfRasterizer { get; init; }

	/// <summary>Gets the clock used to timestamp job statuses.</summary>
	public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

	/// <summary>
	/// Gets a value indicating whether a printer name may name any printer, not only a listed one, when it has a
	/// backslash or a comma (<see cref="WindowsOptions.AllowAnyPrinterName"/>).
	/// </summary>
	public bool AllowsAnyPrinterName { get; init; }
}
