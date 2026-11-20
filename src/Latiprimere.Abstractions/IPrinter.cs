namespace Latiprimere;

/// <summary>
/// A printer that can accept print jobs.
/// </summary>
/// <remarks>
/// Safe to use from several threads at once, for example to submit several jobs together.
/// </remarks>
public interface IPrinter
{
	/// <summary>
	/// Gets information about the printer, as read when the printer was looked up.
	/// </summary>
	/// <remarks>
	/// It isn't updated afterwards: to see the printer's current state, look it up again with
	/// <see cref="IPrinterCatalog.GetPrinterAsync"/>.
	/// </remarks>
	PrinterInfo Info { get; }

	/// <summary>
	/// Submits a document to the printer.
	/// </summary>
	/// <param name="content">The document to print.</param>
	/// <param name="options">How to print the document. Use <see cref="PrintOptions.Default"/> for printer
	/// defaults.</param>
	/// <param name="cancellationToken">A token to cancel the submission. Cancelling does not cancel a job once it has
	/// been accepted.</param>
	/// <returns>The submitted job, which can be used to follow its progress.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="options"/> is <see
	/// langword="null"/>.</exception>
	/// <exception cref="NotSupportedException">The printer cannot accept documents in the content's format.</exception>
	/// <exception cref="PrintException">The print system rejected or failed to accept the job.</exception>
	Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Finds a job submitted to this printer earlier, by its <see cref="IPrintJob.Id"/>, so it can be followed or
	/// cancelled after the application has restarted.
	/// </summary>
	/// <param name="jobId">The job's identifier, as <see cref="IPrintJob.Id"/> gave it.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>
	/// The job, or <see langword="null"/> if the print system no longer knows it, or it was never submitted to this
	/// printer. Print systems forget finished jobs: printers and CUPS after a while, and Windows as soon as the job has
	/// printed, unless the printer is set to keep printed documents. So a job that isn't found has most likely
	/// finished, but whether it printed can't be told.
	/// </returns>
	/// <exception cref="ArgumentNullException"><paramref name="jobId"/> is <see langword="null"/>.</exception>
	/// <exception cref="PrintException">The print system or printer couldn't be asked.</exception>
	Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads what the printer can do: paper sizes, duplex, colour, copies, orientation and paper sources.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The capabilities. Anything the print system doesn't report is left empty.</returns>
	/// <exception cref="PrintException">The print system or printer couldn't be asked.</exception>
	Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
}
