namespace Latiprimere;

/// <summary>
/// A platform print system, such as the Windows spooler or CUPS, implemented by a Latiprimere platform adapter.
/// </summary>
/// <remarks>
/// <para>
/// Applications normally use <see cref="IPrinterCatalog"/>, which combines every supported print system. Its
/// printers are its own, around a print system's, and refuse a job that sets a <see cref="PrintOptions"/> setting
/// the print system's printer doesn't apply (<see cref="IPrinter.AppliedOptions"/>); a printer from a print system
/// used directly has no such check. Printer identifiers returned by a print system must not clash with those of other
/// print systems.
/// </para>
/// <para>
/// Print systems may hold connections, such as the HTTP client of the CUPS and IPP print systems, so dispose them
/// when finished. A dependency injection container does that for the print systems it creates; an application that
/// creates them with a factory disposes them itself. Printers and jobs obtained from a print system stop working
/// once it is disposed.
/// </para>
/// </remarks>
public interface IPrintSystem : IDisposable
{
	/// <summary>
	/// Gets the name of the print system, used in diagnostics.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Gets a value indicating whether the print system can be used on the current platform.
	/// </summary>
	bool IsSupported { get; }

	/// <summary>
	/// Gets information about every printer in this print system.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The printers. Empty if the print system has none.</returns>
	/// <exception cref="PrintException">The print system failed while listing printers.</exception>
	Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Looks for a printer by its identifier.
	/// </summary>
	/// <param name="printerId">The printer identifier.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The printer, or <see langword="null"/> if this print system has no printer with that
	/// identifier.</returns>
	/// <exception cref="PrintException">The print system failed while looking up the printer.</exception>
	Task<IPrinter?> FindPrinterAsync(string printerId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Looks for this print system's default printer.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The default printer, or <see langword="null"/> if none is set.</returns>
	/// <exception cref="PrintException">The print system failed while looking up the default printer.</exception>
	Task<IPrinter?> FindDefaultPrinterAsync(CancellationToken cancellationToken = default);
}
