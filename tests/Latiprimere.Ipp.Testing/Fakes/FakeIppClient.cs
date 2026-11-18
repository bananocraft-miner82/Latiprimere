using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Testing.Fakes;

/// <summary>
/// An in-memory IPP client that returns configured responses and records the requests it receives.
/// </summary>
internal sealed class FakeIppClient : IIppClient, IDisposable
{
	private readonly List<ValidateJobRequest> validateJobRequests = []; // Validate-Job requests received
	private int attributeListRequestCount; // Raw Get-Printer-Attributes requests made
	private readonly Dictionary<string, IReadOnlyList<IppAttribute>> attributeListsByUri =
		new Dictionary<string, IReadOnlyList<IppAttribute>>(StringComparer.Ordinal); // Raw attributes, by URI
	private readonly Dictionary<string, PrinterDescriptionAttributes> printersByUri =
		new Dictionary<string, PrinterDescriptionAttributes>(); // Printer attributes, by printer URI
	private readonly List<CUPSGetPrintersRequest> cupsGetPrintersRequests =
		new List<CUPSGetPrintersRequest>(); // CUPS-Get-Printers requests received
	private readonly List<GetPrinterAttributesRequest> getPrinterAttributesRequests =
		new List<GetPrinterAttributesRequest>(); // Get-Printer-Attributes requests received
	private readonly HashSet<string> unreachableUris = new HashSet<string>(); // URIs that fail to connect
	private readonly HashSet<string> unresponsiveUris = new HashSet<string>(); // URIs that never answer
	private readonly List<byte[]> receivedDocuments = new List<byte[]>(); // Document bytes of each Print-Job, in order

	/// <summary>Gets or sets the printers returned by CUPS-Get-Printers.</summary>
	public PrinterDescriptionAttributes[] CupsPrinters { get; set; } = [];

	/// <summary>Gets or sets the queue name returned by CUPS-Get-Default, or <see langword="null"/> for none.</summary>
	public string? DefaultPrinterName { get; set; }

	/// <summary>Gets the number of CUPS-Get-Default requests sent.</summary>
	public int CupsGetDefaultRequestCount { get; private set; }

	/// <summary>Gets or sets the job ID returned by Print-Job. Zero simulates a response without one.</summary>
	public int PrintJobId { get; set; } = 1;

	/// <summary>Gets or sets the job attributes returned by Get-Job-Attributes.</summary>
	public JobDescriptionAttributes? JobAttributes { get; set; }

	/// <summary>Gets or sets an exception to throw from every operation, simulating a failure.</summary>
	public PrintException? FailureToThrow { get; set; }

	/// <summary>
	/// Gets or sets an exception to throw from Get-Job-Attributes and Cancel-Job only, such as not-found for a job
	/// the printer has forgotten while the printer itself still answers.
	/// </summary>
	public PrintException? JobFailureToThrow { get; set; }

	/// <summary>
	/// Gets or sets how many Print-Job requests to turn away with <c>server-error-busy</c> before accepting one.
	/// </summary>
	public int BusyPrintJobCount { get; set; }

	/// <summary>Gets the document bytes each Print-Job request carried, busy ones included, in order.</summary>
	public IReadOnlyList<byte[]> ReceivedDocuments
	{
		get
		{
			return this.receivedDocuments;
		}
	}

	/// <summary>Gets the CUPS-Get-Printers requests received.</summary>
	public IReadOnlyList<CUPSGetPrintersRequest> CupsGetPrintersRequests
	{
		get
		{
			return this.cupsGetPrintersRequests;
		}
	}

	/// <summary>Gets the Get-Printer-Attributes requests received.</summary>
	public IReadOnlyList<GetPrinterAttributesRequest> GetPrinterAttributesRequests
	{
		get
		{
			return this.getPrinterAttributesRequests;
		}
	}

	/// <summary>Gets the last Print-Job request received.</summary>
	public PrintJobRequest? LastPrintJobRequest { get; private set; }

	/// <summary>Gets or sets a failure Print-Job throws after reading the document, or <see langword="null"/>.</summary>
	public PrintException? PrintJobFailureToThrow { get; set; }

	/// <summary>Gets the vendor attributes sent with the last Print-Job request, or <see langword="null"/> before the first.</summary>
	public IppVendorAttributes? LastVendorAttributes { get; private set; }

	/// <summary>Gets the last Get-Job-Attributes request received.</summary>
	public GetJobAttributesRequest? LastGetJobAttributesRequest { get; private set; }

	/// <summary>Gets the last Cancel-Job request received.</summary>
	public CancelJobRequest? LastCancelJobRequest { get; private set; }

	/// <summary>Gets a value indicating whether the client has been disposed.</summary>
	public bool IsDisposed { get; private set; }

	/// <summary>
	/// Makes Get-Printer-Attributes return the given attributes for a printer URI. Other URIs answer not-found.
	/// </summary>
	/// <param name="printerUri">The printer URI.</param>
	/// <param name="attributes">The attributes to return.</param>
	public void AddPrinter(Uri printerUri, PrinterDescriptionAttributes attributes)
	{
		this.printersByUri[printerUri.ToString()] = attributes;
	}

	/// <summary>
	/// Makes Get-Printer-Attributes for a printer URI fail as if nothing could be connected to there.
	/// </summary>
	/// <param name="printerUri">The printer URI.</param>
	public void AddUnreachable(Uri printerUri)
	{
		this.unreachableUris.Add(printerUri.ToString());
	}

	/// <summary>
	/// Makes every printer reachable again, as if they had come back on.
	/// </summary>
	public void ClearUnreachable()
	{
		this.unreachableUris.Clear();
	}

	/// <summary>
	/// Makes Get-Printer-Attributes for a printer URI wait until it is cancelled, as if the host never answered.
	/// </summary>
	/// <param name="printerUri">The printer URI.</param>
	public void AddUnresponsive(Uri printerUri)
	{
		this.unresponsiveUris.Add(printerUri.ToString());
	}

	/// <summary>Returns the configured CUPS printers.</summary>
	/// <param name="request">The request, which is recorded.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The response.</returns>
	public Task<CUPSGetPrintersResponse> GetCupsPrintersAsync
	(
		CUPSGetPrintersRequest request,
		CancellationToken cancellationToken
	)
	{
		this.ThrowIfFailing();
		this.cupsGetPrintersRequests.Add(request);

		return Task.FromResult(new CUPSGetPrintersResponse { PrintersAttributes = this.CupsPrinters });
	}

	/// <summary>Returns the configured default queue name.</summary>
	/// <param name="serverUri">Not used.</param>
	/// <param name="requestingUserName">Not used.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The default queue name.</returns>
	public Task<string?> GetCupsDefaultPrinterNameAsync
	(
		Uri serverUri,
		string requestingUserName,
		CancellationToken cancellationToken
	)
	{
		this.CupsGetDefaultRequestCount++;
		this.ThrowIfFailing();

		return Task.FromResult(this.DefaultPrinterName);
	}

	/// <summary>
	/// Returns the attributes added for the request's printer URI, fails for unreachable or unresponsive URIs, or
	/// answers not-found.
	/// </summary>
	/// <param name="request">The request, which is recorded.</param>
	/// <param name="cancellationToken">Ends the wait for an unresponsive URI.</param>
	/// <returns>The response.</returns>
	/// <exception cref="PrintException">
	/// The URI is unreachable (no IPP status), or no printer was added for it (the IPP not-found status).
	/// </exception>
	/// <exception cref="OperationCanceledException">The URI is unresponsive and the wait was cancelled.</exception>
	public async Task<GetPrinterAttributesResponse> GetPrinterAttributesAsync
	(
		GetPrinterAttributesRequest request,
		CancellationToken cancellationToken
	)
	{
		this.ThrowIfFailing();
		this.getPrinterAttributesRequests.Add(request);

		string printerUri = request.OperationAttributes?.PrinterUri?.Value?.ToString() ?? string.Empty;

		if (this.unresponsiveUris.Contains(printerUri))
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
		}

		if (this.unreachableUris.Contains(printerUri))
		{
			throw new PrintException("Could not connect.");
		}

		if (!this.printersByUri.TryGetValue(printerUri, out PrinterDescriptionAttributes? attributes))
		{
			throw new PrintException("Not found.") { NativeErrorCode = (int)IppStatusCode.ClientErrorNotFound };
		}

		return new GetPrinterAttributesResponse { PrinterAttributes = attributes };
	}

	/// <summary>Gets or sets an exception the raw Get-Printer-Attributes request alone throws, as from a printer
	/// that refuses the job-template group.</summary>
	public PrintException? AttributeListFailureToThrow { get; set; }

	/// <summary>Gets how many raw Get-Printer-Attributes requests were made.</summary>
	public int AttributeListRequestCount
	{
		get
		{
			return this.attributeListRequestCount;
		}
	}

	/// <summary>
	/// Sets the raw printer attributes a printer reports, for its vendor options.
	/// </summary>
	/// <param name="printerUri">The printer's URI.</param>
	/// <param name="attributes">The attributes.</param>
	public void SetAttributeList(Uri printerUri, IReadOnlyList<IppAttribute> attributes)
	{
		this.attributeListsByUri[printerUri.ToString()] = attributes;
	}

	/// <summary>
	/// Returns the raw attributes set for the printer, or none, failing as <see cref="GetPrinterAttributesAsync"/>
	/// does.
	/// </summary>
	/// <param name="request">The request, which is recorded.</param>
	/// <param name="cancellationToken">Ends the wait for an unresponsive URI.</param>
	/// <returns>The attributes.</returns>
	public async Task<IReadOnlyList<IppAttribute>> GetPrinterAttributeListAsync
	(
		GetPrinterAttributesRequest request,
		CancellationToken cancellationToken
	)
	{
		// Fails as the typed call does, but is recorded apart, so tests counting typed requests are unaffected
		this.ThrowIfFailing();
		this.attributeListRequestCount++;

		if (this.AttributeListFailureToThrow is not null)
		{
			throw this.AttributeListFailureToThrow;
		}

		string printerUri = request.OperationAttributes?.PrinterUri?.Value?.ToString() ?? string.Empty;

		if (this.unresponsiveUris.Contains(printerUri))
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
		}

		if (this.unreachableUris.Contains(printerUri))
		{
			throw new PrintException("Could not connect.");
		}

		if (!this.printersByUri.ContainsKey(printerUri))
		{
			throw new PrintException("Not found.") { NativeErrorCode = (int)IppStatusCode.ClientErrorNotFound };
		}

		return this.attributeListsByUri.GetValueOrDefault(printerUri) ?? Array.Empty<IppAttribute>();
	}

	/// <summary>Gets the Validate-Job requests received, in order.</summary>
	public IReadOnlyList<ValidateJobRequest> ValidateJobRequests
	{
		get
		{
			return this.validateJobRequests;
		}
	}

	/// <summary>Gets the vendor attributes the last Validate-Job carried, or <see langword="null"/>.</summary>
	public IppVendorAttributes? LastValidateJobVendorAttributes { get; private set; }

	/// <summary>Gets or sets an exception Validate-Job throws, as a printer's refusal (an IPP client error) or a busy
	/// answer would.</summary>
	public PrintException? ValidateJobFailureToThrow { get; set; }

	/// <summary>
	/// Records the request, then accepts it or throws <see cref="ValidateJobFailureToThrow"/>.
	/// </summary>
	/// <param name="request">The request, which is recorded.</param>
	/// <param name="vendorAttributes">The vendor attributes, which are recorded.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>A completed task, or a faulted one.</returns>
	public Task ValidateJobAsync(ValidateJobRequest request, IppVendorAttributes vendorAttributes, CancellationToken cancellationToken)
	{
		this.ThrowIfFailing();
		this.validateJobRequests.Add(request);
		this.LastValidateJobVendorAttributes = vendorAttributes;

		return this.ValidateJobFailureToThrow is null ? Task.CompletedTask : Task.FromException(this.ValidateJobFailureToThrow);
	}

	/// <summary>
	/// Records the request and reads its document, as sending it would; then turns it away as busy if
	/// <see cref="BusyPrintJobCount"/> says so, or returns the configured job ID.
	/// </summary>
	/// <param name="request">The request, which is recorded.</param>
	/// <param name="vendorAttributes">The vendor attributes, which are recorded.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The response.</returns>
	/// <exception cref="PrintException">A failure has been configured, or the request is turned away as busy.</exception>
	public Task<PrintJobResponse> PrintJobAsync(PrintJobRequest request, IppVendorAttributes vendorAttributes, CancellationToken cancellationToken)
	{
		this.ThrowIfFailing();
		this.LastPrintJobRequest = request;
		this.LastVendorAttributes = vendorAttributes;
		this.receivedDocuments.Add(FakeIppClient.ReadDocument(request.Document));

		if (this.PrintJobFailureToThrow is not null)
		{
			throw this.PrintJobFailureToThrow;
		}

		if (this.BusyPrintJobCount > 0)
		{
			this.BusyPrintJobCount--;

			throw new PrintException("Busy.") { NativeErrorCode = (int)IppStatusCode.ServerErrorBusy };
		}

		return Task.FromResult(new PrintJobResponse
		{
			JobAttributes = new JobAttributes { JobId = this.PrintJobId, JobState = JobState.Pending }
		});
	}

	/// <summary>Records the request and returns the configured job attributes.</summary>
	/// <param name="request">The request, which is recorded.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The response.</returns>
	public Task<GetJobAttributesResponse> GetJobAttributesAsync
	(
		GetJobAttributesRequest request,
		CancellationToken cancellationToken
	)
	{
		this.ThrowIfFailing();
		this.LastGetJobAttributesRequest = request;

		if (this.JobFailureToThrow is not null)
		{
			throw this.JobFailureToThrow;
		}

		// The null-forgiving operator is deliberate: tests use a null JobAttributes to simulate an empty response
		return Task.FromResult(new GetJobAttributesResponse { JobAttributes = this.JobAttributes! });
	}

	/// <summary>Records the request.</summary>
	/// <param name="request">The request, which is recorded.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The response.</returns>
	public Task<CancelJobResponse> CancelJobAsync(CancelJobRequest request, CancellationToken cancellationToken)
	{
		this.ThrowIfFailing();
		this.LastCancelJobRequest = request;

		if (this.JobFailureToThrow is not null)
		{
			throw this.JobFailureToThrow;
		}

		return Task.FromResult(new CancelJobResponse());
	}

	/// <summary>Marks the client as disposed.</summary>
	public void Dispose()
	{
		this.IsDisposed = true;
	}

	/// <summary>Reads a request's document from where it stands to its end.</summary>
	/// <param name="document">The document stream.</param>
	/// <returns>The bytes read; none when there is no document.</returns>
	private static byte[] ReadDocument(Stream? document)
	{
		if (document is null)
		{
			return [];
		}

		using MemoryStream copy = new MemoryStream();
		document.CopyTo(copy);

		return copy.ToArray();
	}

	/// <summary>Throws the configured failure, if any.</summary>
	/// <exception cref="PrintException">A failure has been configured.</exception>
	private void ThrowIfFailing()
	{
		if (this.FailureToThrow is not null)
		{
			throw this.FailureToThrow;
		}
	}
}
