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

	/// <summary>
	/// Logs that a queue's IPP printer presented a certificate that isn't trusted, so its jobs go through the queue.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="printerUri">The printer's IPPS address.</param>
	/// <param name="fingerprint">The certificate's SHA-256 fingerprint, to pin.</param>
	/// <param name="exception">The refusal.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "The IPP printer of '{PrinterName}' at {PrinterUri} presented a certificate that isn't trusted, with SHA-256 fingerprint {Fingerprint}, so the job goes through the queue. If that is the fingerprint the printer's own web page shows, add it to WindowsOptions.IppTrustedCertificateFingerprints to send its PDF jobs over IPPS.")]
	internal static partial void IppCertificateNotTrusted(ILogger logger, string printerName, Uri printerUri, string fingerprint, Exception exception);

	/// <summary>
	/// Logs that a queue's IPP printer couldn't be asked about a job, so it goes through the queue.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="printerUri">The printer's address.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "The IPP printer of '{PrinterName}' at {PrinterUri} couldn't be reached or refused to say what it takes, so the job goes through the queue.")]
	internal static partial void IppPrinterUnavailable(ILogger logger, string printerName, Uri printerUri, Exception exception);

	/// <summary>
	/// Logs that a queue's IPP printer asks for authentication it won't be given, so its jobs go through the queue.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="printerUri">The printer's address.</param>
	/// <param name="authentication">Its <c>uri-authentication-supported</c> keyword for that address.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "The IPP printer of '{PrinterName}' at {PrinterUri} asks for '{Authentication}' authentication, and is sent no password there: WindowsOptions.IppCredentials and IppPrinterCredentials are sent only over IPPS, with HTTP Basic. Its jobs go through the queue.")]
	internal static partial void IppPrinterNeedsAuthentication(ILogger logger, string printerName, Uri printerUri, string authentication);

	/// <summary>
	/// Logs that a queue's IPP printer couldn't be reached at its IPPS address, so its plain IPP address is used.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="secureUri">The IPPS address tried first.</param>
	/// <param name="printerUri">The plain IPP address used instead.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "The encrypted connection to the IPP printer of '{PrinterName}' at {SecureUri} couldn't be opened, because its certificate isn't trusted or nothing answered there, so {PrinterUri} is used instead.")]
	internal static partial void IppSecureAddressUnusable(ILogger logger, string printerName, Uri secureUri, Uri printerUri, Exception exception);

	/// <summary>
	/// Logs that a queue's IPP printer couldn't be asked for its details, so they are left empty (ADR 0059).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="printerUri">The printer's address.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "The IPP printer of '{PrinterName}' at {PrinterUri} couldn't be asked for its details, so they are left empty.")]
	internal static partial void IppPrinterDetailsUnavailable(ILogger logger, string printerName, Uri printerUri, Exception exception);
}
