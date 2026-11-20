using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows.Mapping;

/// <summary>
/// Maps a Windows printer's status flags to a <see cref="PrinterStatus"/>, with the IPP <c>printer-state-reasons</c>
/// keywords that say the same (ADR 0049), so code reads printers' reasons the same way on every print system.
/// </summary>
internal static class WindowsPrinterStatusMapper
{
	// Each flag that has a reason, in the order they are given
	private static readonly (PrinterStatusFlags Flag, string Reason)[] Reasons =
	[
		(PrinterStatusFlags.Paused, "paused"),
		(PrinterStatusFlags.PendingDeletion, "deactivated"),
		(PrinterStatusFlags.Offline, "offline-report"),
		(PrinterStatusFlags.NotAvailable, "offline-report"),
		(PrinterStatusFlags.PaperJam, "media-jam-error"),
		(PrinterStatusFlags.PaperOut, "media-empty-error"),
		(PrinterStatusFlags.PaperProblem, "media-needed-error"),
		(PrinterStatusFlags.ManualFeed, "media-needed-report"),
		(PrinterStatusFlags.DoorOpen, "door-open-error"),
		(PrinterStatusFlags.NoToner, "toner-empty-error"),
		(PrinterStatusFlags.TonerLow, "toner-low-warning"),
		(PrinterStatusFlags.OutputBinFull, "output-area-full-error"),
		(PrinterStatusFlags.OutOfMemory, "interpreter-resource-unavailable-error"),
		(PrinterStatusFlags.UserIntervention, "other-error"),
		(PrinterStatusFlags.PagePunt, "other-error"),
		(PrinterStatusFlags.Error, "other-error"),
	];

	/// <summary>
	/// Maps a printer's record to its status.
	/// </summary>
	/// <param name="printer">The printer, as the spooler reports it.</param>
	/// <param name="printerId">The printer's identifier.</param>
	/// <param name="timestamp">When it was read.</param>
	/// <returns>The status. The spooler has no message, so there is none; a queue takes jobs unless it is being
	/// deleted.</returns>
	public static PrinterStatus ToPrinterStatus(WindowsPrinterRecord printer, string printerId, DateTimeOffset timestamp)
	{
		ArgumentNullException.ThrowIfNull(printer);

		PrinterStatusFlags status = printer.Status | (printer.IsWorkingOffline ? PrinterStatusFlags.Offline : PrinterStatusFlags.None);

		return new PrinterStatus
		{
			PrinterId       = printerId,
			State           = WindowsPrinterInfoMapper.ToPrinterState(printer.Status, printer.IsWorkingOffline),
			StateReasons    = WindowsPrinterStatusMapper.Reasons
				.Where(entry => (status & entry.Flag) != 0)
				.Select(entry => entry.Reason)
				.Distinct(StringComparer.Ordinal)
				.ToArray(),
			IsAcceptingJobs = (status & PrinterStatusFlags.PendingDeletion) == 0,
			Timestamp       = timestamp
		};
	}
}
