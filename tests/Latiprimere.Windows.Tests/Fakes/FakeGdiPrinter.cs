using Latiprimere.Windows.Gdi;

namespace Latiprimere.Windows.Tests.Fakes;

/// <summary>
/// A GDI printer that records what it was asked to print, and reports how many pages the document had at that time.
/// </summary>
internal sealed class FakeGdiPrinter : IGdiPrinter
{
	/// <summary>Gets or sets the job ID returned by the next print.</summary>
	public int NextJobId { get; set; } = 1;

	/// <summary>Gets the last request received.</summary>
	public GdiPrintRequest? LastRequest { get; private set; }

	/// <summary>Gets the page count of the last request's document, read while it was still open.</summary>
	public int LastPageCount { get; private set; }

	/// <summary>Records the request and returns <see cref="NextJobId"/>.</summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The job ID.</returns>
	public int PrintRasterDocument(GdiPrintRequest request, CancellationToken cancellationToken)
	{
		this.LastRequest = request;
		this.LastPageCount = request.Document.PageCount;

		return this.NextJobId;
	}
}
