using Latiprimere.Windows.Resources;

namespace Latiprimere.Windows.Gdi;

/// <summary>
/// Stands in for GDI on platforms other than Windows, where the print system reports itself as unsupported.
/// </summary>
internal sealed class UnavailableGdiPrinter : IGdiPrinter
{
	/// <inheritdoc/>
	/// <exception cref="PlatformNotSupportedException">Always.</exception>
	public int PrintRasterDocument(GdiPrintRequest request, CancellationToken cancellationToken)
	{
		throw new PlatformNotSupportedException(ExceptionMessages.WindowsOnly);
	}
}
