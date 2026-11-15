using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;

namespace Latiprimere.Ipp;

/// <summary>
/// The IPP operations Latiprimere uses. Keeps the rest of the code independent of how requests are sent, and lets
/// tests substitute canned responses.
/// </summary>
/// <remarks>
/// Implementations translate transport and protocol failures into <see cref="PrintException"/>.
/// </remarks>
internal interface IIppClient
{
	/// <summary>
	/// Sends a CUPS-Get-Printers request.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The response.</returns>
	/// <exception cref="PrintException">The request failed or was rejected.</exception>
	Task<CUPSGetPrintersResponse> GetCupsPrintersAsync(CUPSGetPrintersRequest request, CancellationToken cancellationToken);

	/// <summary>
	/// Sends a CUPS-Get-Default request and reads the default queue's name.
	/// </summary>
	/// <param name="serverUri">The base URI of the CUPS server.</param>
	/// <param name="requestingUserName">The user name to send.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The default queue's name, or <see langword="null"/> if no default is set.</returns>
	/// <exception cref="PrintException">The request failed or was rejected.</exception>
	Task<string?> GetCupsDefaultPrinterNameAsync(Uri serverUri, string requestingUserName, CancellationToken cancellationToken);

	/// <summary>
	/// Sends a Get-Printer-Attributes request.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The response.</returns>
	/// <exception cref="PrintException">The request failed or was rejected.</exception>
	Task<GetPrinterAttributesResponse> GetPrinterAttributesAsync(GetPrinterAttributesRequest request, CancellationToken cancellationToken);

	/// <summary>
	/// Sends a Print-Job request.
	/// </summary>
	/// <param name="request">The request, including the document.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The response.</returns>
	/// <exception cref="PrintException">The request failed or was rejected.</exception>
	Task<PrintJobResponse> PrintJobAsync(PrintJobRequest request, CancellationToken cancellationToken);

	/// <summary>
	/// Sends a Get-Job-Attributes request.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The response.</returns>
	/// <exception cref="PrintException">The request failed or was rejected.</exception>
	Task<GetJobAttributesResponse> GetJobAttributesAsync(GetJobAttributesRequest request, CancellationToken cancellationToken);

	/// <summary>
	/// Sends a Cancel-Job request.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The response.</returns>
	/// <exception cref="PrintException">The request failed or was rejected.</exception>
	Task<CancelJobResponse> CancelJobAsync(CancelJobRequest request, CancellationToken cancellationToken);
}
