using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples;

/// <summary>
/// The examples from "Print options and following jobs".
/// </summary>
internal static class PrintOptionsAndJobs
{
	/// <summary>
	/// Prints two double-sided copies in landscape, and follows the job for at most five minutes.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <param name="pdfPath">The PDF to print.</param>
	/// <returns>A task that completes when the job has finished, or following it has stopped.</returns>
	public static async Task PrintWithOptionsAsync(IPrinter printer, string pdfPath)
	{
		PrintOptions options = new PrintOptions
		{
			JobName = "Quarterly report",
			Copies = 2,
			Duplex = DuplexMode.LongEdge,
			Orientation = PageOrientation.Landscape,
		};

		using FileStream document = File.OpenRead(pdfPath);
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options);

		using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));

		try
		{
			await foreach (PrintJobStatus status in job.WatchStatusAsync(timeout.Token))
			{
				string pages = status.PagesCompleted is int count ? $", {count} pages printed" : string.Empty;

				Console.WriteLine($"{status.State}{pages} {status.StateMessage}");
			}
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			Console.WriteLine("Stopped following the job. It may still print.");
		}
	}

	/// <summary>
	/// Waits for a job to finish, checking every two seconds, and reports whether it printed.
	/// </summary>
	/// <param name="job">The job.</param>
	/// <param name="cancellationToken">A token to stop waiting. It doesn't cancel the job.</param>
	/// <returns><see langword="true"/> if the job completed; <see langword="false"/> if it was cancelled or
	/// aborted.</returns>
	public static async Task<bool> WaitForJobAsync(IPrintJob job, CancellationToken cancellationToken)
	{
		PrintJobStatus? finalStatus = null;

		await foreach (PrintJobStatus status in job.WatchStatusAsync(TimeSpan.FromSeconds(2), cancellationToken))
		{
			finalStatus = status;
		}

		return finalStatus?.State == PrintJobState.Completed;
	}

	/// <summary>
	/// Cancels a job that hasn't finished within a minute.
	/// </summary>
	/// <param name="job">The job.</param>
	/// <returns>A task that completes when the job has finished or been cancelled.</returns>
	public static async Task CancelIfSlowAsync(IPrintJob job)
	{
		using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));

		try
		{
			await foreach (PrintJobStatus status in job.WatchStatusAsync(timeout.Token))
			{
				Console.WriteLine(status.State);
			}
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested)
		{
			bool cancelled = await job.CancelAsync();

			Console.WriteLine(cancelled ? "Cancelled the job." : "The job had already finished.");
		}
	}
}
