using Latiprimere.Cups.Resources;
using Latiprimere.Ipp;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;

namespace Latiprimere.Cups;

/// <summary>
/// The IPP client of a CUPS print system whose <c>CUPS_SERVER</c> isn't a valid server: every request fails with a
/// <see cref="PrintException"/> that says so.
/// </summary>
/// <remarks>
/// Failing each request, rather than failing to build the print system, lets the printer catalog skip CUPS and
/// still offer the other print systems' printers (ADR 0008), while the reason stays visible.
/// </remarks>
internal sealed class InvalidServerIppClient : IIppClient
{
	private readonly string server; // The invalid CUPS_SERVER value

	/// <summary>
	/// Initialises a new instance of the <see cref="InvalidServerIppClient"/> class.
	/// </summary>
	/// <param name="server">The invalid <c>CUPS_SERVER</c> value.</param>
	public InvalidServerIppClient(string server)
	{
		this.server = server;
	}

	/// <inheritdoc/>
	public Task<CUPSGetPrintersResponse> GetCupsPrintersAsync(CUPSGetPrintersRequest request, CancellationToken cancellationToken)
	{
		return Task.FromException<CUPSGetPrintersResponse>(this.CreateException());
	}

	/// <inheritdoc/>
	public Task<string?> GetCupsDefaultPrinterNameAsync(Uri serverUri, string requestingUserName, CancellationToken cancellationToken)
	{
		return Task.FromException<string?>(this.CreateException());
	}

	/// <inheritdoc/>
	public Task<GetPrinterAttributesResponse> GetPrinterAttributesAsync(GetPrinterAttributesRequest request, CancellationToken cancellationToken)
	{
		return Task.FromException<GetPrinterAttributesResponse>(this.CreateException());
	}

	/// <inheritdoc/>
	public Task<PrintJobResponse> PrintJobAsync(PrintJobRequest request, IppVendorAttributes vendorAttributes, CancellationToken cancellationToken)
	{
		return Task.FromException<PrintJobResponse>(this.CreateException());
	}

	/// <inheritdoc/>
	public Task<GetJobAttributesResponse> GetJobAttributesAsync(GetJobAttributesRequest request, CancellationToken cancellationToken)
	{
		return Task.FromException<GetJobAttributesResponse>(this.CreateException());
	}

	/// <inheritdoc/>
	public Task<CancelJobResponse> CancelJobAsync(CancelJobRequest request, CancellationToken cancellationToken)
	{
		return Task.FromException<CancelJobResponse>(this.CreateException());
	}

	/// <summary>
	/// Creates the failure every request reports.
	/// </summary>
	/// <returns>The exception.</returns>
	private PrintException CreateException()
	{
		return new PrintException(ExceptionMessages.FormatInvalidCupsServerFromEnvironment(this.server));
	}
}
