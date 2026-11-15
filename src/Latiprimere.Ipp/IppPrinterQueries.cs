using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp;

/// <summary>
/// IPP queries shared by the CUPS and network print systems.
/// </summary>
internal static class IppPrinterQueries
{
	/// <summary>
	/// Reads a printer's attributes.
	/// </summary>
	/// <param name="context">The client and settings to use.</param>
	/// <param name="printerUri">The printer's URI.</param>
	/// <param name="requestedAttributes">The attributes to read.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The attributes, or <see langword="null"/> if there is no printer at that URI.</returns>
	/// <exception cref="PrintException">The printer could not be reached or rejected the request.</exception>
	internal static async Task<PrinterDescriptionAttributes?> GetAttributesAsync
	(
		IppPrintContext   context,
		Uri               printerUri,
		string[]          requestedAttributes,
		CancellationToken cancellationToken
	)
	{
		GetPrinterAttributesRequest request = new GetPrinterAttributesRequest
		{
			OperationAttributes = new GetPrinterAttributesOperationAttributes
			{
				PrinterUri          = new IppValue<Uri>(printerUri),
				RequestingUserName  = new StringWithLanguage(context.RequestingUserName),
				RequestedAttributes = new IppValue<string[]>(requestedAttributes)
			}
		};

		try
		{
			GetPrinterAttributesResponse response = await context.Client
				.GetPrinterAttributesAsync(request, cancellationToken)
				.ConfigureAwait(false);

			return response.PrinterAttributes;
		}
		catch (PrintException exception)
			when (exception.NativeErrorCode == (int)IppStatusCode.ClientErrorNotFound)
		{
			// IPP answers client-error-not-found for an unknown printer or queue
			return null;
		}
	}
}
