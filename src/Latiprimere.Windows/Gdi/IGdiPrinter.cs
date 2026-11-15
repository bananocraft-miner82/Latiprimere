namespace Latiprimere.Windows.Gdi;

/// <summary>
/// Prints rendered documents through GDI. Keeps the rest of the adapter free of GDI and lets tests substitute a fake.
/// </summary>
internal interface IGdiPrinter
{
	/// <summary>
	/// Renders every page of a document onto a printer device context and spools the job. Blocks until the job has
	/// been spooled.
	/// </summary>
	/// <param name="request">What to print, where and how.</param>
	/// <param name="cancellationToken">A token to abandon spooling; the partial job is removed.</param>
	/// <returns>The spooler's job ID.</returns>
	/// <exception cref="PrintException">GDI or the spooler failed; see <see cref="Mapping.WindowsErrorMapper"/>.</exception>
	/// <exception cref="OperationCanceledException">Spooling was cancelled.</exception>
	int PrintRasterDocument(GdiPrintRequest request, CancellationToken cancellationToken);
}
