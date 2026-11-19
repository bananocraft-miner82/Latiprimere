using Latiprimere.Ipp.Raster;
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

	/// <summary>
	/// Logs that a printer couldn't check a job's attributes with Validate-Job, as when busy, so the job was sent
	/// without that check (ADR 0033).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerUri">The printer.</param>
	/// <param name="exception">What it answered.</param>
	[LoggerMessage(Level = LogLevel.Debug, Message = "{PrinterUri} couldn't validate the job's attributes now, so the job is sent without that check.")]
	internal static partial void ValidationSkipped(ILogger logger, Uri printerUri, Exception exception);

	/// <summary>
	/// Logs that a PDF is sent as raster to a printer that can't take PDF (ADR 0035).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="printerUri">The printer.</param>
	/// <param name="mimeType">The raster format's MIME type.</param>
	/// <param name="colorSpace">The colour space.</param>
	/// <param name="dpi">The resolution.</param>
	/// <param name="gzip">Whether the upload is gzipped.</param>
	[LoggerMessage(Level = LogLevel.Debug, Message = "{PrinterUri} doesn't take PDF, so the PDF is sent as {MimeType}, {ColorSpace} at {Dpi} dpi, gzipped: {Gzip}.")]
	internal static partial void PrintingAsRaster(ILogger logger, Uri printerUri, string mimeType, RasterColorSpace colorSpace, int dpi, bool gzip);

	/// <summary>
	/// Logs that a printer's IPPS address couldn't be used, for an untrusted certificate or because nothing answered
	/// there, so it is reached over plain IPP (ADR 0037).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="secureUri">The IPPS address.</param>
	/// <param name="printerUri">The plain IPP address used instead.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Information, Message = "The encrypted connection to {SecureUri} couldn't be opened, because its certificate isn't trusted or nothing answered there, so {PrinterUri} is used instead.")]
	internal static partial void SecureAddressUnusable(ILogger logger, Uri secureUri, Uri printerUri, Exception exception);

	/// <summary>
	/// Logs the printers read from the address store (ADR 0038).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="path">The file.</param>
	/// <param name="count">How many printers it remembers.</param>
	[LoggerMessage(Level = LogLevel.Debug, Message = "Read {Count} remembered printer addresses from {Path}.")]
	internal static partial void AddressStoreLoaded(ILogger logger, string path, int count);

	/// <summary>
	/// Logs that the address store doesn't exist yet, so nothing is remembered (ADR 0038).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="path">The file.</param>
	[LoggerMessage(Level = LogLevel.Debug, Message = "There is no address store at {Path} yet, so no printer addresses are remembered.")]
	internal static partial void AddressStoreMissing(ILogger logger, string path);

	/// <summary>
	/// Logs that the address store couldn't be read, so nothing is remembered (ADR 0038).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="path">The file.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "The address store at {Path} couldn't be read, so no printer addresses are remembered; it is written afresh at the next change.")]
	internal static partial void AddressStoreUnreadable(ILogger logger, string path, Exception exception);

	/// <summary>
	/// Logs that the address store is in a later format, so it is neither read nor written (ADR 0038).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="path">The file.</param>
	/// <param name="version">The file's format version.</param>
	/// <param name="supportedVersion">The latest format this version reads.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "The address store at {Path} is in format {Version}, later than the {SupportedVersion} this version of Latiprimere reads, so no printer addresses are remembered and the file is left as it is.")]
	internal static partial void AddressStoreNewerFormat(ILogger logger, string path, int version, int supportedVersion);

	/// <summary>
	/// Logs that the address store couldn't be written (ADR 0038).
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="path">The file.</param>
	/// <param name="exception">Why.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "The address store at {Path} couldn't be written; the next change tries again.")]
	internal static partial void AddressStoreNotSaved(ILogger logger, string path, Exception exception);
}
