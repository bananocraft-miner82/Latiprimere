using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Ipp.Tests.Integration;

/// <summary>
/// End-to-end checks of direct IPP against the virtual IPP Everywhere printer (<c>ippeveprinter</c>) in the test
/// container (tools/virtual-printers) or on a Mac (tools/macos-test-printers), through the catalog as applications
/// use it. Nothing reaches paper.
/// </summary>
/// <remarks>
/// Run only when <see cref="IntegrationEnvironment.IppPrinterVariable"/> gives the printer's URI. Discovery is off:
/// mDNS doesn't cross into the container, and on a Mac the printer doesn't announce itself, so it is configured by
/// URI.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("OS", "Any")]
public sealed class IppNetworkPrintSystemIntegrationTests
{
	private const int AttributesOrValuesNotSupported = 0x040B; // IPP status for a job the printer can't print as asked

	[Fact]
	public async Task GivenConfiguredPrinterWhenGetPrintersThenListsItByUri()
	{
		// Arrange
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);

		// Act
		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrinterInfo printer = Assert.Single(printers);
		Assert.Equal(IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri), printer.Id);
	}

	[Fact]
	public async Task GivenConfiguredPrinterWhenGetPrinterThenReadsItsMakeAndModel()
	{
		// Arrange
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);

		// Act
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert — the container starts ippeveprinter with make "Latiprimere" and model "Virtual IPP Printer"
		// Its state isn't checked: it is Processing whenever another test process is printing to it
		Assert.Equal("Latiprimere Virtual IPP Printer", printer.Info.MakeAndModel);
	}

	[Fact]
	public async Task GivenUnknownPathOnPrinterHostWhenGetPrinterThenThrowsPrinterNotFoundException()
	{
		// Arrange
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		Uri unknownUri = new Uri(printerUri, "/ipp/no-such-printer");

		// Act
		Func<Task> act = () => fixture.Catalog.GetPrinterAsync
		(
			IppNetworkPrintSystemIntegrationTests.GetPrinterId(unknownUri),
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<PrinterNotFoundException>(act);
	}

	[Fact]
	public async Task GivenVirtualPrinterWhenGetCapabilitiesThenReportsOneSidedLetterAndCopyLimit()
	{
		// Arrange
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal([DuplexMode.OneSided], capabilities.DuplexModes);
		Assert.Equal("na_letter_8.5x11in", capabilities.DefaultMediaSize?.Name);
		Assert.Equal(999, capabilities.MaxCopies);
	}

	[Fact]
	public async Task GivenIppsAddressWhenItsCertificateIsRefusedThenPinnedThenTheJobCompletesOverIpps()
	{
		// Arrange — ippeveprinter takes TLS on its own port, with a certificate it makes itself (ADR 0037)
		Uri plainUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		Uri secureUri = new UriBuilder(plainUri) { Scheme = "ipps" }.Uri;
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(secureUri);
		string? fingerprint;

		await using (CatalogFixture untrusted = IppNetworkPrintSystemIntegrationTests.CreateCatalog(secureUri))
		{
			PrintException refused = await Assert.ThrowsAnyAsync<PrintException>
			(
				() => untrusted.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken)
			);

			fingerprint = (refused as PrintCertificateException)?.CertificateFingerprint;
			Assert.SkipWhen(fingerprint is null, $"This ippeveprinter didn't offer TLS: {refused.Message}");
		}

		await using CatalogFixture pinned = new CatalogFixture
		(
			services => services.AddLatiprimereIpp
			(
				options =>
				{
					options.EnableDiscovery = false;
					options.PrinterUris.Add(secureUri);
					options.TrustedCertificateFingerprints.Add(fingerprint);
				}
			)
		);

		IPrinter printer = await pinned.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), new PrintOptions { JobName = "Latiprimere over IPPS" }, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenPdfWhenPrintedThenJobCompletes()
	{
		// Arrange
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { JobName = "Latiprimere integration test" };

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenPrintedJobWhenLookedUpByIdThenFoundUntilThePrinterForgetsIt()
	{
		// Arrange
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		IPrintJob submitted = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken);

		// Act
		IPrintJob? found = await printer.GetJobAsync(submitted.Id, TestContext.Current.CancellationToken);
		IPrintJob? unknown = await printer.GetJobAsync("2147483647", TestContext.Current.CancellationToken);

		// Assert — ippeveprinter keeps finished jobs for a while, as most printers do
		Assert.NotNull(found);
		Assert.Equal(submitted.Id, found.Id);
		Assert.Equal(PrintJobState.Completed, (await PrintJobWaiter.WaitUntilFinishedAsync(found, TestContext.Current.CancellationToken)).State);
		Assert.Null(unknown);
	}

	[Fact]
	public async Task GivenDuplexOnOneSidedPrinterWhenPrintedThenPrinterRejectsJobUpFront()
	{
		// Arrange — unlike CUPS, which accepts the job and cancels it later
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { Duplex = DuplexMode.LongEdge };

		// Act
		Func<Task> act = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(act);
		Assert.Equal(IppNetworkPrintSystemIntegrationTests.AttributesOrValuesNotSupported, exception.NativeErrorCode);
	}

	[Fact]
	public async Task GivenTraySizeAndColourFromCapabilitiesWhenPrintedThenPrinterAcceptsThemAndJobCompletes()
	{
		// Arrange — ippeveprinter checks media-col against what it supports, so a size it can't read is refused
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);
		Assert.SkipWhen(capabilities.InputTrays.Count == 0, "The printer reports no trays.");

		MediaSize size = capabilities.MediaSizes.FirstOrDefault(size => size.Name != capabilities.DefaultMediaSize?.Name)
						 ?? capabilities.MediaSizes[0];

		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		PrintOptions options = new PrintOptions
		{
			MediaSize = size,
			InputTray = capabilities.InputTrays[^1],
			ColorMode = ColorMode.Monochrome
		};

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task GivenSizeThePrinterDoesNotSupportWhenPrintedThenPrinterRejectsJobUpFront(bool withTray)
	{
		// Arrange — shows the size reaches the printer rather than being dropped: alone it goes as media, with a
		// tray as media-size dimensions inside media-col (withTray provided by InlineData above)
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);
		Assert.SkipWhen(withTray && capabilities.InputTrays.Count == 0, "The printer reports no trays.");
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		PrintOptions options = new PrintOptions
		{
			MediaSize = new MediaSize
			{
				Name              = "custom_latiprimere_123x45mm",
				DisplayName       = "123 × 45 mm",
				WidthMillimeters  = 123,
				HeightMillimeters = 45
			},
			InputTray = withTray ? capabilities.InputTrays[^1] : null
		};

		// Act
		Func<Task> act = () => printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(act);
		Assert.Equal(IppNetworkPrintSystemIntegrationTests.AttributesOrValuesNotSupported, exception.NativeErrorCode);
	}

	[Theory]
	[InlineData("5", true)]
	[InlineData("99", false)]
	public async Task GivenVendorPrintQualityWhenPrintedThenPrinterAppliesOrRefusesIt(string quality, bool isSupported)
	{
		// Arrange — ippeveprinter supports print-quality 3 to 5 and refuses anything else up front, which shows the
		// attribute reaches it (quality and isSupported provided by InlineData above)
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		PrintOptions options = new PrintOptions
		{
			VendorOptions = new Dictionary<string, string>
			{
				["ipp:print-quality"]          = quality,
				["ipp:ipp-attribute-fidelity"] = "true",
				["cups:fit-to-page"]           = "true" // Ignored: only CUPS reads cups: keys
			}
		};

		// Act
		Func<Task<PrintJobStatus>> print = async () =>
		{
			IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);

			return await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		};

		// Assert
		if (isSupported)
		{
			Assert.Equal(PrintJobState.Completed, (await print()).State);
		}
		else
		{
			PrintException exception = await Assert.ThrowsAsync<PrintException>(print);
			Assert.Equal(IppNetworkPrintSystemIntegrationTests.AttributesOrValuesNotSupported, exception.NativeErrorCode);
		}
	}

	[Fact]
	public async Task GivenSeveralPdfsSentAtOnceWhenPrintedThenEveryJobCompletes()
	{
		// Arrange — ippeveprinter takes one job at a time and answers the others server-error-busy, which
		// Latiprimere answers by sending them again; the other test process may be printing too
		Uri printerUri = IppNetworkPrintSystemIntegrationTests.GetPrinterUri();
		await using CatalogFixture fixture = IppNetworkPrintSystemIntegrationTests.CreateCatalog(printerUri);
		string printerId = IppNetworkPrintSystemIntegrationTests.GetPrinterId(printerUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Act
		IPrintJob[] jobs = await Task.WhenAll
		(
			IppNetworkPrintSystemIntegrationTests.SubmitThreePagesAsync(printer),
			IppNetworkPrintSystemIntegrationTests.SubmitThreePagesAsync(printer),
			IppNetworkPrintSystemIntegrationTests.SubmitThreePagesAsync(printer)
		);

		PrintJobStatus[] statuses = await Task.WhenAll
		(
			jobs.Select(job => PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken))
		);

		// Assert
		Assert.All(statuses, status => Assert.Equal(PrintJobState.Completed, status.State));
	}

	/// <summary>
	/// Prints the three-page test document with default options.
	/// </summary>
	/// <param name="printer">The printer.</param>
	/// <returns>The submitted job.</returns>
	private static async Task<IPrintJob> SubmitThreePagesAsync(IPrinter printer)
	{
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		return await printer
			.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, TestContext.Current.CancellationToken)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Gets the virtual printer's URI, or skips the test when none is configured.
	/// </summary>
	/// <returns>The printer URI.</returns>
	private static Uri GetPrinterUri()
	{
		Uri? printerUri = IntegrationEnvironment.IppPrinterUri;
		Assert.SkipWhen(printerUri is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.IppPrinterVariable));

		return printerUri!; // Not null: skipped above otherwise
	}

	/// <summary>
	/// Gets the printer ID the direct IPP print system gives a configured printer.
	/// </summary>
	/// <param name="printerUri">The printer URI.</param>
	/// <returns>The printer ID.</returns>
	private static string GetPrinterId(Uri printerUri)
	{
		return "ipp:" + printerUri.AbsoluteUri;
	}

	/// <summary>
	/// Creates a catalog over direct IPP with only the given printer configured and discovery off.
	/// </summary>
	/// <param name="printerUri">The printer URI.</param>
	/// <returns>The catalog fixture. The caller disposes it.</returns>
	private static CatalogFixture CreateCatalog(Uri printerUri)
	{
		return new CatalogFixture
		(
			services => services.AddLatiprimereIpp
			(
				options =>
				{
					options.EnableDiscovery = false;
					options.PrinterUris.Add(printerUri);
				}
			)
		);
	}
}
