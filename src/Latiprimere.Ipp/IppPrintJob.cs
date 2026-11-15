using System.Globalization;
using Latiprimere.Ipp.Mapping;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp;

/// <summary>
/// A job submitted to an IPP printer or CUPS queue.
/// </summary>
internal sealed class IppPrintJob : IPrintJob
{
	private readonly IppPrintContext context; // Client and settings shared with the print system
	private readonly Uri printerUri;          // URI of the printer the job was sent to
	private readonly int jobNumber;           // The IPP job-id
	private volatile bool isCancelRequested;  // Whether CancelAsync cancelled the job

	/// <summary>
	/// Initialises a new instance of the <see cref="IppPrintJob"/> class.
	/// </summary>
	/// <param name="context">The client and settings shared with the print system.</param>
	/// <param name="printerUri">The URI of the printer the job was sent to.</param>
	/// <param name="printerId">The Latiprimere identifier of that printer.</param>
	/// <param name="jobNumber">The IPP <c>job-id</c>.</param>
	public IppPrintJob(IppPrintContext context, Uri printerUri, string printerId, int jobNumber)
	{
		this.context = context;
		this.printerUri = printerUri;
		this.PrinterId = printerId;
		this.jobNumber = jobNumber;
	}

	/// <inheritdoc/>
	public string Id
	{
		get
		{
			return this.jobNumber.ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <inheritdoc/>
	public string PrinterId { get; }

	/// <inheritdoc/>
	public async Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		// Ask only for the attributes a status needs
		GetJobAttributesRequest request = new GetJobAttributesRequest
		{
			OperationAttributes = new GetJobAttributesOperationAttributes
			{
				PrinterUri          = new IppValue<Uri>(this.printerUri),
				JobId               = new IppValue<int>(this.jobNumber),
				RequestingUserName  = new StringWithLanguage(this.context.RequestingUserName),
				RequestedAttributes = new IppValue<string[]>(IppRequestedAttributes.JobStatusAttributes)
			}
		};

		GetJobAttributesResponse response;

		try
		{
			response = await this.context.Client
				.GetJobAttributesAsync(request, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (PrintException exception) when (exception.NativeErrorCode == (int)IppStatusCode.ClientErrorNotFound)
		{
			return await this.CreateNoLongerListedStatusAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			return IppJobStatusMapper.ToPrintJobStatus
			(
				response.JobAttributes,
				this.Id,
				this.context.TimeProvider.GetUtcNow()
			);
		}
		catch (Exception exception) when (IppResponseErrors.IsUnreadableData(exception))
		{
			throw IppResponseErrors.CreateUnreadable(this.printerUri, exception);
		}
	}

	/// <inheritdoc/>
	public async Task<bool> CancelAsync(CancellationToken cancellationToken = default)
	{
		CancelJobRequest request = new CancelJobRequest
		{
			OperationAttributes = new CancelJobOperationAttributes
			{
				PrinterUri         = new IppValue<Uri>(this.printerUri),
				JobId              = new IppValue<int>(this.jobNumber),
				RequestingUserName = new StringWithLanguage(this.context.RequestingUserName)
			}
		};

		try
		{
			await this.context.Client.CancelJobAsync(request, cancellationToken).ConfigureAwait(false);
			this.isCancelRequested = true;

			return true;
		}
		catch (PrintException exception)
			when (exception.NativeErrorCode is (int)IppStatusCode.ClientErrorNotPossible or (int)IppStatusCode.ClientErrorNotFound)
		{
			// IPP reports client-error-not-possible for jobs that have already finished, and printers with little
			// job history answer not-found for jobs they have already forgotten
			return false;
		}
	}

	/// <summary>
	/// Creates the status of a job the printer answers not-found for: forgotten, if the printer or queue still
	/// answers, or gone with it, if not.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the check.</param>
	/// <returns>The job status.</returns>
	/// <exception cref="PrintException">The printer or queue couldn't be asked.</exception>
	/// <remarks>
	/// Printers with little job history forget a job soon after it finishes, but CUPS also answers not-found
	/// when the queue itself has been deleted, which cancels its jobs. Asking for the printer tells the two apart.
	/// </remarks>
	private async Task<PrintJobStatus> CreateNoLongerListedStatusAsync(CancellationToken cancellationToken)
	{
		PrinterDescriptionAttributes? printer = await IppPrinterQueries
			.GetAttributesAsync
			(
				this.context,
				this.printerUri,
				[IppRequestedAttributes.PrinterState],
				cancellationToken
			)
			.ConfigureAwait(false);

		DateTimeOffset now = this.context.TimeProvider.GetUtcNow();

		return printer is null
			? IppJobStatusMapper.ToPrinterGoneStatus(this.Id, this.isCancelRequested, now)
			: IppJobStatusMapper.ToNoLongerListedStatus(this.Id, this.isCancelRequested, now);
	}
}
