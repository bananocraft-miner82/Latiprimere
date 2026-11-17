using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples.Tests.Fakes;

/// <summary>
/// A printer catalog that holds a fixed set of fake printers, in place of the real print systems.
/// </summary>
internal sealed class FakePrinterCatalog : IPrinterCatalog
{
	private readonly List<FakePrinter> printers; // The printers the catalog knows

	/// <summary>
	/// Initialises a new instance of the <see cref="FakePrinterCatalog"/> class.
	/// </summary>
	/// <param name="printers">The printers the catalog knows.</param>
	public FakePrinterCatalog(params IEnumerable<FakePrinter> printers)
	{
		this.printers = printers.ToList();
	}

	/// <summary>
	/// Gets the printer <see cref="GetDefaultPrinterAsync"/> returns, or <see langword="null"/> for none, as on a
	/// server with no default printer.
	/// </summary>
	public FakePrinter? DefaultPrinter { get; init; }

	/// <summary>
	/// Describes every printer the catalog knows.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The printers' descriptions.</returns>
	public Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
	{
		IReadOnlyList<PrinterInfo> descriptions = this.printers.Select(printer => printer.Info).ToList();

		return Task.FromResult(descriptions);
	}

	/// <summary>
	/// Finds a printer by its ID, failing the way the real catalog does when there's no such printer.
	/// </summary>
	/// <param name="printerId">The printer's ID.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The printer.</returns>
	/// <exception cref="PrinterNotFoundException">The catalog has no printer with that ID.</exception>
	public Task<IPrinter> GetPrinterAsync(string printerId, CancellationToken cancellationToken = default)
	{
		FakePrinter? printer = this.printers.Find(candidate => candidate.Info.Id == printerId);

		return printer is null
			? Task.FromException<IPrinter>(PrinterNotFoundException.FromPrinterId(printerId))
			: Task.FromResult<IPrinter>(printer);
	}

	/// <summary>
	/// Returns <see cref="DefaultPrinter"/>.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The default printer, or <see langword="null"/> if there isn't one.</returns>
	public Task<IPrinter?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IPrinter?>(this.DefaultPrinter);
	}
}
