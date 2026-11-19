using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;

namespace Latiprimere.Ipp;

/// <summary>
/// The document of a Print-Job request, which may be sent more than once while the printer is busy.
/// </summary>
internal abstract class IppJobDocument
{
	/// <summary>
	/// Gets a value indicating whether the document can be sent again from its start, after a failed attempt.
	/// </summary>
	public abstract bool CanResend { get; }

	/// <summary>
	/// Sends the request with the document, from its start.
	/// </summary>
	/// <param name="client">The client to send it with.</param>
	/// <param name="request">The request, whose document this sets.</param>
	/// <param name="vendorAttributes">Attributes from the vendor options.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The printer's response.</returns>
	/// <exception cref="PrintException">The printer rejected the job, or couldn't be reached.</exception>
	public abstract Task<PrintJobResponse> SendAsync
	(
		IIppClient          client,
		PrintJobRequest     request,
		IppVendorAttributes vendorAttributes,
		CancellationToken   cancellationToken
	);
}
