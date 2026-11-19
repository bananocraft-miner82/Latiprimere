using Latiprimere.Ipp;
using Latiprimere.Ipp.Mapping;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Cups;

/// <summary>
/// The CUPS print system: lists CUPS queues and prints to them over IPP.
/// </summary>
/// <remarks>
/// Printer identifiers are the queue name prefixed with <c>cups:</c>, so they never clash with other print systems.
/// </remarks>
internal sealed class CupsPrintSystem : IPrintSystem
{
	/// <summary>The prefix of every printer identifier from this print system.</summary>
	internal const string PrinterIdPrefix = "cups:";

	private readonly IppPrintContext context;  // Client and settings shared with printers and jobs
	private readonly CupsEndpoint endpoint;    // Where the scheduler is
	private readonly IDisposable? ownedClient; // The client to dispose with this print system, if owned

	/// <summary>
	/// Initialises a new instance of the <see cref="CupsPrintSystem"/> class.
	/// </summary>
	/// <param name="context">The client and settings shared with printers and jobs.</param>
	/// <param name="endpoint">Where the scheduler is.</param>
	/// <param name="isSupported">Whether CUPS can be used on this platform.</param>
	/// <param name="ownedClient">The client to dispose with this print system, if it owns one.</param>
	public CupsPrintSystem(IppPrintContext context, CupsEndpoint endpoint, bool isSupported, IDisposable? ownedClient)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(endpoint);

		this.context = context;
		this.endpoint = endpoint;
		this.IsSupported = isSupported;
		this.ownedClient = ownedClient;
	}

	/// <summary>
	/// Gets where the print system logs, from the application's logger factory when dependency injection has one.
	/// </summary>
	internal Microsoft.Extensions.Logging.ILogger Logger
	{
		get
		{
			return this.context.Logger;
		}
	}

	/// <inheritdoc/>
	public string Name
	{
		get
		{
			return "CUPS";
		}
	}

	/// <inheritdoc/>
	public bool IsSupported { get; }

	/// <inheritdoc/>
	public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
	{
		// Ask for the default, to flag it, and for the queues at the same time: neither needs the other's answer
		CUPSGetPrintersRequest request = this.CreateGetPrintersRequest(IppRequestedAttributes.PrinterSummaryAttributes);
		Task<string?> defaultLookup = this.GetDefaultQueueNameAsync(cancellationToken);
		Task<CUPSGetPrintersResponse> queuesLookup = this.context.Client.GetCupsPrintersAsync(request, cancellationToken);

		// Waiting for both observes both, so a failure in either is reported and neither goes unobserved
		await Task.WhenAll(defaultLookup, queuesLookup).ConfigureAwait(false);

		string? defaultQueueName = await defaultLookup.ConfigureAwait(false);
		CUPSGetPrintersResponse response = await queuesLookup.ConfigureAwait(false);

		// Queues without a name can't be addressed, so they're skipped
		List<PrinterInfo> printers = new List<PrinterInfo>();

		try
		{
			foreach (PrinterDescriptionAttributes attributes in response.PrintersAttributes ?? [])
			{
				string? queueName = IppValueReader.ReadString(attributes.PrinterName);

				if (queueName is not null)
				{
					printers.Add(CupsPrintSystem.CreatePrinterInfo(attributes, queueName, queueName == defaultQueueName));
				}
			}
		}
		catch (Exception exception) when (IppResponseErrors.IsUnreadableData(exception))
		{
			throw IppResponseErrors.CreateUnreadable(this.endpoint.ServerUri, exception);
		}

		return printers;
	}

	/// <inheritdoc/>
	public async Task<IPrinter?> FindPrinterAsync(string printerId, CancellationToken cancellationToken = default)
	{
		// Identifiers from other print systems are not ours to look up
		ArgumentNullException.ThrowIfNull(printerId);

		if (!printerId.StartsWith(CupsPrintSystem.PrinterIdPrefix, StringComparison.Ordinal)
			|| printerId.Length == CupsPrintSystem.PrinterIdPrefix.Length)
		{
			return null;
		}

		string queueName = printerId[CupsPrintSystem.PrinterIdPrefix.Length..];

		return await this.FindQueueAsync(queueName, isDefault: null, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task<IPrinter?> FindDefaultPrinterAsync(CancellationToken cancellationToken = default)
	{
		string? defaultQueueName = await this.GetDefaultQueueNameAsync(cancellationToken).ConfigureAwait(false);

		if (defaultQueueName is null)
		{
			return null;
		}

		// Already known to be the default, so CUPS isn't asked a second time
		return await this.FindQueueAsync(defaultQueueName, isDefault: true, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Looks up a queue by name.
	/// </summary>
	/// <param name="queueName">The queue name.</param>
	/// <param name="isDefault">Whether the queue is the default, or <see langword="null"/> to ask CUPS.</param>
	/// <param name="cancellationToken">A token to cancel the lookup.</param>
	/// <returns>The printer, or <see langword="null"/> if CUPS has no such queue.</returns>
	private async Task<IPrinter?> FindQueueAsync(string queueName, bool? isDefault, CancellationToken cancellationToken)
	{
		// No queue has these names, and as path segments they would leave /printers/: ".." would name the server
		// itself, which CUPS takes as its default destination, so cups:.. would print there
		if (queueName is "." or "..")
		{
			return null;
		}

		Uri printerUri = this.endpoint.GetPrinterUri(queueName);

		PrinterDescriptionAttributes? attributes = await IppPrinterQueries
			.GetAttributesAsync
			(
				this.context,
				printerUri,
				IppRequestedAttributes.PrinterSummaryAttributes,
				cancellationToken
			)
			.ConfigureAwait(false);

		if (attributes is null)
		{
			return null;
		}

		bool isDefaultQueue = isDefault
			?? queueName == await this.GetDefaultQueueNameAsync(cancellationToken).ConfigureAwait(false);

		try
		{
			PrinterInfo info = CupsPrintSystem.CreatePrinterInfo(attributes, queueName, isDefaultQueue);

			return new IppPrinter(this.context, printerUri, info);
		}
		catch (Exception exception) when (IppResponseErrors.IsUnreadableData(exception))
		{
			throw IppResponseErrors.CreateUnreadable(printerUri, exception);
		}
	}

	/// <summary>
	/// Releases the connection to the scheduler, if this print system owns it.
	/// </summary>
	public void Dispose()
	{
		this.ownedClient?.Dispose();
	}

	/// <summary>
	/// Creates printer information for a CUPS queue.
	/// </summary>
	/// <param name="attributes">The queue's IPP attributes.</param>
	/// <param name="queueName">The queue name.</param>
	/// <param name="isDefault">Whether it is the default queue.</param>
	/// <returns>The printer information.</returns>
	private static PrinterInfo CreatePrinterInfo
	(
		PrinterDescriptionAttributes attributes,
		string queueName,
		bool isDefault
	)
	{
		return IppPrinterInfoMapper.ToPrinterInfo
		(
			attributes,
			CupsPrintSystem.PrinterIdPrefix + queueName,
			queueName,
			isDefault
		);
	}

	/// <summary>
	/// Asks CUPS for the default queue's name.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The default queue's name, or <see langword="null"/> if none is set.</returns>
	private Task<string?> GetDefaultQueueNameAsync(CancellationToken cancellationToken)
	{
		// CUPS-Get-Default is the only reliable way: CUPS adds the default flag to printer-type when it reports
		// attributes, so filtering CUPS-Get-Printers on that flag never matches
		return this.context.Client.GetCupsDefaultPrinterNameAsync
		(
			this.endpoint.ServerUri,
			this.context.RequestingUserName,
			cancellationToken
		);
	}

	/// <summary>
	/// Creates a CUPS-Get-Printers request for the given attributes.
	/// </summary>
	/// <param name="requestedAttributes">The attributes to return for each queue.</param>
	/// <returns>The request.</returns>
	private CUPSGetPrintersRequest CreateGetPrintersRequest(string[] requestedAttributes)
	{
		return new CUPSGetPrintersRequest
		{
			OperationAttributes = new CUPSGetPrintersOperationAttributes
			{
				PrinterUri          = new IppValue<Uri>(this.endpoint.ServerUri),
				RequestingUserName  = new StringWithLanguage(this.context.RequestingUserName),
				RequestedAttributes = new IppValue<string[]>(requestedAttributes)
			}
		};
	}
}
