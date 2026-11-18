using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Latiprimere.Ipp;

/// <summary>
/// What the IPP print systems log: what they carry on without, which callers aren't told.
/// </summary>
internal static partial class IppLog
{
	/// <summary>The category the IPP and CUPS print systems log under.</summary>
	internal const string Category = "Latiprimere.Ipp";

	/// <summary>
	/// Creates the logger the print systems use.
	/// </summary>
	/// <param name="loggerFactory">The application's logger factory, or <see langword="null"/>.</param>
	/// <returns>The logger; one that logs nothing without a factory.</returns>
	internal static ILogger CreateLogger(ILoggerFactory? loggerFactory)
	{
		return loggerFactory?.CreateLogger(IppLog.Category) ?? NullLogger.Instance;
	}

	/// <summary>
	/// Logs that a printer's vendor options couldn't be read, so its capabilities list none (ADR 0032).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerUri">The printer.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "The vendor options of {PrinterUri} couldn't be read, so its capabilities list none.")]
	internal static partial void VendorOptionsUnreadable(ILogger logger, Uri printerUri, Exception exception);
}
