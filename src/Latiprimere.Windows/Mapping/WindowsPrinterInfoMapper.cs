using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows.Mapping;

/// <summary>
/// Maps Windows spooler printer information to <see cref="PrinterInfo"/>.
/// </summary>
internal static class WindowsPrinterInfoMapper
{
	/// <summary>Flags meaning the printer cannot print until something is fixed.</summary>
	private static readonly PrinterStatusFlags StoppedFlags = PrinterStatusFlags.Paused
																| PrinterStatusFlags.Error
																| PrinterStatusFlags.PendingDeletion
																| PrinterStatusFlags.PaperJam
																| PrinterStatusFlags.PaperOut
																| PrinterStatusFlags.ManualFeed
																| PrinterStatusFlags.PaperProblem
																| PrinterStatusFlags.Offline
																| PrinterStatusFlags.OutputBinFull
																| PrinterStatusFlags.NotAvailable
																| PrinterStatusFlags.NoToner
																| PrinterStatusFlags.PagePunt
																| PrinterStatusFlags.UserIntervention
																| PrinterStatusFlags.OutOfMemory
																| PrinterStatusFlags.DoorOpen
																| PrinterStatusFlags.ServerUnknown;

	/// <summary>Flags meaning the printer is working on jobs.</summary>
	private static readonly PrinterStatusFlags ProcessingFlags = PrinterStatusFlags.Printing
																	| PrinterStatusFlags.Processing
																	| PrinterStatusFlags.Busy
																	| PrinterStatusFlags.IOActive
																	| PrinterStatusFlags.Initializing
																	| PrinterStatusFlags.WarmingUp;

	/// <summary>
	/// Creates printer information from a spooler printer record.
	/// </summary>
	/// <param name="printer">The printer record.</param>
	/// <param name="printerId">The Latiprimere identifier to give the printer.</param>
	/// <param name="isDefault">Whether it is the user's default printer.</param>
	/// <returns>The printer information.</returns>
	internal static PrinterInfo ToPrinterInfo(WindowsPrinterRecord printer, string printerId, bool isDefault)
	{
		ArgumentNullException.ThrowIfNull(printer);

		return new PrinterInfo
		{
			Id           = printerId,
			Name         = printer.Name,
			Description  = string.IsNullOrWhiteSpace(printer.Comment) ? null : printer.Comment,
			Location     = string.IsNullOrWhiteSpace(printer.Location) ? null : printer.Location,
			MakeAndModel = string.IsNullOrWhiteSpace(printer.DriverName) ? null : printer.DriverName,
			State        = WindowsPrinterInfoMapper.ToPrinterState(printer.Status, printer.IsWorkingOffline),
			IsDefault    = isDefault
		};
	}

	/// <summary>
	/// Maps spooler printer status flags to a printer state.
	/// </summary>
	/// <param name="status">The PRINTER_STATUS_* flags.</param>
	/// <param name="isWorkingOffline">Whether the user has set the printer to work offline.</param>
	/// <returns>The printer state. Advisory flags such as toner-low or power-save leave the printer idle.</returns>
	internal static PrinterState ToPrinterState(PrinterStatusFlags status, bool isWorkingOffline)
	{
		return status switch
		{
			_ when isWorkingOffline                                          => PrinterState.Stopped,
			_ when (status & WindowsPrinterInfoMapper.StoppedFlags) != 0    => PrinterState.Stopped,
			_ when (status & WindowsPrinterInfoMapper.ProcessingFlags) != 0 => PrinterState.Processing,
			_                                                                => PrinterState.Idle,
		};
	}
}
