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
}
