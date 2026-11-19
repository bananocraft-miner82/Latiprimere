using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpIpp.Protocol;
using SharpIpp.Protocol.Models;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// A scripted IPP printer on a loopback address: it answers like a printer, records every request, and misbehaves
/// when its <see cref="Behaviour"/> says so. Nothing is printed.
/// </summary>
/// <remarks>
/// It answers Get-Printer-Attributes from its <see cref="FakePrinterProfile"/>, and accepts Print-Job,
/// Validate-Job, Get-Job-Attributes, Get-Jobs and Cancel-Job. Start one with <see cref="StartAsync"/> and dispose it
/// to stop it.
/// </remarks>
public sealed class FakePrinter : IAsyncDisposable
{
	private readonly WebApplication app;                                      // The Kestrel server
	private readonly FakePrinterOptions options;                           // How it was set up
	private readonly IppResponder responder;                                  // Answers requests and keeps jobs
	private readonly X509Certificate2? certificate;                           // For ipps, or null
	private readonly IppProtocol protocol = new IppProtocol();                // Reads requests and writes answers
	private readonly List<RecordedRequest> requests = new List<RecordedRequest>(); // Every request, in order
	private readonly Lock stateLock = new Lock();                             // Guards responder and requests
	private bool isDisposed;                                                  // Whether it has been stopped

	/// <summary>
	/// Initialises a new instance of the <see cref="FakePrinter"/> class, ready to start.
	/// </summary>
	/// <param name="options">How to set it up.</param>
	/// <param name="profile">Its attributes.</param>
	/// <param name="certificate">The certificate for ipps, or <see langword="null"/>.</param>
	private FakePrinter(FakePrinterOptions options, FakePrinterProfile profile, X509Certificate2? certificate)
	{
		this.options = options;
		this.Profile = profile;
		this.certificate = certificate;
		this.responder = new IppResponder(profile, this.Behaviour, certificate is not null, options.RequiredCredentials is not null);
		this.protocol.ReadDocumentStream = true;
		this.protocol.MaxDocumentStreamBytes = null;

		IPAddress address = options.Address switch
		{
			FakePrinterAddress.IPv6Loopback => IPAddress.IPv6Loopback,
			FakePrinterAddress.AnyIPv4 => IPAddress.Any,
			_ => IPAddress.Loopback
		};

		WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
		builder.Logging.ClearProviders();

		builder.WebHost.ConfigureKestrel
		(
			kestrel =>
			{
				kestrel.Limits.MaxRequestBodySize = null;

				kestrel.Listen
				(
					address,
					options.Port,
					listen =>
					{
						listen.Protocols = HttpProtocols.Http1;

						if (certificate is not null)
						{
							listen.UseHttps(certificate);
						}
					}
				);
			}
		);

		this.app = builder.Build();
		this.app.Run(this.HandleAsync);
	}

	/// <summary>
	/// Gets the SHA-256 fingerprint of the printer's certificate, as colon-separated pairs of upper-case hexadecimal
	/// digits, or <see langword="null"/> without TLS.
	/// </summary>
	public string? CertificateFingerprint
	{
		get
		{
			return this.certificate is null
				? null
				: string.Join(':', this.certificate.GetCertHash(System.Security.Cryptography.HashAlgorithmName.SHA256).Select(value => value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
		}
	}

	/// <summary>
	/// Gets the printer's URI, such as <c>ipp://127.0.0.1:54321/ipp/print</c>.
	/// </summary>
	public Uri PrinterUri
	{
		get
		{
			return this.responder.PrinterUri;
		}
	}

	/// <summary>
	/// Gets what the printer does; change it at any time.
	/// </summary>
	public FakePrinterBehaviour Behaviour { get; } = new FakePrinterBehaviour();

	/// <summary>
	/// Gets the printer's attributes; change them at any time.
	/// </summary>
	public FakePrinterProfile Profile { get; }

	/// <summary>
	/// Gets every request received so far, in order.
	/// </summary>
	public IReadOnlyList<RecordedRequest> Requests
	{
		get
		{
			lock (this.stateLock)
			{
				return this.requests.ToList();
			}
		}
	}

	/// <summary>
	/// Starts a fake printer.
	/// </summary>
	/// <param name="options">How to set it up, or <see langword="null"/> for an IPP Everywhere printer like the test
	/// container's <c>ippeveprinter</c> on a free port of <c>127.0.0.1</c>.</param>
	/// <param name="cancellationToken">Cancels starting.</param>
	/// <returns>The running printer. Dispose it to stop it.</returns>
	public static async Task<FakePrinter> StartAsync(FakePrinterOptions? options = null, CancellationToken cancellationToken = default)
	{
		options ??= new FakePrinterOptions();
		FakePrinterProfile profile = options.Profile ?? await FakePrinterProfile.LoadIppeveprinterAsync(cancellationToken);
		X509Certificate2? certificate = options.UseTls ? SelfSignedCertificate.Create() : null;
		FakePrinter printer = new FakePrinter(options, profile, certificate);

		try
		{
			await printer.app.StartAsync(cancellationToken);
			printer.responder.PrinterUri = printer.ReadPrinterUri();

			return printer;
		}
		catch
		{
			await printer.DisposeAsync();

			throw;
		}
	}

	/// <summary>
	/// Gets the requests received for one operation.
	/// </summary>
	/// <param name="operation">The operation.</param>
	/// <returns>The requests, in order.</returns>
	public IReadOnlyList<RecordedRequest> GetRequests(IppOperation operation)
	{
		return this.Requests.Where(request => request.Operation == operation).ToList();
	}

	/// <summary>
	/// Stops the printer. Stopping it again does nothing.
	/// </summary>
	/// <returns>A task that completes when it has stopped.</returns>
	public async ValueTask DisposeAsync()
	{
		if (this.isDisposed)
		{
			return;
		}

		this.isDisposed = true;
		await this.app.StopAsync(CancellationToken.None);
		await this.app.DisposeAsync();
		this.certificate?.Dispose();
	}

	/// <summary>
	/// Works out the printer's URI from the port Kestrel listens on.
	/// </summary>
	/// <returns>The URI.</returns>
	private Uri ReadPrinterUri()
	{
		IServer server = this.app.Services.GetRequiredService<IServer>();
		string address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
		int port = new Uri(address).Port;
		string host = this.options.UriHost ?? (this.options.Address == FakePrinterAddress.IPv6Loopback ? "[::1]" : "127.0.0.1");
		string scheme = this.certificate is null ? "ipp" : "ipps";

		return new Uri($"{scheme}://{host}:{port}{this.options.Path}");
	}

	/// <summary>
	/// Handles one HTTP request: reads the IPP request, checks the credentials, waits, misbehaves or answers, and
	/// records what happened.
	/// </summary>
	/// <param name="context">The HTTP request.</param>
	/// <returns>A task that completes when the request has been handled.</returns>
	private async Task HandleAsync(HttpContext context)
	{
		CancellationToken cancellationToken = context.RequestAborted;

		if (!HttpMethods.IsPost(context.Request.Method))
		{
			context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;

			return;
		}

		// The whole request first, so the client has sent everything before any answer, even a refusal
		using MemoryStream body = new MemoryStream();
		await context.Request.Body.CopyToAsync(body, cancellationToken);
		body.Position = 0;
		IIppRequestMessage? request = await this.TryReadRequestAsync(body, cancellationToken);

		if (request is null)
		{
			context.Response.StatusCode = StatusCodes.Status400BadRequest;

			return;
		}

		byte[] document = await FakePrinter.ReadDocumentAsync(request, cancellationToken);
		bool hasAuthorization = context.Request.Headers.Authorization.Count > 0;

		if (this.Behaviour.ResponseDelay > TimeSpan.Zero)
		{
			await Task.Delay(this.Behaviour.ResponseDelay, cancellationToken);
		}

		if (!this.IsAuthorized(context.Request))
		{
			this.Record(request, document, hasAuthorization, StatusCodes.Status401Unauthorized, statusCode: null);
			context.Response.StatusCode = StatusCodes.Status401Unauthorized;
			context.Response.Headers.WWWAuthenticate = "Basic realm=\"Latiprimere fake printer\"";

			return;
		}

		if (request.IppOperation == IppOperation.PrintJob && this.Behaviour.DropConnectionOnPrintJob)
		{
			this.Record(request, document, hasAuthorization, httpStatus: 0, statusCode: null);
			context.Abort();

			return;
		}

		IppResponseMessage response;

		lock (this.stateLock)
		{
			response = this.responder.Answer(request, document, DateTimeOffset.UtcNow);
		}

		this.Record(request, document, hasAuthorization, StatusCodes.Status200OK, response.StatusCode);

		using MemoryStream output = new MemoryStream();
		await this.protocol.WriteIppResponseAsync(response, output, cancellationToken);
		context.Response.ContentType = "application/ipp";
		context.Response.ContentLength = output.Length;
		await context.Response.Body.WriteAsync(output.GetBuffer().AsMemory(0, (int)output.Length), cancellationToken);
	}

	/// <summary>
	/// Reads an IPP request.
	/// </summary>
	/// <param name="body">The HTTP request's body.</param>
	/// <param name="cancellationToken">Cancels reading.</param>
	/// <returns>The request, or <see langword="null"/> if the body isn't a valid IPP request.</returns>
	private async Task<IIppRequestMessage?> TryReadRequestAsync(Stream body, CancellationToken cancellationToken)
	{
		try
		{
			return await this.protocol.ReadIppRequestAsync(body, cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Whatever SharpIppNext couldn't read is a bad request, as a printer would answer
			return null;
		}
	}

	/// <summary>
	/// Reads the document that follows a request's attributes.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="cancellationToken">Cancels reading.</param>
	/// <returns>The document's bytes; empty if there is none.</returns>
	private static async Task<byte[]> ReadDocumentAsync(IIppRequestMessage request, CancellationToken cancellationToken)
	{
		if (request.Document is null)
		{
			return [];
		}

		using MemoryStream copy = new MemoryStream();
		await request.Document.CopyToAsync(copy, cancellationToken);

		return copy.ToArray();
	}

	/// <summary>
	/// Checks the request's Basic credentials, when the printer requires them.
	/// </summary>
	/// <param name="request">The HTTP request.</param>
	/// <returns><see langword="true"/> if no credentials are required, or the right ones were sent.</returns>
	private bool IsAuthorized(HttpRequest request)
	{
		NetworkCredential? required = this.options.RequiredCredentials;

		if (required is null)
		{
			return true;
		}

		if (!AuthenticationHeaderValue.TryParse(request.Headers.Authorization.ToString(), out AuthenticationHeaderValue? header)
			|| !string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
			|| header.Parameter is null)
		{
			return false;
		}

		string expected = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{required.UserName}:{required.Password}"));

		return string.Equals(header.Parameter, expected, StringComparison.Ordinal);
	}

	/// <summary>
	/// Records a request and how it was answered.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <param name="document">Its document.</param>
	/// <param name="hasAuthorization">Whether it carried credentials.</param>
	/// <param name="httpStatus">The HTTP status answered, or 0 for a dropped connection.</param>
	/// <param name="statusCode">The IPP status answered, or <see langword="null"/> for none.</param>
	private void Record(IIppRequestMessage request, byte[] document, bool hasAuthorization, int httpStatus, IppStatusCode? statusCode)
	{
		RecordedRequest recorded = new RecordedRequest
		{
			Operation = request.IppOperation,
			RequestId = request.RequestId,
			OperationAttributes = IppAttributeLists.DescribeAll(request.OperationAttributes),
			JobAttributes = IppAttributeLists.DescribeAll(request.JobAttributes),
			Document = document,
			HasAuthorization = hasAuthorization,
			HttpStatus = httpStatus,
			StatusCode = statusCode,
			ReceivedAt = DateTimeOffset.UtcNow
		};

		lock (this.stateLock)
		{
			this.requests.Add(recorded);
		}

		this.options.RequestRecorded?.Invoke(recorded);
	}
}
