namespace Latiprimere;

/// <summary>
/// Finds the printers available to the application.
/// </summary>
/// <remarks>
/// The catalog, and the printers and jobs it returns, are safe to use from several threads at once. Register it as
/// a singleton: it keeps no state between calls beyond what its print systems keep.
/// </remarks>
public interface IPrinterCatalog
{
	/// <summary>
	/// Gets information about every available printer.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The available printers. Empty if no print system has any. Printers from a print system that failed are
	/// missing; implementations log such failures.
	/// </returns>
	/// <exception cref="PrintSystemUnavailableException">No supported print system is available.</exception>
	/// <exception cref="PrintException">Every print system failed while listing printers.</exception>
	Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets a printer by its identifier.
	/// </summary>
	/// <param name="printerId">The printer identifier, as given by <see cref="PrinterInfo.Id"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The printer.</returns>
	/// <exception cref="ArgumentException"><paramref name="printerId"/> is <see langword="null"/>, empty or
	/// whitespace.</exception>
	/// <exception cref="PrinterNotFoundException">No printer has the given identifier.</exception>
	/// <exception cref="PrintSystemUnavailableException">No supported print system is available.</exception>
	/// <exception cref="PrintException">The print system that owns the identifier failed while looking it
	/// up.</exception>
	Task<IPrinter> GetPrinterAsync(string printerId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the default printer.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// The default printer of the first print system that has one, or <see langword="null"/> if none does.
	/// Print systems that fail are skipped; implementations log such failures.
	/// </returns>
	/// <exception cref="PrintSystemUnavailableException">No supported print system is available.</exception>
	/// <exception cref="PrintException">Every print system failed while looking for a default printer.</exception>
	Task<IPrinter?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default);
}
