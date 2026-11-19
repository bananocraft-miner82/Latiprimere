using Latiprimere.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Latiprimere.Ipp;

/// <summary>
/// The settings shared by every printer and job of one IPP print system.
/// </summary>
internal sealed record IppPrintContext
{
	/// <summary>
	/// Gets the client used to send IPP requests.
	/// </summary>
	public required IIppClient Client { get; init; }

	/// <summary>
	/// Gets the user name sent as <c>requesting-user-name</c>. Print systems use it to decide who owns a job.
	/// </summary>
	public required string RequestingUserName { get; init; }

	/// <summary>
	/// Gets the document format MIME types the print system expects.
	/// </summary>
	public required IppDocumentFormats DocumentFormats { get; init; }

	/// <summary>
	/// Gets the prefixes of the <see cref="PrintOptions.VendorOptions"/> keys this print system reads: <c>ipp:</c>
	/// for network printers, and <c>cups:</c> as well for CUPS.
	/// </summary>
	public IReadOnlyList<string> VendorOptionPrefixes { get; init; } = Mapping.IppVendorAttributeMapper.NetworkPrefixes;

	/// <summary>
	/// Gets the clock used to timestamp job statuses and to time retries.
	/// </summary>
	public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

	/// <summary>Gets whether to send Validate-Job before each Print-Job, to printers that list it, so attributes the
	/// printer refuses fail before the document is uploaded (ADR 0033). CUPS accepts a job and cancels it later, so
	/// only direct IPP turns this on.</summary>
	public bool ValidateJobs { get; init; }

	/// <summary>Gets the rasteriser that renders PDFs for printers that can't take PDF, or <see langword="null"/> to
	/// refuse such jobs (ADR 0035).</summary>
	public IPdfRasterizer? PdfRasterizer { get; init; }

	/// <summary>Gets the highest resolution a PDF is rendered at for a raster job, or <see langword="null"/> for no
	/// limit.</summary>
	public int? MaxRenderDpi { get; init; }

	/// <summary>Gets where to log what the caller isn't told, such as vendor options that couldn't be read.</summary>
	public ILogger Logger { get; init; } = NullLogger.Instance;

	/// <summary>
	/// Gets how long to keep resending a job that the printer turns away with <c>server-error-busy</c>.
	/// <see cref="TimeSpan.Zero"/>, the default, sends it once.
	/// </summary>
	public TimeSpan BusyRetryDuration { get; init; } = TimeSpan.Zero;

	/// <summary>
	/// Gets the wait before the first resend of a job turned away as busy. Each later wait doubles, up to
	/// <see cref="BusyRetryMaxDelay"/>.
	/// </summary>
	public TimeSpan BusyRetryFirstDelay { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the longest wait between resends of a job turned away as busy. Each resend uploads the whole document
	/// again, so the waits grow rather than stay short.
	/// </summary>
	public TimeSpan BusyRetryMaxDelay { get; init; } = TimeSpan.FromSeconds(15);
}
