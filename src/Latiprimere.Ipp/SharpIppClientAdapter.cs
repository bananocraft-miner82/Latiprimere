using System.Net;
using Latiprimere.Ipp.Resources;
using SharpIpp;
using SharpIpp.Exceptions;
using SharpIpp.Models.Requests;
using SharpIpp.Models.Responses;
using SharpIpp.Protocol;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp;

/// <summary>
/// Sends IPP requests with SharpIppNext, translating its failures into <see cref="PrintException"/>.
/// </summary>
internal sealed class SharpIppClientAdapter : IIppClient, IDisposable
{
	/// <summary>The CUPS-Get-Default operation code, which SharpIppNext has no typed request for.</summary>
	private const short CupsGetDefaultOperation = 0x4001;

	/// <summary>The IPP major version sent with raw requests.</summary>
	private const byte IppMajorVersion = 1;

	/// <summary>The IPP minor version sent with raw requests.</summary>
	private const byte IppMinorVersion = 1;

	/// <summary>
	/// How long a request other than Print-Job may take, from sending to the whole answer: HttpClient's former
	/// default. Print-Job uploads the document, which can take much longer, so it has no limit of its own.
	/// </summary>
	internal static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromSeconds(100);

	private readonly HttpClient httpClient;   // The HTTP connection, owned by this adapter
	private readonly SharpIppClient client;   // SharpIppNext client sending over httpClient
	private readonly bool sendsCredentials;   // Whether httpClient adds a user name and password to requests
	private readonly TimeSpan responseTimeout; // The limit on requests other than Print-Job

	/// <summary>
	/// Initialises a new instance of the <see cref="SharpIppClientAdapter"/> class for an HTTP client that sends no
	/// credentials.
	/// </summary>
	/// <param name="httpClient">The HTTP client to send requests with. The adapter takes ownership of it.</param>
	public SharpIppClientAdapter(HttpClient httpClient)
		: this(httpClient, sendsCredentials: false)
	{
	}

	/// <summary>
	/// Initialises a new instance of the <see cref="SharpIppClientAdapter"/> class.
	/// </summary>
	/// <param name="httpClient">The HTTP client to send requests with. The adapter takes ownership of it.</param>
	/// <param name="sendsCredentials">Whether the HTTP client adds a user name and password to requests, which
	/// decides how a refusal is explained.</param>
	public SharpIppClientAdapter(HttpClient httpClient, bool sendsCredentials)
		: this(httpClient, sendsCredentials, SharpIppClientAdapter.DefaultResponseTimeout)
	{
	}

	/// <summary>
	/// Gets how the HTTP client protects requests, when it chooses credentials per printer as direct IPP's does, so a
	/// refusal can say whether credentials were sent; or <see langword="null"/> to go by the constructor's flag.
	/// </summary>
	internal Connections.IppConnectionSecurity? Security { get; init; }

	/// <summary>
	/// Gets the check of printers' certificates, which remembers the fingerprints it refuses, so a refusal can give
	/// the fingerprint to pin (ADR 0037); or <see langword="null"/>.
	/// </summary>
	internal Connections.IppCertificateTrust? CertificateTrust { get; init; }

	/// <summary>
	/// Initialises a new instance of the <see cref="SharpIppClientAdapter"/> class with a response timeout.
	/// </summary>
	/// <param name="httpClient">The HTTP client to send requests with. The adapter takes ownership of it.</param>
	/// <param name="sendsCredentials">Whether the HTTP client adds a user name and password to requests.</param>
	/// <param name="responseTimeout">How long a request other than Print-Job may take.</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="responseTimeout"/> isn't positive.</exception>
	internal SharpIppClientAdapter(HttpClient httpClient, bool sendsCredentials, TimeSpan responseTimeout)
	{
		ArgumentNullException.ThrowIfNull(httpClient);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(responseTimeout, TimeSpan.Zero);

		this.httpClient = httpClient;
		this.client = new SharpIppClient(httpClient);
		this.sendsCredentials = sendsCredentials;
		this.responseTimeout = responseTimeout;
	}

	/// <inheritdoc/>
	public Task<CUPSGetPrintersResponse> GetCupsPrintersAsync
	(
		CUPSGetPrintersRequest request,
		CancellationToken cancellationToken
	)
	{
		return this.SendAsync
		(
			request.OperationAttributes?.PrinterUri,
			token => this.client.GetCUPSPrintersAsync(request, token),
			isUpload: false,
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public Task<string?> GetCupsDefaultPrinterNameAsync(Uri serverUri, string requestingUserName, CancellationToken cancellationToken)
	{
		return this.SendAsync
		(
			new IppValue<Uri>(serverUri),
			token => this.SendCupsGetDefaultAsync(serverUri, requestingUserName, token),
			isUpload: false,
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public Task<GetPrinterAttributesResponse> GetPrinterAttributesAsync
	(
		GetPrinterAttributesRequest request,
		CancellationToken cancellationToken
	)
	{
		return this.SendAsync
		(
			request.OperationAttributes?.PrinterUri,
			token => this.client.GetPrinterAttributesAsync(request, token),
			isUpload: false,
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public Task<IReadOnlyList<IppAttribute>> GetPrinterAttributeListAsync
	(
		GetPrinterAttributesRequest request,
		CancellationToken cancellationToken
	)
	{
		return this.SendAsync
		(
			request.OperationAttributes?.PrinterUri,
			token => this.SendRawGetPrinterAttributesAsync(request, token),
			isUpload: false,
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public Task ValidateJobAsync(ValidateJobRequest request, IppVendorAttributes vendorAttributes, CancellationToken cancellationToken)
	{
		return this.SendAsync
		(
			request.OperationAttributes?.PrinterUri,
			token => this.SendRawValidateJobAsync(request, vendorAttributes, token),
			isUpload: false,
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public Task<PrintJobResponse> PrintJobAsync(PrintJobRequest request, IppVendorAttributes vendorAttributes, CancellationToken cancellationToken)
	{
		// The typed request can't carry attributes SharpIppNext doesn't model, so a job with vendor attributes
		// goes in its raw form, which SharpIppNext sends and checks the same way
		Func<CancellationToken, Task<PrintJobResponse>> send = vendorAttributes.IsEmpty
			? token => this.client.PrintJobAsync(request, token)
			: token => this.SendRawPrintJobAsync(request, vendorAttributes, token);

		return this.SendAsync(request.OperationAttributes?.PrinterUri, send, isUpload: true, cancellationToken);
	}

	/// <summary>
	/// Reads the parts of a raw Print-Job response that <see cref="IppPrinter"/> uses: the status, and the job's
	/// ID and state.
	/// </summary>
	/// <param name="response">The raw response.</param>
	/// <returns>The response.</returns>
	internal static PrintJobResponse ToPrintJobResponse(IIppResponseMessage response)
	{
		IppAttribute[] jobAttributes = response.JobAttributes.SelectMany(group => group).ToArray();
		int? jobId = SharpIppClientAdapter.FindInteger(jobAttributes, "job-id");
		int? jobState = SharpIppClientAdapter.FindInteger(jobAttributes, "job-state");

		// IppPrinter treats a response without a job ID as an error, as for the typed response
		return new PrintJobResponse
		{
			Version       = response.Version,
			StatusCode    = response.StatusCode,
			RequestId     = response.RequestId,
			JobAttributes = jobId is null
				? null
				: new JobAttributes { JobId = jobId.Value, JobState = (JobState)(jobState ?? (int)JobState.Pending) }
		};
	}

	/// <summary>
	/// Finds an integer or enum attribute's first value.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <param name="name">The attribute's name.</param>
	/// <returns>The value, or <see langword="null"/> if absent or not an integer.</returns>
	private static int? FindInteger(IEnumerable<IppAttribute> attributes, string name)
	{
		return attributes
			.Where(attribute => attribute.Name == name && attribute.Value is int)
			.Select(attribute => (int?)(int)attribute.Value)
			.FirstOrDefault();
	}

	/// <summary>
	/// Sends a Print-Job request in its raw form, with the vendor attributes added to their groups.
	/// </summary>
	/// <param name="request">The typed request.</param>
	/// <param name="vendorAttributes">The attributes to add.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The response.</returns>
	private async Task<PrintJobResponse> SendRawPrintJobAsync
	(
		PrintJobRequest     request,
		IppVendorAttributes vendorAttributes,
		CancellationToken   cancellationToken
	)
	{
		IIppRequestMessage raw = this.client.CreateRawRequest(request);
		raw.OperationAttributes.AddRange(vendorAttributes.Operation);
		raw.JobAttributes.AddRange(vendorAttributes.Job);

		Uri printerUri = request.OperationAttributes?.PrinterUri?.Value
						 ?? throw new ArgumentException(ExceptionMessages.IppRequestWithoutPrinterUri, nameof(request));

		IIppResponseMessage response = await this.client.SendAsync(printerUri, raw, cancellationToken).ConfigureAwait(false);

		return SharpIppClientAdapter.ToPrintJobResponse(response);
	}

	/// <summary>
	/// Sends a Validate-Job request in its raw form, with the vendor attributes added to their groups, as Print-Job
	/// sends them, so the printer judges exactly what the job will carry.
	/// </summary>
	/// <param name="request">The typed request.</param>
	/// <param name="vendorAttributes">The attributes to add.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>True, once the printer has accepted the attributes; SharpIppNext throws for a refusal.</returns>
	private async Task<bool> SendRawValidateJobAsync
	(
		ValidateJobRequest  request,
		IppVendorAttributes vendorAttributes,
		CancellationToken   cancellationToken
	)
	{
		IIppRequestMessage raw = this.client.CreateRawRequest(request);
		raw.OperationAttributes.AddRange(vendorAttributes.Operation);
		raw.JobAttributes.AddRange(vendorAttributes.Job);

		Uri printerUri = request.OperationAttributes?.PrinterUri?.Value
						 ?? throw new ArgumentException(ExceptionMessages.IppRequestWithoutPrinterUri, nameof(request));

		await this.client.SendAsync(printerUri, raw, cancellationToken).ConfigureAwait(false);

		return true;
	}

	/// <summary>
	/// Sends a Get-Printer-Attributes request in its raw form, and reads the printer attributes as they came.
	/// </summary>
	/// <param name="request">The typed request.</param>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>The printer attributes, from every printer attributes group.</returns>
	private async Task<IReadOnlyList<IppAttribute>> SendRawGetPrinterAttributesAsync
	(
		GetPrinterAttributesRequest request,
		CancellationToken           cancellationToken
	)
	{
		IIppRequestMessage raw = this.client.CreateRawRequest(request);

		Uri printerUri = request.OperationAttributes?.PrinterUri?.Value
						 ?? throw new ArgumentException(ExceptionMessages.IppRequestWithoutPrinterUri, nameof(request));

		IIppResponseMessage response = await this.client.SendAsync(printerUri, raw, cancellationToken).ConfigureAwait(false);

		return response.PrinterAttributes.SelectMany(group => group).ToList();
	}

	/// <inheritdoc/>
	public Task<GetJobAttributesResponse> GetJobAttributesAsync(GetJobAttributesRequest request, CancellationToken cancellationToken)
	{
		return this.SendAsync
		(
			request.OperationAttributes?.PrinterUri,
			token => this.client.GetJobAttributesAsync(request, token),
			isUpload: false,
			cancellationToken
		);
	}

	/// <inheritdoc/>
	public Task<CancelJobResponse> CancelJobAsync(CancelJobRequest request, CancellationToken cancellationToken)
	{
		return this.SendAsync
		(
			request.OperationAttributes?.PrinterUri,
			token => this.client.CancelJobAsync(request, token),
			isUpload: false,
			cancellationToken
		);
	}

	/// <summary>
	/// Releases the HTTP connection.
	/// </summary>
	public void Dispose()
	{
		// SharpIppNext doesn't dispose an HTTP client it was given, so the adapter does
		this.client.Dispose();
		this.httpClient.Dispose();
	}

	/// <summary>
	/// Sends a request, translating SharpIppNext and HTTP failures into <see cref="PrintException"/>.
	/// </summary>
	/// <typeparam name="TResponse">The response type.</typeparam>
	/// <param name="printerUri">The URI the request is sent to, for error messages.</param>
	/// <param name="send">Sends the request with the token it is given.</param>
	/// <param name="isUpload">Whether the request uploads a document (Print-Job), which isn't limited by the
	/// response timeout; every other request is.</param>
	/// <param name="cancellationToken">The caller's token.</param>
	/// <returns>The response.</returns>
	/// <exception cref="PrintException">The request failed or was rejected, or didn't finish within the response
	/// timeout.</exception>
	private async Task<TResponse> SendAsync<TResponse>
	(
		IppValue<Uri>?                           printerUri,
		Func<CancellationToken, Task<TResponse>> send,
		bool                                     isUpload,
		CancellationToken                        cancellationToken
	)
	{
		string target = printerUri?.Value?.ToString() ?? string.Empty;
		using CancellationTokenSource? responseTimeout = isUpload
			? null
			: CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

		responseTimeout?.CancelAfter(this.responseTimeout);

		try
		{
			return await send(responseTimeout?.Token ?? cancellationToken).ConfigureAwait(false);
		}
		catch (IppResponseException exception)
			when (exception.InnerException is HttpRequestException { StatusCode: HttpStatusCode httpStatus }
				&& SharpIppClientAdapter.IsRefusal(httpStatus))
		{
			// An HTTP refusal that came with an IPP body, which SharpIppNext decodes before rethrowing
			throw this.CreateRefusedException(target, httpStatus, exception);
		}
		catch (IppResponseException exception)
			when (exception.ResponseMessage is null || exception.InnerException is not (null or HttpRequestException))
		{
			// No IPP answer could be read, so there is no IPP status to report. SharpIppNext checks the
			// Content-Type first and gives no response for an answer that isn't IPP at all, as from a web
			// interface. It gives one that failed to parse partway as an inner exception, alongside the part read,
			// whose status is only the default.
			string message = exception.InnerException is null
				? ExceptionMessages.FormatIppResponseNotIpp(target)
				: ExceptionMessages.FormatIppResponseUnreadable(target);

			throw new PrintException(message, exception);
		}
		catch (IppResponseException exception)
		{
			// The print system answered, but with an error status
			IppStatusCode statusCode = exception.ResponseMessage?.StatusCode ?? IppStatusCode.ServerErrorInternalError;

			throw this.CreateRejectedException(target, statusCode, exception);
		}
		catch (IppRequestException exception)
		{
			// The request was refused before or while it was sent
			throw this.CreateRejectedException(target, exception.StatusCode, exception);
		}
		catch (HttpRequestException exception)
			when (exception.StatusCode is HttpStatusCode httpStatus && SharpIppClientAdapter.IsRefusal(httpStatus))
		{
			// CUPS answers authentication failures with an HTML page, not IPP
			throw this.CreateRefusedException(target, exception.StatusCode.Value, exception);
		}
		catch (HttpRequestException exception) when (exception.HttpRequestError == HttpRequestError.SecureConnectionError)
		{
			// The TLS handshake failed, most often because the server's certificate isn't trusted: for a printer
			// whose certificate was refused, say which, so it can be pinned
			_ = Uri.TryCreate(target, UriKind.Absolute, out Uri? targetUri);

			Connections.RefusedCertificate? refused = this.CertificateTrust is not null && targetUri is not null
				? this.CertificateTrust.FindRefusal(targetUri.Host)
				: null;

			if (refused is null)
			{
				throw new PrintException(ExceptionMessages.FormatIppSecureConnectionFailed(target), exception);
			}

			// The fingerprint goes in the exception as well as the message, so an application can offer to pin it
			string message = refused.IsTrustedBySystem
				? ExceptionMessages.FormatIppCertificateNotVouchedForCredentials(target, refused.Fingerprint)
				: ExceptionMessages.FormatIppCertificateNotTrusted(target, refused.Fingerprint);

			throw new PrintCertificateException(message, exception)
			{
				CertificateFingerprint = refused.Fingerprint,
				PrinterUri             = targetUri
			};
		}
		catch (HttpRequestException exception)
		{
			// The print system could not be reached, or answered with an HTTP error such as 401
			string message = ExceptionMessages.FormatIppConnectionFailed(target);

			throw new PrintException(message, exception)
			{
				NativeErrorCode = (int?)exception.StatusCode
			};
		}
		catch (OperationCanceledException exception)
			when (exception.InnerException is TimeoutException
				|| (responseTimeout?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested))
		{
			// The connection didn't open within the connect timeout, or no answer came within the response
			// timeout. Both are failures to reach the print system, not cancellations by the caller, whose own
			// cancellation passes through unchanged.
			string message = ExceptionMessages.FormatIppConnectionTimedOut(target);

			throw new PrintException(message, exception);
		}
	}

	/// <summary>
	/// Builds a raw CUPS-Get-Default request asking only for the queue name.
	/// </summary>
	/// <param name="serverUri">The base URI of the CUPS server, sent as <c>printer-uri</c>.</param>
	/// <param name="requestingUserName">The user name to send.</param>
	/// <returns>The request message.</returns>
	/// <remarks>
	/// CUPS ignores <c>printer-uri</c> for this operation, but SharpIppNext rejects requests without one before
	/// sending them.
	/// </remarks>
	private static IppRequestMessage CreateCupsGetDefaultRequest(Uri serverUri, string requestingUserName)
	{
		return new IppRequestMessage
		{
			Version      = new IppVersion(SharpIppClientAdapter.IppMajorVersion, SharpIppClientAdapter.IppMinorVersion),
			IppOperation = (IppOperation)SharpIppClientAdapter.CupsGetDefaultOperation,
			RequestId    = 1,
			OperationAttributes =
			{
				new IppAttribute(Tag.Charset, "attributes-charset", "utf-8"),
				new IppAttribute(Tag.NaturalLanguage, "attributes-natural-language", "en"),
				new IppAttribute(Tag.Uri, "printer-uri", serverUri.ToString()),
				new IppAttribute(Tag.NameWithoutLanguage, "requesting-user-name", requestingUserName),
				new IppAttribute(Tag.Keyword, "requested-attributes", IppRequestedAttributes.PrinterName)
			}
		};
	}

	/// <summary>
	/// Sends a CUPS-Get-Default request and reads the default queue's name.
	/// </summary>
	/// <param name="serverUri">The base URI of the CUPS server.</param>
	/// <param name="requestingUserName">The user name to send.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The default queue's name, or <see langword="null"/> if CUPS has no default.</returns>
	/// <exception cref="IppResponseException">CUPS returned an error other than not-found.</exception>
	private async Task<string?> SendCupsGetDefaultAsync(Uri serverUri, string requestingUserName, CancellationToken cancellationToken)
	{
		IppRequestMessage request = SharpIppClientAdapter.CreateCupsGetDefaultRequest(serverUri, requestingUserName);
		IIppResponseMessage response;

		try
		{
			response = await this.client.SendAsync(serverUri, request, cancellationToken).ConfigureAwait(false);
		}
		catch (IppResponseException exception) when (exception.ResponseMessage?.StatusCode == IppStatusCode.ClientErrorNotFound)
		{
			// SharpIppNext throws for every error status, before the response can be read. For CUPS-Get-Default,
			// not-found means that no default destination is set, as on a Mac with no printer chosen as default.
			return null;
		}

		return SharpIppClientAdapter.ReadDefaultPrinterName(response);
	}

	/// <summary>
	/// Reads the queue name from a CUPS-Get-Default response.
	/// </summary>
	/// <param name="response">The response message.</param>
	/// <returns>The default queue's name, or <see langword="null"/> if CUPS has no default.</returns>
	/// <exception cref="IppResponseException">CUPS returned an error other than not-found.</exception>
	private static string? ReadDefaultPrinterName(IIppResponseMessage response)
	{
		// CUPS answers client-error-not-found when no default destination is set
		if (response.StatusCode == IppStatusCode.ClientErrorNotFound)
		{
			return null;
		}

		if (response.StatusCode >= IppStatusCode.ClientErrorBadRequest)
		{
			throw new IppResponseException(response);
		}

		object? value = response.PrinterAttributes
			.SelectMany(attributeGroup => attributeGroup)
			.Where(attribute => attribute.Name == IppRequestedAttributes.PrinterName)
			.Select(attribute => attribute.Value)
			.FirstOrDefault();

		return value switch
		{
			string name => name,
			StringWithLanguage nameWithLanguage => nameWithLanguage.Value,
			_ => null,
		};
	}

	/// <summary>
	/// Creates the exception for a request the print system rejected.
	/// </summary>
	/// <param name="target">The URI the request was sent to.</param>
	/// <param name="statusCode">The IPP status code returned.</param>
	/// <param name="innerException">The SharpIppNext exception.</param>
	/// <returns>The exception to throw.</returns>
	private PrintException CreateRejectedException(string target, IppStatusCode statusCode, Exception innerException)
	{
		// IPP's own authentication statuses, which printers send where CUPS uses HTTP
		if (statusCode == IppStatusCode.ClientErrorNotAuthenticated)
		{
			return this.CreateAuthenticationException(target, (int)statusCode, innerException);
		}

		string message = statusCode == IppStatusCode.ClientErrorNotAuthorized
			? ExceptionMessages.FormatIppRequestForbidden(target)
			: ExceptionMessages.FormatIppRequestRejected(target, statusCode);

		return new PrintException(message, innerException)
		{
			NativeErrorCode = (int)statusCode
		};
	}

	/// <summary>
	/// Creates the exception for an HTTP refusal: authentication needed or not accepted, encryption needed, or the
	/// user not allowed.
	/// </summary>
	/// <param name="target">The URI the request was sent to.</param>
	/// <param name="httpStatus">The HTTP status, one that <see cref="IsRefusal"/> accepts.</param>
	/// <param name="innerException">The exception that reported it.</param>
	/// <returns>The exception to throw.</returns>
	private PrintException CreateRefusedException(string target, HttpStatusCode httpStatus, Exception innerException)
	{
		if (httpStatus == HttpStatusCode.Unauthorized)
		{
			return this.CreateAuthenticationException(target, (int)httpStatus, innerException);
		}

		if (httpStatus == HttpStatusCode.UpgradeRequired)
		{
			// CUPS asks for TLS before it asks for a password. Without credentials, the password is what's missing
			string message = this.sendsCredentials
				? ExceptionMessages.FormatIppEncryptionRequired(target)
				: ExceptionMessages.FormatIppEncryptedAuthenticationRequired(target);

			return new PrintAuthenticationException(message, innerException)
			{
				NativeErrorCode = (int)httpStatus
			};
		}

		return new PrintException(ExceptionMessages.FormatIppRequestForbidden(target), innerException)
		{
			NativeErrorCode = (int)httpStatus
		};
	}

	/// <summary>
	/// Creates the exception for a request that needs credentials, or whose credentials weren't accepted.
	/// </summary>
	/// <param name="target">The URI the request was sent to.</param>
	/// <param name="nativeErrorCode">The HTTP or IPP status.</param>
	/// <param name="innerException">The exception that reported it.</param>
	/// <returns>The exception to throw.</returns>
	private PrintAuthenticationException CreateAuthenticationException(string target, int nativeErrorCode, Exception innerException)
	{
		bool sent = this.sendsCredentials;
		bool withheld = false;

		// Direct IPP chooses credentials per printer, and sends them only over TLS (ADR 0037)
		if (this.Security is not null && Uri.TryCreate(target, UriKind.Absolute, out Uri? targetUri))
		{
			sent = this.Security.FindCredentialsToSend(targetUri) is not null;
			withheld = !sent && this.Security.FindCredentials(targetUri) is not null;
		}

		string message = sent
			? ExceptionMessages.FormatIppCredentialsRejected(target)
			: withheld
				? ExceptionMessages.FormatIppCredentialsNeedIpps(target)
				: ExceptionMessages.FormatIppAuthenticationRequired(target);

		return new PrintAuthenticationException(message, innerException)
		{
			NativeErrorCode = nativeErrorCode
		};
	}

	/// <summary>
	/// Checks whether an HTTP status refuses the request for who sent it or how: 401, 403 or 426.
	/// </summary>
	/// <param name="httpStatus">The HTTP status.</param>
	/// <returns><see langword="true"/> for a refusal.</returns>
	private static bool IsRefusal(HttpStatusCode httpStatus)
	{
		return httpStatus is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.UpgradeRequired;
	}
}
