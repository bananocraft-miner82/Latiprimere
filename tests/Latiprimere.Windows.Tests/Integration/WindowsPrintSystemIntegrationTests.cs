using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Windows.Tests.Integration;

/// <summary>
/// End-to-end checks of the Windows print system through the real spooler, using the catalog as applications do.
/// Nothing reaches paper.
/// </summary>
/// <remarks>
/// The spooler tests need the printers from <c>tools/windows-test-printers/Install-TestPrinters.ps1</c>, and run only
/// when <see cref="IntegrationEnvironment.WindowsOutputVariable"/> gives their output directory. The PDF test runs
/// wherever Microsoft Print to PDF is installed.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("OS", "Windows")]
[SupportedOSPlatform("windows")]
public sealed class WindowsPrintSystemIntegrationTests
{
	private const string RawName      = "Latiprimere Raw";      // Writes each job's bytes to Latiprimere-Raw.prn
	private const string RetainedName = "Latiprimere Retained"; // Same, but keeps finished jobs in the queue
	private const string PausedName   = "Latiprimere Paused";   // Paused, so jobs wait until cancelled
	private const string IdPrefix     = "windows:";             // Prefix of every Windows printer ID

	[Fact]
	public async Task GivenTestPrintersWhenGetPrintersThenListsThemWithPausedOneStopped()
	{
		// Arrange
		WindowsPrintSystemIntegrationTests.SkipUnlessTestPrintersInstalled();
		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();

		// Act
		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		IReadOnlyList<string> printerIds = printers.Select(printer => printer.Id).ToList();
		Assert.Contains(WindowsPrintSystemIntegrationTests.ToId(WindowsPrintSystemIntegrationTests.RawName), printerIds);
		Assert.Contains(WindowsPrintSystemIntegrationTests.ToId(WindowsPrintSystemIntegrationTests.RetainedName), printerIds);

		PrinterInfo paused = Assert.Single
		(
			printers,
			printer => printer.Id == WindowsPrintSystemIntegrationTests.ToId(WindowsPrintSystemIntegrationTests.PausedName)
		);

		Assert.Equal(PrinterState.Stopped, paused.State);
	}

	[Fact]
	public async Task GivenRawDataWhenPrintedToFilePortPrinterThenFileReceivesTheSameBytes()
	{
		// Arrange
		string outputDirectory = WindowsPrintSystemIntegrationTests.SkipUnlessTestPrintersInstalled();
		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, WindowsPrintSystemIntegrationTests.RawName);
		byte[] expected = WindowsPrintSystemIntegrationTests.CreateRawData();
		using MemoryStream data = new MemoryStream(expected);

		// The port writes every job to the same file, so only one test process may print there at a time
		using PrinterLock printerLock = new PrinterLock(WindowsPrintSystemIntegrationTests.RawName);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		string outputFile = Path.Combine(outputDirectory, "Latiprimere-Raw.prn");
		byte[]? received = await OutputFileWaiter.WaitForContentAsync(outputFile, expected, TestContext.Current.CancellationToken);

		// Assert — the job leaves the queue when done, so Windows can't say how it ended; Latiprimere reports Completed
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.Equal(expected, received);
	}

	[Fact]
	public async Task GivenPrinterKeepingPrintedJobsWhenRawDataPrintedThenJobStaysAndEndsCompleted()
	{
		// Arrange
		WindowsPrintSystemIntegrationTests.SkipUnlessTestPrintersInstalled();
		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, WindowsPrintSystemIntegrationTests.RetainedName);
		using MemoryStream data = new MemoryStream(WindowsPrintSystemIntegrationTests.CreateRawData());

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		int jobNumber = int.Parse(job.Id, CultureInfo.InvariantCulture);
		WindowsJobRecord? retainedJob = new WinSpoolApi().GetJob(WindowsPrintSystemIntegrationTests.RetainedName, jobNumber);

		// Assert — still in the queue, so the final state comes from the spooler rather than from the job vanishing
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.NotNull(retainedJob);
	}

	[Fact]
	public async Task GivenJobWaitingOnPausedPrinterWhenCancelledThenJobEndsCancelled()
	{
		// Arrange
		WindowsPrintSystemIntegrationTests.SkipUnlessTestPrintersInstalled();
		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, WindowsPrintSystemIntegrationTests.PausedName);
		using MemoryStream data = new MemoryStream(WindowsPrintSystemIntegrationTests.CreateRawData());
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		PrintJobStatus waitingStatus = await job.GetStatusAsync(TestContext.Current.CancellationToken);

		// Act
		bool isCancelled = await job.CancelAsync(TestContext.Current.CancellationToken);
		PrintJobStatus finalStatus = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert
		Assert.True(isCancelled);
		Assert.False(waitingStatus.IsTerminal);
		Assert.Equal(PrintJobState.Cancelled, finalStatus.State);
	}

	[Fact]
	public async Task GivenOutputFileInMissingFolderWhenRawDataPrintedThenSpoolerErrorIsExplained()
	{
		// Arrange — the XPS Document Writer takes raw data and an output file, and needs no test printers
		const string xpsWriter = "Microsoft XPS Document Writer";
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");
		Assert.SkipWhen(new WinSpoolApi().GetPrinter(xpsWriter) is null, "Microsoft XPS Document Writer is not installed.");
		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, xpsWriter);
		string outputFile = Path.Combine(Path.GetTempPath(), $"latiprimere-missing-{Guid.NewGuid():N}", "out.bin");
		using MemoryStream data = new MemoryStream(WindowsPrintSystemIntegrationTests.CreateRawData());
		PrintOptions options = new PrintOptions { OutputFile = outputFile };

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(data), options, TestContext.Current.CancellationToken);

		// Assert — ERROR_PATH_NOT_FOUND from StartDocPrinter, with Latiprimere's explanation and Windows' own text
		PrintException exception = await Assert.ThrowsAsync<PrintException>(submit);
		Assert.Equal(3, exception.NativeErrorCode);
		Assert.StartsWith("Windows can't find a file or folder.", exception.Message, StringComparison.Ordinal);
		Assert.Contains("StartDocPrinter", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenRawDataWhenPrintedToMicrosoftPrintToPdfThenRefusedBeforeQueueing()
	{
		// Arrange — its driver would accept the job and write an empty file
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, PrinterLock.MicrosoftPrintToPdf);
		string outputFile = Path.Combine(Path.GetTempPath(), $"latiprimere-test-{Guid.NewGuid():N}.pdf");
		using MemoryStream data = new MemoryStream(WindowsPrintSystemIntegrationTests.CreateRawData());
		PrintOptions options = new PrintOptions { OutputFile = outputFile };

		// Act
		Func<Task> submit = () => printer.SubmitAsync(PrintContent.FromRaw(data), options, TestContext.Current.CancellationToken);

		// Assert — matched on the real driver's name, and nothing was written
		NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(submit);
		Assert.Contains("Microsoft Print To PDF", exception.Message, StringComparison.Ordinal);
		Assert.False(File.Exists(outputFile));
	}

	[Fact]
	public async Task GivenPdfWhenPrintedToMicrosoftPrintToPdfThenWritesOutputFile()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, PrinterLock.MicrosoftPrintToPdf);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		string outputFile = Path.Combine(Path.GetTempPath(), $"latiprimere-test-{Guid.NewGuid():N}.pdf");
		PrintOptions options = new PrintOptions { JobName = "Latiprimere integration test", OutputFile = outputFile };
		using PrinterLock printerLock = new PrinterLock(PrinterLock.MicrosoftPrintToPdf);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		byte[]? written = await OutputFileWaiter.WaitForAnyContentAsync(outputFile, TestContext.Current.CancellationToken);

		// Assert — the driver writes a PDF of its own
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.NotNull(written);
		Assert.Equal("%PDF-"u8.ToArray(), written.AsSpan(0, 5).ToArray());
		File.Delete(outputFile);
	}

	/// <summary>
	/// Skips the test unless it runs on Windows with the test printers installed.
	/// </summary>
	/// <returns>The test printers' output directory.</returns>
	private static string SkipUnlessTestPrintersInstalled()
	{
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		string? outputDirectory = IntegrationEnvironment.WindowsOutputDirectory;
		Assert.SkipWhen(outputDirectory is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.WindowsOutputVariable));

		return outputDirectory!; // Not null: skipped above otherwise
	}

	/// <summary>
	/// Creates a catalog over the Windows print system, with PDFium for PDF jobs.
	/// </summary>
	/// <returns>The catalog fixture. The caller disposes it.</returns>
	private static CatalogFixture CreateCatalog()
	{
		return new CatalogFixture(services => services.AddLatiprimerePdfium().AddLatiprimereWindows());
	}

	/// <summary>
	/// Looks up a Windows printer by name through the catalog.
	/// </summary>
	/// <param name="fixture">The catalog fixture.</param>
	/// <param name="printerName">The printer's name in Windows.</param>
	/// <returns>The printer.</returns>
	private static Task<IPrinter> GetPrinterAsync(CatalogFixture fixture, string printerName)
	{
		return fixture.Catalog.GetPrinterAsync(WindowsPrintSystemIntegrationTests.ToId(printerName), TestContext.Current.CancellationToken);
	}

	/// <summary>
	/// Gets the ID the Windows print system gives a printer.
	/// </summary>
	/// <param name="printerName">The printer's name in Windows.</param>
	/// <returns>The printer ID.</returns>
	private static string ToId(string printerName)
	{
		return WindowsPrintSystemIntegrationTests.IdPrefix + printerName;
	}

	/// <summary>
	/// Creates raw job data that is different every time, with line endings that must arrive unchanged.
	/// </summary>
	/// <returns>The data.</returns>
	private static byte[] CreateRawData()
	{
		return Encoding.ASCII.GetBytes($"Latiprimere raw integration test {Guid.NewGuid():N}\r\nSecond line\r\n");
	}
}
