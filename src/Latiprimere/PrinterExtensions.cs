namespace Latiprimere;

/// <summary>
/// Provides helper methods for following printers.
/// </summary>
public static class PrinterExtensions
{
	/// <summary>
	/// The interval between state checks used when none is specified, for print systems that can't say when a
	/// printer's state changes. Printers change less often than jobs, so it is longer than
	/// <see cref="PrintJobExtensions.DefaultPollInterval"/>.
	/// </summary>
	public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Follows a printer's state until cancelled, as <see cref="IPrinter.WatchStatusAsync"/> does, checking it every
	/// <see cref="DefaultPollInterval"/> when the print system can't say when it changes.
	/// </summary>
	/// <param name="printer">The printer to follow.</param>
	/// <param name="cancellationToken">A token to stop following the printer.</param>
	/// <returns>The printer's status, then each change of it.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="printer"/> is <see langword="null"/>.</exception>
	public static IAsyncEnumerable<PrinterStatus> WatchStatusAsync(this IPrinter printer, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(printer);

		return printer.WatchStatusAsync(PrinterExtensions.DefaultPollInterval, cancellationToken);
	}
}
