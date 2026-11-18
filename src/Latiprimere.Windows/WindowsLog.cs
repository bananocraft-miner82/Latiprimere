using Microsoft.Extensions.Logging;

namespace Latiprimere.Windows;

/// <summary>
/// What the Windows print system logs: what it carries on without, which callers aren't told.
/// </summary>
internal static partial class WindowsLog
{
	/// <summary>
	/// Logs that a driver's Print Capabilities couldn't be read, as for a driver without Print Ticket support, so its
	/// capabilities list no vendor options (ADR 0032).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The printer.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "The Print Capabilities of '{PrinterName}' couldn't be read, so its capabilities list no vendor options.")]
	internal static partial void PrintCapabilitiesUnavailable(ILogger logger, string printerName, Exception exception);

	/// <summary>
	/// Logs that a driver's Print Capabilities or default Print Ticket weren't XML (ADR 0032).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The printer.</param>
	/// <param name="exception">What the XML parser said.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "The driver of '{PrinterName}' gave Print Capabilities or a default Print Ticket that can't be read, so its vendor options may be missing or have no defaults.")]
	internal static partial void PrintCapabilitiesUnreadable(ILogger logger, string printerName, Exception exception);

	/// <summary>
	/// Logs that a WSD queue's printer was found over mDNS by the UUID its port records, and its PDF jobs go to it
	/// over IPP (ADR 0036).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The queue.</param>
	/// <param name="printerUri">The printer's IPP URI.</param>
	[LoggerMessage(Level = LogLevel.Debug, Message = "The WSD queue '{PrinterName}' is the printer at {PrinterUri}, by its UUID, so its PDF jobs go there over IPP.")]
	internal static partial void IppPrinterMatched(ILogger logger, string printerName, Uri printerUri);

	/// <summary>
	/// Logs that the printer where a WSD queue's UUID was found reports another UUID, or none (ADR 0036).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The queue.</param>
	/// <param name="printerUri">Where it was found.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "The printer at {PrinterUri} doesn't report the UUID the WSD queue '{PrinterName}' records, so the job goes through the queue.")]
	internal static partial void IppPrinterNotMatched(ILogger logger, string printerName, Uri printerUri);

	/// <summary>
	/// Logs that searching the network for a WSD queue's printer failed (ADR 0036).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "Searching the network for a WSD queue's IPP printer failed, so its jobs go through the queue.")]
	internal static partial void IppPrinterSearchFailed(ILogger logger, Exception exception);

	/// <summary>
	/// Logs that a PDF couldn't be planned as raster for a queue's IPP printer, so the driver prints it (ADR 0036).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The queue.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "The PDF can't be sent as raster to the IPP printer of '{PrinterName}', so the job goes through the queue.")]
	internal static partial void RasterNotPossible(ILogger logger, string printerName, Exception exception);
}
