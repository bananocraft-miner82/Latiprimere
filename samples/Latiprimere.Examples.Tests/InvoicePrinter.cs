using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples;

/// <summary>
/// Prints invoices: the code under test in "Testing code that prints". It depends only on
/// <see cref="IPrinterCatalog"/>, so a test can hand it a fake catalog instead of real printers.
/// </summary>
internal sealed class InvoicePrinter
{
	private readonly IPrinterCatalog catalog; // Where printers are looked up

	/// <summary>
	/// Initialises a new instance of the <see cref="InvoicePrinter"/> class.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
	public InvoicePrinter(IPrinterCatalog catalog)
	{
		ArgumentNullException.ThrowIfNull(catalog);

		this.catalog = catalog;
	}

	/// <summary>
	/// Gets how long to wait between checks on a job. Tests make it short, so they don't wait.
	/// </summary>
	public TimeSpan PollInterval { get; init; } = PrintJobExtensions.DefaultPollInterval;

	/// <summary>
	/// Prints an invoice two-sided, on the saved printer or, if that has gone, the default printer, and waits for the
	/// job to finish.
	/// </summary>
	/// <param name="printerId">The ID of the printer the user chose.</param>
	/// <param name="invoiceNumber">The invoice number, which names the job in the print queue.</param>
	/// <param name="invoicePdf">The invoice, as a PDF.</param>
	/// <param name="cancellationToken">A token to stop waiting. It doesn't cancel a job already sent.</param>
	/// <returns><see langword="true"/> if the invoice printed; <see langword="false"/> if the job was cancelled or
	/// aborted.</returns>
	/// <exception cref="PrinterNotFoundException">The saved printer has gone and there is no default
	/// printer.</exception>
	/// <exception cref="PrintException">The print system turned the job away or failed.</exception>
	public async Task<bool> PrintAsync(string printerId, string invoiceNumber, Stream invoicePdf, CancellationToken cancellationToken)
	{
		IPrinter printer = await this.FindPrinterAsync(printerId, cancellationToken);
		PrintOptions options = new PrintOptions
		{
			JobName = $"Invoice {invoiceNumber}",
			Duplex = DuplexMode.LongEdge,
		};

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(invoicePdf), options, cancellationToken);

		// Follow the job until it finishes, and keep the last thing it reported
		PrintJobStatus? finalStatus = null;

		await foreach (PrintJobStatus status in job.WatchStatusAsync(this.PollInterval, cancellationToken))
		{
			finalStatus = status;
		}

		return finalStatus?.State == PrintJobState.Completed;
	}

	/// <summary>
	/// Looks up the saved printer, falling back to the default printer if it has gone.
	/// </summary>
	/// <param name="printerId">The ID of the saved printer.</param>
	/// <param name="cancellationToken">A token to cancel the lookup.</param>
	/// <returns>The printer to use.</returns>
	/// <exception cref="PrinterNotFoundException">The saved printer has gone and there is no default
	/// printer.</exception>
	private async Task<IPrinter> FindPrinterAsync(string printerId, CancellationToken cancellationToken)
	{
		try
		{
			return await this.catalog.GetPrinterAsync(printerId, cancellationToken);
		}
		catch (PrinterNotFoundException)
		{
			IPrinter? defaultPrinter = await this.catalog.GetDefaultPrinterAsync(cancellationToken);

			if (defaultPrinter is null)
			{
				throw;
			}

			return defaultPrinter;
		}
	}
}
