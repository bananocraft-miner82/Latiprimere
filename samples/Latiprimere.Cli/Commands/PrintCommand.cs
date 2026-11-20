using System.Diagnostics;
using Latiprimere.Cli.Resources;

namespace Latiprimere.Cli.Commands;

/// <summary>
/// The <c>print</c> command: prints a file and follows the job until it finishes.
/// </summary>
internal static class PrintCommand
{
	/// <summary>The command name.</summary>
	internal const string Name = "print";

	/// <summary>Bytes in a mebibyte, for reporting memory use.</summary>
	private const double BytesPerMebibyte = 1024.0 * 1024.0;

	/// <summary>
	/// Prints a file, optionally cancels it, and reports each status change until the job finishes.
	/// </summary>
	/// <param name="catalog">The printer catalog.</param>
	/// <param name="arguments">The printer ID, the file path and any options.</param>
	/// <returns>The process exit code.</returns>
	internal static async Task<int> RunAsync(IPrinterCatalog catalog, string[] arguments)
	{
		// Validate the command line before contacting the print system
		PrintArguments? printArguments = PrintArguments.Parse(arguments, out string? error);

		if (printArguments is null)
		{
			ConsoleOutput.WriteError(CliMessages.InvalidArguments, error);

			return ConsoleOutput.WriteUsage();
		}

		IPrinter printer = await catalog.GetPrinterAsync(printArguments.PrinterId, CancellationToken.None);

		// Paper and tray are chosen from what the printer reports, as the library expects
		PrintArguments? resolved = await PrintCommand.ResolvePaperAndTrayAsync(printer, printArguments);

		if (resolved is null)
		{
			return ExitCodes.InvalidArguments;
		}

		// Submit, timing how long rendering and spooling take
		Stopwatch stopwatch = Stopwatch.StartNew();

		IPrintJob job = await PrintCommand.SubmitAsync(printer, resolved);
		TimeSpan submitTime = stopwatch.Elapsed;

		ConsoleOutput.Write(CliMessages.JobSubmitted, job.Id, printer.Info.Name);

		if (printArguments.CancelAfterSubmit)
		{
			bool isCancelled = await job.CancelAsync(CancellationToken.None);

			ConsoleOutput.Write(isCancelled ? CliMessages.JobCancelRequested : CliMessages.JobAlreadyFinished, job.Id);
		}

		await PrintCommand.FollowJobAsync(job);

		if (printArguments.ShowStatistics)
		{
			PrintCommand.WriteStatistics(submitTime, stopwatch.Elapsed);
		}

		return ExitCodes.Success;
	}

	/// <summary>
	/// Looks up the paper size and tray asked for in the printer's capabilities.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <param name="printArguments">The parsed arguments.</param>
	/// <returns>The arguments with the size and tray in their options, or <see langword="null"/> if the printer
	/// doesn't list one of them, which has been reported.</returns>
	private static async Task<PrintArguments?> ResolvePaperAndTrayAsync(IPrinter printer, PrintArguments printArguments)
	{
		if (printArguments.PaperName is null && printArguments.TrayId is null)
		{
			return printArguments;
		}

		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(CancellationToken.None);
		PrintOptions options = printArguments.Options;

		if (printArguments.PaperName is not null)
		{
			MediaSize? size = capabilities.MediaSizes.FirstOrDefault(size => size.Name == printArguments.PaperName);

			if (size is null)
			{
				ConsoleOutput.WriteError(CliMessages.PaperNotListed, printer.Info.Name, printArguments.PaperName);

				return null;
			}

			options = options with { MediaSize = size };
		}

		if (printArguments.TrayId is not null)
		{
			InputTray? tray = capabilities.InputTrays.FirstOrDefault(tray => tray.Id == printArguments.TrayId);

			if (tray is null)
			{
				ConsoleOutput.WriteError(CliMessages.TrayNotListed, printer.Info.Name, printArguments.TrayId);

				return null;
			}

			options = options with { InputTray = tray };
		}

		return printArguments with { Options = options };
	}

	/// <summary>
	/// Opens the file and submits it; the stream stays open until the submission completes.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <param name="printArguments">The parsed arguments.</param>
	/// <returns>The submitted job.</returns>
	private static async Task<IPrintJob> SubmitAsync(IPrinter printer, PrintArguments printArguments)
	{
		using FileStream document = File.OpenRead(printArguments.FilePath);

		PrintContent content = printArguments.IsRaw ? PrintContent.FromRaw(document) : PrintCommand.ToContent(document, printArguments.FilePath);

		return await printer.SubmitAsync(content, printArguments.Options, CancellationToken.None);
	}

	/// <summary>
	/// Wraps a file as an image, text or a PDF, by its extension.
	/// </summary>
	/// <param name="document">The open file.</param>
	/// <param name="filePath">Its path.</param>
	/// <returns>A PNG or JPEG for those extensions, UTF-8 text for <c>.txt</c>, otherwise a PDF.</returns>
	private static PrintContent ToContent(FileStream document, string filePath)
	{
		return Path.GetExtension(filePath).ToUpperInvariant() switch
		{
			".PNG"           => PrintContent.FromPng(document),
			".JPG" or ".JPEG" => PrintContent.FromJpeg(document),
			".TXT"           => PrintContent.FromText(document, System.Text.Encoding.UTF8),
			_                => PrintContent.FromPdf(document),
		};
	}

	/// <summary>
	/// Reports each status change until the job finishes.
	/// </summary>
	/// <param name="job">The job to follow.</param>
	/// <returns>A task that completes when the job reaches a final state.</returns>
	private static async Task FollowJobAsync(IPrintJob job)
	{
		await foreach (PrintJobStatus status in job.WatchStatusAsync(CancellationToken.None))
		{
			string messagePart = status.StateMessage is null
				? string.Empty
				: CliMessages.FormatJobStatusMessagePart(status.StateMessage);

			// Why the job is in its state, as the printer's job-state-reasons keywords (ADR 0043)
			string reasonsPart = status.StateReasons.Count == 0
				? string.Empty
				: CliMessages.FormatJobStatusReasonsPart(string.Join(", ", status.StateReasons));

			string pagesPart = status.PagesCompleted is null
				? string.Empty
				: CliMessages.FormatJobStatusPagesPart(status.PagesCompleted);

			ConsoleOutput.Write
			(
				CliMessages.JobStatusLine,
				status.Timestamp.ToLocalTime(),
				status.State,
				messagePart,
				reasonsPart,
				pagesPart
			);
		}
	}

	/// <summary>
	/// Reports timing and the process's peak memory use.
	/// </summary>
	/// <param name="submitTime">How long submitting (rendering and spooling) took.</param>
	/// <param name="totalTime">How long until the job finished.</param>
	private static void WriteStatistics(TimeSpan submitTime, TimeSpan totalTime)
	{
		using Process process = Process.GetCurrentProcess();

		ConsoleOutput.Write
		(
			CliMessages.Statistics,
			submitTime.TotalSeconds,
			totalTime.TotalSeconds,
			process.PeakWorkingSet64 / PrintCommand.BytesPerMebibyte,
			GC.GetGCMemoryInfo().HeapSizeBytes / PrintCommand.BytesPerMebibyte
		);
	}
}
