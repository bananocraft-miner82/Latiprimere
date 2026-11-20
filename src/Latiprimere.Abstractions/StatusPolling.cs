using System.Runtime.CompilerServices;

namespace Latiprimere;

/// <summary>
/// Follows a job or a printer by reading its status at an interval: what <see cref="IPrintJob.WatchStatusAsync"/> and
/// <see cref="IPrinter.WatchStatusAsync"/> do for implementations that have no events to wait for.
/// </summary>
internal static class StatusPolling
{
	/// <summary>
	/// Follows a job until it reaches a final state, reading its status at an interval.
	/// </summary>
	/// <param name="job">The job.</param>
	/// <param name="pollInterval">How long to wait between reads.</param>
	/// <param name="cancellationToken">A token to stop following the job.</param>
	/// <returns>Each distinct status, ending with the final one.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is zero or negative.</exception>
	public static async IAsyncEnumerable<PrintJobStatus> WatchJobAsync
	(
		IPrintJob                                  job,
		TimeSpan                                   pollInterval,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

		PrintJobStatus? previous = null;

		while (true)
		{
			PrintJobStatus current = await job.GetStatusAsync(cancellationToken).ConfigureAwait(false);

			if (StatusPolling.HasChanged(previous, current))
			{
				yield return current;
			}

			if (current.IsTerminal)
			{
				yield break;
			}

			previous = current;
			await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Follows a printer until cancelled, reading its status at an interval.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <param name="pollInterval">How long to wait between reads.</param>
	/// <param name="cancellationToken">A token to stop following the printer.</param>
	/// <returns>Each distinct status.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="pollInterval"/> is zero or negative.</exception>
	public static async IAsyncEnumerable<PrinterStatus> WatchPrinterAsync
	(
		IPrinter                                   printer,
		TimeSpan                                   pollInterval,
		[EnumeratorCancellation] CancellationToken cancellationToken
	)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

		PrinterStatus? previous = null;

		while (true)
		{
			PrinterStatus current = await StatusPolling.ReadAsync(printer, cancellationToken).ConfigureAwait(false);

			if (StatusPolling.HasChanged(previous, current))
			{
				yield return current;
			}

			previous = current;
			await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reads a printer's status, giving a failure to read it, such as a dropped connection, as an unknown state with
	/// why as its message, so a watch with no end isn't ended by one.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <param name="cancellationToken">A token to stop reading.</param>
	/// <returns>The status.</returns>
	private static async Task<PrinterStatus> ReadAsync(IPrinter printer, CancellationToken cancellationToken)
	{
		try
		{
			return await printer.GetStatusAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (PrintException exception)
		{
			return new PrinterStatus
			{
				PrinterId    = printer.Info.Id,
				State        = PrinterState.Unknown,
				StateMessage = exception.Message,
				Timestamp    = DateTimeOffset.UtcNow
			};
		}
	}

	/// <summary>
	/// Checks whether a job's status has changed in a way worth reporting: its timestamp always changes, so only the
	/// rest is compared.
	/// </summary>
	/// <param name="previous">The status last reported, or <see langword="null"/>.</param>
	/// <param name="current">The status just read.</param>
	/// <returns>Whether its state, message, reasons or page count differ.</returns>
	public static bool HasChanged(PrintJobStatus? previous, PrintJobStatus current)
	{
		return previous is null
			|| previous.State != current.State
			|| previous.StateMessage != current.StateMessage
			|| previous.PagesCompleted != current.PagesCompleted
			|| !previous.StateReasons.SequenceEqual(current.StateReasons, StringComparer.Ordinal);
	}

	/// <summary>
	/// Checks whether a printer's status has changed in a way worth reporting.
	/// </summary>
	/// <param name="previous">The status last reported, or <see langword="null"/>.</param>
	/// <param name="current">The status just read.</param>
	/// <returns>Whether its state, message, reasons or whether it accepts jobs differ.</returns>
	public static bool HasChanged(PrinterStatus? previous, PrinterStatus current)
	{
		return previous is null
			|| previous.State != current.State
			|| previous.StateMessage != current.StateMessage
			|| previous.IsAcceptingJobs != current.IsAcceptingJobs
			|| !previous.StateReasons.SequenceEqual(current.StateReasons, StringComparer.Ordinal);
	}
}
