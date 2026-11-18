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
}
