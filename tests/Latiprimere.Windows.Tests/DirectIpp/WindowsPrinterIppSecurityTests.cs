using System.Net;
using Latiprimere.Ipp;
using Latiprimere.Ipp.Connections;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using Latiprimere.Testing.Fakes;
using Latiprimere.Windows.DirectIpp;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Latiprimere.Windows.Tests.DirectIpp;

/// <summary>
/// Windows queues sent to their IPP printers over IPPS, with pinned certificates and passwords, and the printer's
/// <c>uri-authentication-supported</c> deciding when the driver prints instead (ADR 0042).
/// </summary>
public sealed class WindowsPrinterIppSecurityTests : IDisposable
{
	private const string PrinterName = "HP ENVY 5640 series";                   // A WSD queue
	private const string PortName = "WSD-a316ac12-318f-4e7e-ae4c-f523327f4f2d";  // Its port
	private const string Uuid = "1c852a4d-b800-1f08-abcd-a0d3c1d9e51b";          // The UUID the port records
	private const string UrlQueueName = "Office";                               // A queue added by URL
	private const string UrlPortName = "http://office.local:631/ipp/print";      // Its port

	private static readonly Uri PlainUri = new Uri("ipp://hp-envy.local:631/ipp/print");   // Announced over _ipp._tcp
	private static readonly Uri SecureUri = new Uri("ipps://hp-envy.local:443/ipp/print"); // Announced over _ipps._tcp
	private static readonly Uri OfficeUri = new Uri("ipp://office.local:631/ipp/print");   // The URL queue's printer

	private readonly FakeIppClient client = new FakeIppClient { PrintJobId = 88 };      // The printer, over IPP
	private readonly FakeIppPrinterDiscovery discovery = new FakeIppPrinterDiscovery(); // The network
	private readonly FakeWinSpool spool = new FakeWinSpool();                           // The spooler and registry
	private readonly FakeGdiPrinter gdi = new FakeGdiPrinter { NextJobId = 5 };         // The driver's path
	private readonly FakePdfRasterizer rasterizer = new FakePdfRasterizer();            // For the driver's path
	private readonly RecordingLogger logger = new RecordingLogger();                    // The Windows print system's log
	private readonly List<WindowsIppRoute> routes = new List<WindowsIppRoute>();        // Disposed after each test

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsPrinterIppSecurityTests"/> class, with a WSD queue whose
	/// printer announces both addresses.
	/// </summary>
	public WindowsPrinterIppSecurityTests()
	{
		this.spool.WsdPrinterUuids[WindowsPrinterIppSecurityTests.PortName] = "urn:uuid:" + WindowsPrinterIppSecurityTests.Uuid;

		this.discovery.AddPrinter
		(
			"HP ENVY 5640 series [D9E51B]",
			WindowsPrinterIppSecurityTests.PlainUri,
			WindowsPrinterIppSecurityTests.Uuid,
			WindowsPrinterIppSecurityTests.SecureUri
		);
	}

	[Fact]
	public async Task GivenTrustBeyondTheSystemWhenPdfSubmittedToWsdQueueThenItGoesOverIpps()
	{
		// Arrange — the printer's certificate is pinned, so its IPPS address is preferred
		this.AddWsdPrinter(WindowsPrinterIppSecurityTests.SecureUri);
		WindowsPrinter printer = this.CreateWsdPrinter(prefersIpps: true);
		using MemoryStream document = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:88", job.Id);
		Assert.Equal(WindowsPrinterIppSecurityTests.SecureUri, this.client.LastPrintJobRequest?.OperationAttributes?.PrinterUri?.Value);
		Assert.Null(this.gdi.LastRequest);
	}

	[Fact]
	public async Task GivenIppsCertificateNotTrustedWhenPdfSubmittedToWsdQueueThenItGoesOverPlainIpp()
	{
		// Arrange — trust is configured, but not for this printer's certificate
		this.client.AddUntrusted(WindowsPrinterIppSecurityTests.SecureUri);
		this.AddWsdPrinter(WindowsPrinterIppSecurityTests.PlainUri);
		WindowsPrinter printer = this.CreateWsdPrinter(prefersIpps: true);
		using MemoryStream document = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — plain IPP answered, and the switch is logged
		Assert.Equal("ipp:88", job.Id);
		Assert.Equal(WindowsPrinterIppSecurityTests.PlainUri, this.client.LastPrintJobRequest?.OperationAttributes?.PrinterUri?.Value);
		Assert.Contains(this.logger.Entries, entry => entry.Message.Contains(WindowsPrinterIppSecurityTests.SecureUri.ToString(), StringComparison.Ordinal));
	}

	[Fact]
	public async Task GivenOnlySystemTrustWhenPdfSubmittedToWsdQueueThenIppsIsNotTried()
	{
		// Arrange — nothing beyond the system's trust, so nothing changes for those who configure nothing
		this.AddWsdPrinter(WindowsPrinterIppSecurityTests.PlainUri);
		this.AddWsdPrinter(WindowsPrinterIppSecurityTests.SecureUri);
		WindowsPrinter printer = this.CreateWsdPrinter(prefersIpps: false);
		using MemoryStream document = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.All
		(
			this.client.GetPrinterAttributesRequests,
			request => Assert.Equal(WindowsPrinterIppSecurityTests.PlainUri, request.OperationAttributes?.PrinterUri?.Value)
		);
	}

	[Fact]
	public async Task GivenPrinterAskingForPasswordItWontBeSentWhenPdfSubmittedThenTheDriverPrintsItWithoutAskingTwice()
	{
		// Arrange — it asks for a password on plain IPP, and none is sent there
		this.AddUrlPrinter("basic");
		WindowsPrinter printer = this.CreateUrlPrinter(IppConnectionSecurity.None);
		using MemoryStream first = WindowsPrinterIppSecurityTests.CreatePdf();
		using MemoryStream second = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		IPrintJob firstJob = await printer.SubmitAsync(PrintContent.FromPdf(first), PrintOptions.Default, TestContext.Current.CancellationToken);
		IPrintJob secondJob = await printer.SubmitAsync(PrintContent.FromPdf(second), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the driver printed both, the printer was asked once and sent nothing, and the reason is logged
		Assert.Equal(("5", "5"), (firstJob.Id, secondJob.Id));
		Assert.Single(this.client.GetPrinterAttributesRequests);
		Assert.Empty(this.client.ValidateJobRequests);
		Assert.Null(this.client.LastPrintJobRequest);
		Assert.Contains(this.logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("'basic'", StringComparison.Ordinal));
	}

	[Fact]
	public async Task GivenPrinterAskingForPasswordThatIsSentWhenPdfSubmittedThenItGoesOverIpp()
	{
		// Arrange — as when its ipps:// address is configured with credentials
		this.AddUrlPrinter("basic");
		IppConnectionSecurity security = new IppConnectionSecurity
		{
			Credentials                 = new NetworkCredential("ann", "secret"),
			UseTls                      = true,
			SendsCredentialsOnlyOverTls = true
		};

		WindowsPrinter printer = this.CreateUrlPrinter(security);
		using MemoryStream document = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:88", job.Id);
		Assert.Null(this.gdi.LastRequest);
	}

	[Theory]
	[InlineData("certificate")]
	[InlineData("digest")]
	[InlineData("negotiate")]
	public async Task GivenPrinterAskingForAuthenticationLatiprimereCantGiveWhenPdfSubmittedThenTheDriverPrintsIt(string keyword)
	{
		// Arrange — keyword provided by InlineData above; credentials wouldn't help
		this.AddUrlPrinter(keyword);
		IppConnectionSecurity security = new IppConnectionSecurity { Credentials = new NetworkCredential("ann", "secret") };
		WindowsPrinter printer = this.CreateUrlPrinter(security);
		using MemoryStream document = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("5", job.Id);
		Assert.Null(this.client.LastPrintJobRequest);
	}

	[Theory]
	[InlineData("none")]
	[InlineData("requesting-user-name")]
	public async Task GivenPrinterAskingForNoPasswordWhenPdfSubmittedThenItGoesOverIpp(string keyword)
	{
		// Arrange — keyword provided by InlineData above
		this.AddUrlPrinter(keyword);
		WindowsPrinter printer = this.CreateUrlPrinter(IppConnectionSecurity.None);
		using MemoryStream document = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("ipp:88", job.Id);
	}

	[Fact]
	public async Task GivenUntrustedCertificateWhenPdfSubmittedThenTheDriverPrintsItAndTheFingerprintIsLogged()
	{
		// Arrange — what SharpIppClientAdapter throws for a certificate it refused
		this.client.FailureToThrow = new PrintCertificateException("Not trusted.")
		{
			CertificateFingerprint = "3A:7F:00:11",
			PrinterUri             = WindowsPrinterIppSecurityTests.OfficeUri
		};

		WindowsPrinter printer = this.CreateUrlPrinter(IppConnectionSecurity.None);
		using MemoryStream document = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — a warning, naming the setting to pin it in
		Assert.Equal("5", job.Id);
		(LogLevel Level, string Message, Exception? Exception) warning = Assert.Single(this.logger.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Contains("3A:7F:00:11", warning.Message, StringComparison.Ordinal);
		Assert.Contains("WindowsOptions.IppTrustedCertificateFingerprints", warning.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenEncryptedConnectionFailingAtPrintJobWhenPdfSubmittedThenTheDriverPrintsItFromTheStart()
	{
		// Arrange — matched, then its certificate changes before the job's own connection: nothing was sent
		this.AddUrlPrinter("none");

		this.client.PrintJobFailureToThrow = new PrintException
		(
			"Could not open an encrypted connection.",
			new HttpRequestException(HttpRequestError.SecureConnectionError, "The certificate isn't trusted.")
		);

		WindowsPrinter printer = this.CreateUrlPrinter(IppConnectionSecurity.None);
		using MemoryStream document = WindowsPrinterIppSecurityTests.CreatePdf();

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Assert — the driver printed it, from the start of the PDF
		Assert.Equal("5", job.Id);
		Assert.NotNull(this.gdi.LastRequest);
		Assert.Equal(0, document.Position);
	}

	/// <summary>
	/// Disposes the routes and their fake client.
	/// </summary>
	public void Dispose()
	{
		foreach (WindowsIppRoute route in this.routes)
		{
			route.Dispose();
		}
	}

	/// <summary>
	/// Creates a small stand-in PDF.
	/// </summary>
	/// <returns>The stream, at its start.</returns>
	private static MemoryStream CreatePdf()
	{
		return new MemoryStream([0x25, 0x50, 0x44, 0x46, 0x2D]);
	}

	/// <summary>
	/// Puts the WSD queue's printer at an address, reporting its UUID and taking PDF.
	/// </summary>
	/// <param name="printerUri">The address.</param>
	private void AddWsdPrinter(Uri printerUri)
	{
		this.client.AddPrinter
		(
			printerUri,
			new PrinterAttributesBuilder().WithUuid("urn:uuid:" + WindowsPrinterIppSecurityTests.Uuid).WithDocumentFormats("application/pdf").Build()
		);
	}

	/// <summary>
	/// Puts the URL queue's printer at its address, taking PDF and asking for the given authentication there.
	/// </summary>
	/// <param name="authentication">Its <c>uri-authentication-supported</c> keyword.</param>
	private void AddUrlPrinter(string authentication)
	{
		this.client.AddPrinter
		(
			WindowsPrinterIppSecurityTests.OfficeUri,
			new PrinterAttributesBuilder()
				.WithDocumentFormats("application/pdf")
				.WithUriAuthentication(("ipp://office.local:631/ipp/print", authentication))
				.Build()
		);
	}

	/// <summary>
	/// Creates the route, as the factory does, with the given security.
	/// </summary>
	/// <param name="security">How requests are protected.</param>
	/// <param name="prefersIpps">Whether IPPS addresses are tried first.</param>
	/// <returns>The route, disposed after the test.</returns>
	private WindowsIppRoute CreateRoute(IppConnectionSecurity security, bool prefersIpps)
	{
		IppPrintContext context = new IppPrintContextBuilder()
			.WithClient(this.client)
			.WithDocumentFormats(IppDocumentFormats.Standard)
			.Build();

		WindowsIppRoute route = new WindowsIppRoute(context, [], this.discovery)
		{
			Security    = security,
			PrefersIpps = prefersIpps
		};

		this.routes.Add(route);

		return route;
	}

	/// <summary>
	/// Creates the WSD queue.
	/// </summary>
	/// <param name="prefersIpps">Whether IPPS addresses are tried first.</param>
	/// <returns>The printer.</returns>
	private WindowsPrinter CreateWsdPrinter(bool prefersIpps)
	{
		return this.CreatePrinter(WindowsPrinterIppSecurityTests.PrinterName, WindowsPrinterIppSecurityTests.PortName, this.CreateRoute(IppConnectionSecurity.None, prefersIpps));
	}

	/// <summary>
	/// Creates the queue added by URL.
	/// </summary>
	/// <param name="security">How requests are protected.</param>
	/// <returns>The printer.</returns>
	private WindowsPrinter CreateUrlPrinter(IppConnectionSecurity security)
	{
		return this.CreatePrinter(WindowsPrinterIppSecurityTests.UrlQueueName, WindowsPrinterIppSecurityTests.UrlPortName, this.CreateRoute(security, prefersIpps: false));
	}

	/// <summary>
	/// Creates a queue that sends its PDF jobs over the route.
	/// </summary>
	/// <param name="printerName">The queue's name.</param>
	/// <param name="portName">Its port.</param>
	/// <param name="route">The route.</param>
	/// <returns>The printer.</returns>
	private WindowsPrinter CreatePrinter(string printerName, string portName, WindowsIppRoute route)
	{
		WindowsPrintContext context = new WindowsPrintContextBuilder()
			.WithSpool(this.spool)
			.WithGdi(this.gdi)
			.WithPdfRasterizer(this.rasterizer)
			.WithIppRoute(route)
			.WithLogger(this.logger)
			.Build();

		PrinterInfo info = new PrinterInfo { Id = "windows:" + printerName, Name = printerName };

		return new WindowsPrinter(context, printerName, null, portName, info);
	}
}
