using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Latiprimere;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LatiprimereExamples;

/// <summary>
/// Prints queued requests one at a time and follows each job until it finishes. Part of "Printing from a web app or
/// background service".
/// </summary>
internal sealed partial class PrintWorker : BackgroundService
{
	/// <summary>
	/// How long to follow a job before moving on to the next request. The job carries on printing either way.
	/// </summary>
	private static readonly TimeSpan FollowTimeout = TimeSpan.FromMinutes(2);

	private readonly PrintQueue queue;           // Requests waiting to print
	private readonly IPrinterCatalog catalog;    // Finds the printers
	private readonly ILogger<PrintWorker> logger; // Records what happened to each request

	/// <summary>
	/// Initialises a new instance of the <see cref="PrintWorker"/> class.
	/// </summary>
	/// <param name="queue">The queue of requests.</param>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="logger">The logger.</param>
	public PrintWorker(PrintQueue queue, IPrinterCatalog catalog, ILogger<PrintWorker> logger)
	{
		this.queue = queue;
		this.catalog = catalog;
		this.logger = logger;
	}

	/// <summary>
	/// Prints requests as they arrive, until the application stops.
	/// </summary>
	/// <param name="stoppingToken">Cancelled when the application stops.</param>
	/// <returns>A task that completes when the application stops.</returns>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await foreach (PrintRequest request in this.queue.ReadAllAsync(stoppingToken))
		{
			// A failed request is logged, and the worker carries on with the next
			try
			{
				await this.PrintAsync(request, stoppingToken);
			}
			catch (PrintException exception)
			{
				PrintWorker.LogPrintFailed(this.logger, request.PrinterId, exception);
			}
			catch (NotSupportedException exception)
			{
				PrintWorker.LogPrintFailed(this.logger, request.PrinterId, exception);
			}
		}
	}

	/// <summary>Logs that a job was accepted by the printer.</summary>
	/// <param name="logger">The logger.</param>
	/// <param name="jobId">The job's ID.</param>
	/// <param name="printerName">The printer's name.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} sent to {PrinterName}.")]
	private static partial void LogSubmitted(ILogger logger, string jobId, string printerName);

	/// <summary>Logs the state a job finished in.</summary>
	/// <param name="logger">The logger.</param>
	/// <param name="jobId">The job's ID.</param>
	/// <param name="state">Its final state.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "Job {JobId} finished: {State}.")]
	private static partial void LogFinished(ILogger logger, string jobId, PrintJobState state);

	/// <summary>Logs that a job was still going when the worker stopped following it.</summary>
	/// <param name="logger">The logger.</param>
	/// <param name="jobId">The job's ID.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "Job {JobId} hasn't finished; no longer following it.")]
	private static partial void LogStillPrinting(ILogger logger, string jobId);

	/// <summary>Logs that a request couldn't be printed.</summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerId">The printer the request was for.</param>
	/// <param name="exception">What went wrong.</param>
	[LoggerMessage(Level = LogLevel.Error, Message = "Couldn't print on {PrinterId}.")]
	private static partial void LogPrintFailed(ILogger logger, string printerId, Exception exception);

	/// <summary>
	/// Prints one request and follows the job for up to <see cref="FollowTimeout"/>.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="stoppingToken">Cancelled when the application stops.</param>
	/// <returns>A task that completes when the job has finished, or following it has stopped.</returns>
	private async Task PrintAsync(PrintRequest request, CancellationToken stoppingToken)
	{
		// The printer ID came from outside, so it is looked up in the catalog, never trusted as an address
		IPrinter printer = await this.catalog.GetPrinterAsync(request.PrinterId, stoppingToken);

		using MemoryStream document = new MemoryStream(request.Document, writable: false);
		PrintOptions options = new PrintOptions { JobName = request.JobName };

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, stoppingToken);

		PrintWorker.LogSubmitted(this.logger, job.Id, printer.Info.Name);

		// Follow the job for a while; stopping the application stops following, not the job
		using CancellationTokenSource following = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
		following.CancelAfter(PrintWorker.FollowTimeout);

		try
		{
			await foreach (PrintJobStatus status in job.WatchStatusAsync(following.Token))
			{
				if (status.IsTerminal)
				{
					PrintWorker.LogFinished(this.logger, job.Id, status.State);
				}
			}
		}
		catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
		{
			PrintWorker.LogStillPrinting(this.logger, job.Id);
		}
	}
}
