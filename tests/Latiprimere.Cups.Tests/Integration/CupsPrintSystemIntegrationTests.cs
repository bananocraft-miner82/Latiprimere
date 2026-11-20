using System.Text;
using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Cups.Tests.Integration;

/// <summary>
/// End-to-end checks of the CUPS print system against the test queues, through the catalog as applications use it:
/// those of the test container (tools/virtual-printers), or of a Mac's own CUPS (tools/macos-test-printers). Nothing
/// reaches paper.
/// </summary>
/// <remarks>
/// Run only when <see cref="IntegrationEnvironment.CupsServerVariable"/> names the server; the raw-bytes check also
/// needs <see cref="IntegrationEnvironment.CupsOutputVariable"/>. On a Mac, the tests of CUPS-PDF, which only the
/// container has, are skipped (<see cref="IntegrationEnvironment.CupsSetupVariable"/>).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("OS", "Any")]
public sealed class CupsPrintSystemIntegrationTests
{
	private const string CupsPdfId    = "cups:CUPS-PDF";    // Renders jobs to PDF; the container's default queue
	private const string RawId        = "cups:Raw";         // Saves job data unchanged to /output/Raw-<job-id>.prn
	private const string PausedId     = "cups:Paused";      // Disabled, so jobs wait until cancelled
	private const string VirtualIppId = "cups:Virtual-IPP"; // IPP Everywhere queue in front of ippeveprinter

	[Fact]
	public async Task GivenTestQueuesWhenGetPrintersThenListsThemWithPausedOneStopped()
	{
		// Arrange
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();

		// Act
		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Contains(printers, printer => printer.Id == CupsPrintSystemIntegrationTests.RawId);
		Assert.Contains(printers, printer => printer.Id == CupsPrintSystemIntegrationTests.VirtualIppId);
		Assert.Equal(PrinterState.Stopped, Assert.Single(printers, printer => printer.Id == CupsPrintSystemIntegrationTests.PausedId).State);
	}

	[Fact]
	public async Task GivenTestContainerWhenGetPrintersThenMarksCupsPdfAsDefault()
	{
		// Arrange
		CupsPrintSystemIntegrationTests.SkipUnlessContainer(CupsPrintSystemIntegrationTests.CupsPdfId);
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();

		// Act
		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.True(Assert.Single(printers, printer => printer.Id == CupsPrintSystemIntegrationTests.CupsPdfId).IsDefault);
	}

	[Fact]
	public async Task GivenTestContainerWhenGetDefaultPrinterThenReturnsCupsPdf()
	{
		// Arrange
		CupsPrintSystemIntegrationTests.SkipUnlessContainer(CupsPrintSystemIntegrationTests.CupsPdfId);
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();

		// Act
		IPrinter? printer = await fixture.Catalog.GetDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(CupsPrintSystemIntegrationTests.CupsPdfId, printer?.Info.Id);
	}

	[Fact]
	public async Task GivenUnknownQueueWhenGetPrinterThenThrowsPrinterNotFoundException()
	{
		// Arrange
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();

		// Act
		Func<Task> act = () => fixture.Catalog.GetPrinterAsync("cups:No-Such-Queue", TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrinterNotFoundException>(act);
	}

	[Fact]
	public async Task GivenCupsPdfQueueWhenGetCapabilitiesThenReportsA4AsDefaultSize()
	{
		// Arrange
		CupsPrintSystemIntegrationTests.SkipUnlessContainer(CupsPrintSystemIntegrationTests.CupsPdfId);
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.CupsPdfId);

		// Act
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("iso_a4_210x297mm", capabilities.DefaultMediaSize?.Name);
		Assert.Contains(capabilities.MediaSizes, size => size.Name == "na_letter_8.5x11in");
	}

	[Fact]
	public async Task GivenRawDataWhenPrintedToRawQueueThenJobCompletes()
	{
		// Arrange
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.RawId);
		using MemoryStream data = new MemoryStream(CupsPrintSystemIntegrationTests.CreateRawData());

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenRawDataWhenPrintedToRawQueueThenQueueReceivesTheSameBytes()
	{
		// Arrange
		string? outputDirectory = IntegrationEnvironment.CupsOutputDirectory;
		Assert.SkipWhen(outputDirectory is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.CupsOutputVariable));
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.RawId);
		byte[] expected = CupsPrintSystemIntegrationTests.CreateRawData();
		using MemoryStream data = new MemoryStream(expected);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		byte[]? received = await CupsPrintSystemIntegrationTests.WaitForRawOutputAsync(outputDirectory!, job, expected); // Skipped above if null

		// Assert — byte for byte, line endings included
		Assert.Equal(expected, received);
	}

	[Fact]
	public async Task GivenPdfWhenPrintedToCupsPdfWithTwoCopiesThenJobCompletes()
	{
		// Arrange
		CupsPrintSystemIntegrationTests.SkipUnlessContainer(CupsPrintSystemIntegrationTests.CupsPdfId);
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.CupsPdfId);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { JobName = "Latiprimere integration test", Copies = 2 };

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenTwoSidedJobToOneSidedQueueWhenCupsCancelsItThenTheStatusSaysWhy()
	{
		// Arrange — CUPS accepts two-sided printing for the one-sided Virtual-IPP queue, then cancels the job when
		// ippeveprinter refuses it (Spike 1); it gives job-state-reasons but no job-state-message
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.VirtualIppId);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { Duplex = DuplexMode.LongEdge };

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert — the reasons, and a message from them or from CUPS
		Assert.True(status.State is PrintJobState.Canceled or PrintJobState.Aborted, $"The job ended {status.State}.");
		Assert.NotEmpty(status.StateReasons);
		Assert.False(string.IsNullOrWhiteSpace(status.StateMessage), "The job ended with no message; reasons: " + string.Join(", ", status.StateReasons));
	}

	[Fact]
	public async Task GivenPdfWhenPrintedLandscapeToIppEverywhereQueueThenJobCompletes()
	{
		// Arrange
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.VirtualIppId);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { Orientation = PageOrientation.Landscape };

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenTraySizeAndColourFromCapabilitiesWhenPrintedToIppEverywhereQueueThenJobCompletes()
	{
		// Arrange — CUPS passes them on to ippeveprinter behind the queue
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.VirtualIppId);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);
		Assert.SkipWhen(capabilities.InputTrays.Count == 0 || capabilities.MediaSizes.Count == 0, "The queue reports no trays or sizes.");
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		PrintOptions options = new PrintOptions
		{
			MediaSize = capabilities.MediaSizes[^1],
			InputTray = capabilities.InputTrays[^1],
			ColorMode = ColorMode.Monochrome
		};

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenIppAndCupsVendorOptionsWhenPrintedToIppEverywhereQueueThenJobCompletes()
	{
		// Arrange
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.VirtualIppId);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);

		PrintOptions options = new PrintOptions
		{
			VendorOptions = new Dictionary<string, string>
			{
				["ipp:print-quality"] = "5",
				["cups:fit-to-page"]  = "true",
				["windows:devmode"]   = "AAAA" // Ignored: only Windows reads windows: keys
			}
		};

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(PrintJobState.Completed, status.State);
	}

	[Fact]
	public async Task GivenJobWaitingOnPausedQueueWhenCancelledThenJobEndsCancelled()
	{
		// Arrange
		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.PausedId);
		using MemoryStream data = new MemoryStream(CupsPrintSystemIntegrationTests.CreateRawData());
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		PrintJobStatus waitingStatus = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Act
		bool isCancelled = await job.CancelAsync(TestContext.Current.CancellationToken);
		PrintJobStatus finalStatus = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.True(isCancelled);
		Assert.Equal(PrintJobState.Pending, waitingStatus.State);
		Assert.Equal(PrintJobState.Canceled, finalStatus.State);
	}

	[Fact]
	public async Task GivenJobWaitingOnPausedQueueWhenLookedUpAfterARestartThenFoundOnItsQueueOnly()
	{
		// Arrange — the job is submitted through one catalog and looked up through another, as after a restart
		IPrintJob submitted;

		await using (CatalogFixture before = CupsPrintSystemIntegrationTests.CreateCatalog())
		{
			IPrinter paused = await CupsPrintSystemIntegrationTests.GetPrinterAsync(before, CupsPrintSystemIntegrationTests.PausedId);
			using MemoryStream data = new MemoryStream(CupsPrintSystemIntegrationTests.CreateRawData());
			submitted = await paused.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		}

		await using CatalogFixture fixture = CupsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.PausedId);
		IPrinter otherQueue = await CupsPrintSystemIntegrationTests.GetPrinterAsync(fixture, CupsPrintSystemIntegrationTests.RawId);

		// Act
		IPrintJob? found = await printer.GetJobAsync(submitted.Id, TestContext.Current.CancellationToken);
		IPrintJob? onOtherQueue = await otherQueue.GetJobAsync(submitted.Id, TestContext.Current.CancellationToken);

		// Assert — then cancelled through the job found, so the paused queue doesn't fill up
		Assert.NotNull(found);
		Assert.Null(onOtherQueue);
		Assert.Equal(submitted.Id, found.Id);
		Assert.Equal(PrintJobState.Pending, (await found.GetStatusAsync(TestContext.Current.CancellationToken)).State);
		Assert.True(await found.CancelAsync(TestContext.Current.CancellationToken));
	}

	/// <summary>
	/// Creates a catalog over the container's CUPS server, or skips the test when none is configured.
	/// </summary>
	/// <returns>The catalog fixture. The caller disposes it.</returns>
	private static CatalogFixture CreateCatalog()
	{
		string? server = IntegrationEnvironment.CupsServer;
		Assert.SkipWhen(server is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.CupsServerVariable));

		return new CatalogFixture(services => services.AddLatiprimereCups(options => options.Server = server));
	}

	/// <summary>
	/// Skips the test when the server is a Mac's own CUPS, which lacks the queue.
	/// </summary>
	/// <param name="printerId">The queue's printer ID.</param>
	private static void SkipUnlessContainer(string printerId)
	{
		Assert.SkipWhen
		(
			IntegrationEnvironment.CupsSetup != CupsTestSetup.Container,
			IntegrationEnvironment.GetContainerOnlySkipReason(printerId)
		);
	}

	/// <summary>
	/// Looks up a queue through the catalog.
	/// </summary>
	/// <param name="fixture">The catalog fixture.</param>
	/// <param name="printerId">The queue's printer ID.</param>
	/// <returns>The printer.</returns>
	private static Task<IPrinter> GetPrinterAsync(CatalogFixture fixture, string printerId)
	{
		return fixture.Catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);
	}

	/// <summary>
	/// Waits for what the Raw queue received for a job.
	/// </summary>
	/// <param name="outputDirectory">The queue's output folder.</param>
	/// <param name="job">The job.</param>
	/// <param name="expected">The bytes the job sent, unique to the test.</param>
	/// <returns>The bytes received, or <see langword="null"/> if nothing arrived in time.</returns>
	/// <remarks>
	/// In the test container, the capture backend names the file after the CUPS job ID, which this checks too. On a
	/// Mac, the capture server names its files itself, so the file is found by its content.
	/// </remarks>
	private static Task<byte[]?> WaitForRawOutputAsync(string outputDirectory, IPrintJob job, byte[] expected)
	{
		if (IntegrationEnvironment.CupsSetup == CupsTestSetup.Mac)
		{
			return OutputFileWaiter.WaitForFileWithContentAsync(outputDirectory, expected, TestContext.Current.CancellationToken);
		}

		string outputFile = Path.Combine(outputDirectory, $"Raw-{job.Id}.prn");

		return OutputFileWaiter.WaitForContentAsync(outputFile, expected, TestContext.Current.CancellationToken);
	}

	/// <summary>
	/// Creates raw job data that is different every time, with Windows line endings that must arrive unchanged.
	/// </summary>
	/// <returns>The data.</returns>
	private static byte[] CreateRawData()
	{
		return Encoding.ASCII.GetBytes($"Latiprimere raw integration test {Guid.NewGuid():N}\r\nSecond line\r\n");
	}
}
