using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Latiprimere.Testing.Fixtures;
using Latiprimere.Windows.Interop;
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
		Assert.Equal(PrintJobState.Canceled, finalStatus.State);
	}

	[Fact]
	public async Task GivenJobWaitingOnPausedPrinterWhenLookedUpAfterARestartThenFound()
	{
		// Arrange — the job is submitted through one catalog and looked up through another, as after a restart
		WindowsPrintSystemIntegrationTests.SkipUnlessTestPrintersInstalled();
		IPrintJob submitted;

		await using (CatalogFixture before = WindowsPrintSystemIntegrationTests.CreateCatalog())
		{
			IPrinter paused = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(before, WindowsPrintSystemIntegrationTests.PausedName);
			using MemoryStream data = new MemoryStream(WindowsPrintSystemIntegrationTests.CreateRawData());
			submitted = await paused.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, TestContext.Current.CancellationToken);
		}

		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, WindowsPrintSystemIntegrationTests.PausedName);

		// Act
		IPrintJob? found = await printer.GetJobAsync(submitted.Id, TestContext.Current.CancellationToken);

		// Assert — then cancelled through the job found, so the paused printer doesn't fill up
		Assert.NotNull(found);
		Assert.Equal(submitted.Id, found.Id);
		Assert.False((await found.GetStatusAsync(TestContext.Current.CancellationToken)).IsTerminal);
		Assert.True(await found.CancelAsync(TestContext.Current.CancellationToken));
		Assert.Null(await printer.GetJobAsync("2147483647", TestContext.Current.CancellationToken));
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

	[Fact]
	public async Task GivenQueueMappedToIppPrinterWhenPdfPrintedThenGoesStraightToThePrinterAndReportsItsOutcome()
	{
		// Arrange — Microsoft Print to PDF's queue stands in for one in front of an IPP printer: mapped to the test
		// container's ippeveprinter, a PDF job must reach ippeveprinter, not the driver, which would need a file name
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");
		Uri? ippPrinter = IntegrationEnvironment.IppPrinterUri;
		Assert.SkipWhen(ippPrinter is null, IntegrationEnvironment.GetSkipReason(IntegrationEnvironment.IppPrinterVariable));

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		await using CatalogFixture fixture = new CatalogFixture
		(
			services => services.AddLatiprimerePdfium().AddLatiprimereWindows
			(
				options =>
				{
					options.PrintPdfOverIpp = true;
					options.IppPrinterUris[PrinterLock.MicrosoftPrintToPdf] = ippPrinter!;
				}
			)
		);

		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, PrinterLock.MicrosoftPrintToPdf);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		PrintOptions options = new PrintOptions { JobName = "Latiprimere direct IPP test", Copies = 2 };

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);

		// Assert — the printer's own outcome, not the spooler's "left the queue"
		Assert.Equal(WindowsPrintSystemIntegrationTests.ToId(PrinterLock.MicrosoftPrintToPdf), job.PrinterId);
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.DoesNotContain("Windows does not record", status.StateMessage ?? string.Empty, StringComparison.Ordinal);

		// And found again by its ID, which tells it from the spooler's jobs, from the printer that still has it
		Assert.StartsWith("ipp:", job.Id, StringComparison.Ordinal);
		IPrintJob? found = await printer.GetJobAsync(job.Id, TestContext.Current.CancellationToken);
		Assert.Equal(job.Id, found?.Id);
	}

	[Fact]
	public async Task GivenRenderDpiCapWhenPdfPrintedToMicrosoftPrintToPdfThenPagesAreRenderedAtTheCap()
	{
		// Arrange — the driver embeds the images it is given as they are, so their size shows the render resolution:
		// an A4 page at 100 DPI is 827 pixels wide, against about 4,960 at the driver's 600 DPI
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		const int maxRenderDpi = 100;

		await using CatalogFixture fixture = new CatalogFixture
		(
			services => services.AddLatiprimerePdfium().AddLatiprimereWindows(options => options.MaxRenderDpi = maxRenderDpi)
		);

		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, PrinterLock.MicrosoftPrintToPdf);
		using FileStream document = TestDocuments.Open(TestDocuments.MixedOnePage);
		string outputFile = Path.Combine(Path.GetTempPath(), $"latiprimere-test-{Guid.NewGuid():N}.pdf");
		PrintOptions options = new PrintOptions { JobName = "Latiprimere render cap test", OutputFile = outputFile };
		using PrinterLock printerLock = new PrinterLock(PrinterLock.MicrosoftPrintToPdf);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		byte[]? written = await OutputFileWaiter.WaitForAnyContentAsync(outputFile, TestContext.Current.CancellationToken);

		// Assert — the driver writes image dictionaries uncompressed, so their widths can be read as text
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.NotNull(written);

		int[] imageWidths = System.Text.RegularExpressions.Regex
			.Matches(Encoding.Latin1.GetString(written), @"/Width (\d+)")
			.Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
			.ToArray();

		Assert.NotEmpty(imageWidths);
		Assert.All(imageWidths, width => Assert.InRange(width, 1, (int)Math.Ceiling(8.27 * maxRenderDpi) + 1));
		File.Delete(outputFile);
	}

	[Fact]
	public async Task GivenSavedDevModeForA5WhenPdfPrintedToMicrosoftPrintToPdfThenOutputPagesAreA5()
	{
		// Arrange — the driver's own DEVMODE, changed to A5 (DMPAPER 11) as its settings dialog would, and saved
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		byte[] saved = WindowsCapabilityReader.ReadDefaultDevMode(PrinterLock.MicrosoftPrintToPdf);
		BinaryPrimitives.WriteInt16LittleEndian(saved.AsSpan(Marshal.OffsetOf<DevMode>(nameof(DevMode.PaperSize)).ToInt32()), 11);
		uint fields = BinaryPrimitives.ReadUInt32LittleEndian(saved.AsSpan(Marshal.OffsetOf<DevMode>(nameof(DevMode.Fields)).ToInt32()));
		fields = (fields | DevModeConstants.FieldPaperSize)
				 & ~(DevModeConstants.FieldPaperLength | DevModeConstants.FieldPaperWidth | DevModeConstants.FieldFormName);
		BinaryPrimitives.WriteUInt32LittleEndian(saved.AsSpan(Marshal.OffsetOf<DevMode>(nameof(DevMode.Fields)).ToInt32()), fields);

		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, PrinterLock.MicrosoftPrintToPdf);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		string outputFile = Path.Combine(Path.GetTempPath(), $"latiprimere-test-{Guid.NewGuid():N}.pdf");

		PrintOptions options = new PrintOptions
		{
			JobName       = "Latiprimere saved DEVMODE test",
			OutputFile    = outputFile,
			VendorOptions = new Dictionary<string, string> { ["windows:devmode"] = Convert.ToBase64String(saved) }
		};

		using PrinterLock printerLock = new PrinterLock(PrinterLock.MicrosoftPrintToPdf);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		byte[]? written = await OutputFileWaiter.WaitForAnyContentAsync(outputFile, TestContext.Current.CancellationToken);

		// Assert — A5 is 419.5 × 595.3 points; the driver's default would be A4 or Letter
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.NotNull(written);

		using (MemoryStream output = new MemoryStream(written))
		using (Latiprimere.Rendering.IRasterDocument printed = new Latiprimere.Rendering.Pdfium.PdfiumRasterizer().Open(output))
		{
			Latiprimere.Rendering.PageSize size = printed.GetPageSize(0);
			Assert.Equal(419.5, Math.Min(size.WidthPoints, size.HeightPoints), 1.5);
			Assert.Equal(595.3, Math.Max(size.WidthPoints, size.HeightPoints), 1.5);
		}

		File.Delete(outputFile);
	}

	[Fact]
	public async Task GivenPrintTicketForA5WhenPdfPrintedToMicrosoftPrintToPdfThenOutputPagesAreA5()
	{
		// Arrange — a delta ticket naming only the paper size; the driver fills in the rest
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

		PrintOptions options = new PrintOptions
		{
			JobName       = "Latiprimere Print Ticket test",
			OutputFile    = outputFile,
			VendorOptions = new Dictionary<string, string>
			{
				["windows:print-ticket"] = Latiprimere.Windows.Tests.Spooler.WindowsVendorOptionsTests.CreatePrintTicket("psk:ISOA5")
			}
		};

		using PrinterLock printerLock = new PrinterLock(PrinterLock.MicrosoftPrintToPdf);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		byte[]? written = await OutputFileWaiter.WaitForAnyContentAsync(outputFile, TestContext.Current.CancellationToken);

		// Assert — A5 is 419.5 × 595.3 points; the driver's default would be A4 or Letter
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.NotNull(written);

		using (MemoryStream output = new MemoryStream(written))
		using (Latiprimere.Rendering.IRasterDocument printed = new Latiprimere.Rendering.Pdfium.PdfiumRasterizer().Open(output))
		{
			Latiprimere.Rendering.PageSize size = printed.GetPageSize(0);
			Assert.Equal(419.5, Math.Min(size.WidthPoints, size.HeightPoints), 1.5);
			Assert.Equal(595.3, Math.Max(size.WidthPoints, size.HeightPoints), 1.5);
		}

		File.Delete(outputFile);
	}

	[Fact]
	public void GivenPrintTicketForA5WhenMergedForMicrosoftPrintToPdfThenDevModeAsksForA5()
	{
		// Arrange — straight through the Print Ticket API, from the driver's defaults
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		byte[] defaults = WindowsCapabilityReader.ReadDefaultDevMode(PrinterLock.MicrosoftPrintToPdf);
		byte[] ticket = Encoding.UTF8.GetBytes(Latiprimere.Windows.Tests.Spooler.WindowsVendorOptionsTests.CreatePrintTicket("psk:ISOA5"));

		// Act
		byte[] merged = Latiprimere.Windows.PrintTickets.PrintTicketMerger.Merge(PrinterLock.MicrosoftPrintToPdf, defaults, [ticket]);

		// Assert — DMPAPER_A5 is 11, or the driver names the form instead
		short paperSize = BinaryPrimitives.ReadInt16LittleEndian(merged.AsSpan(Marshal.OffsetOf<DevMode>(nameof(DevMode.PaperSize)).ToInt32()));
		uint fields = BinaryPrimitives.ReadUInt32LittleEndian(merged.AsSpan(Marshal.OffsetOf<DevMode>(nameof(DevMode.Fields)).ToInt32()));
		Assert.True((fields & DevModeConstants.FieldPaperSize) != 0, "The DEVMODE sets a paper size");
		Assert.Equal(11, paperSize);
	}

	[Fact]
	public async Task GivenVendorFeatureFromCapabilitiesWhenMergedForXpsDocumentWriterThenDevModeKeepsTheChoice()
	{
		// Arrange — the driver's own feature, as GetCapabilitiesAsync lists it, chosen as a job would
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");
		const string xpsWriter = "Microsoft XPS Document Writer";
		Assert.SkipWhen(new WinSpoolApi().GetPrinter(xpsWriter) is null, "Microsoft XPS Document Writer is not installed.");

		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, xpsWriter);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);
		VendorOption images = Assert.Single(capabilities.VendorOptions, option => option.Key.EndsWith(":JobImageType", StringComparison.Ordinal));
		VendorOptionChoice chosen = Assert.Single(images.Choices, choice => choice.Value.EndsWith(":PNG", StringComparison.Ordinal));
		Assert.NotEqual(chosen.Value, images.DefaultValue);

		WinSpoolApi spool = new WinSpoolApi();
		Dictionary<string, string> vendorOptions = new Dictionary<string, string> { [images.Key] = chosen.Value };
		byte[]? ticket = Latiprimere.Windows.PrintTickets.PrintCapabilitiesDocument
			.Parse(spool.GetPrintCapabilities(xpsWriter, null, []))
			.CreateTicket(xpsWriter, vendorOptions, new PrintOptions());

		byte[] defaults = WindowsCapabilityReader.ReadDefaultDevMode(xpsWriter);

		// Act
		byte[] merged = Latiprimere.Windows.PrintTickets.PrintTicketMerger.Merge(xpsWriter, defaults, [ticket!]);

		// Assert — read back through the driver, the DEVMODE asks for PNG images
		string mergedTicket;

		using (Latiprimere.Windows.PrintTickets.PrintTicketSession session = Latiprimere.Windows.PrintTickets.PrintTicketSession.Open(xpsWriter))
		{
			mergedTicket = Encoding.UTF8.GetString(session.ConvertDevModeToTicket(merged));
		}

		Assert.Contains(":PNG\"", mergedTicket, StringComparison.Ordinal);
	}

	[Fact]
	public void GivenPrintTicketSessionDisposedTwiceWhenAnotherIsOpenedOnTheSameThreadThenItWorks()
	{
		// Arrange — a second Dispose mustn't uninitialise COM again on a thread still in use
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		byte[] defaults = WindowsCapabilityReader.ReadDefaultDevMode(PrinterLock.MicrosoftPrintToPdf);
		Latiprimere.Windows.PrintTickets.PrintTicketSession first = Latiprimere.Windows.PrintTickets.PrintTicketSession.Open(PrinterLock.MicrosoftPrintToPdf);

		// Act
		first.Dispose();
		first.Dispose();
		byte[] ticket;

		using (Latiprimere.Windows.PrintTickets.PrintTicketSession second = Latiprimere.Windows.PrintTickets.PrintTicketSession.Open(PrinterLock.MicrosoftPrintToPdf))
		{
			ticket = second.ConvertDevModeToTicket(defaults);
		}

		// Assert
		Assert.Contains("PrintTicket", Encoding.UTF8.GetString(ticket), StringComparison.Ordinal);
	}

	[Fact]
	public void GivenPrintTicketTheDriverFindsMalformedWhenMergedForMicrosoftPrintToPdfThenThrowsArgumentException()
	{
		// Arrange — well-formed XML, but a Feature without a name isn't a valid Print Ticket
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		byte[] defaults = WindowsCapabilityReader.ReadDefaultDevMode(PrinterLock.MicrosoftPrintToPdf);

		byte[] ticket = Encoding.UTF8.GetBytes
		(
			"<psf:PrintTicket xmlns:psf=\"http://schemas.microsoft.com/windows/2003/08/printing/printschemaframework\" version=\"1\">"
			+ "<psf:Feature><psf:Option /></psf:Feature></psf:PrintTicket>"
		);

		// Act
		Action merge = () => Latiprimere.Windows.PrintTickets.PrintTicketMerger.Merge(PrinterLock.MicrosoftPrintToPdf, defaults, [ticket]);

		// Assert
		ArgumentException exception = Assert.Throws<ArgumentException>(merge);
		Assert.Contains(PrinterLock.MicrosoftPrintToPdf, exception.Message, StringComparison.Ordinal);
		Assert.Contains("windows:print-ticket", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenPaperSizeFromCapabilitiesWhenPdfPrintedToMicrosoftPrintToPdfThenOutputPagesHaveThatSize()
	{
		// Arrange — A5 is neither the A4 nor the Letter default, so a size that wasn't applied shows
		Assert.SkipUnless(OperatingSystem.IsWindows(), "The Windows spooler is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(PrinterLock.MicrosoftPrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		await using CatalogFixture fixture = WindowsPrintSystemIntegrationTests.CreateCatalog();
		IPrinter printer = await WindowsPrintSystemIntegrationTests.GetPrinterAsync(fixture, PrinterLock.MicrosoftPrintToPdf);
		PrinterCapabilities capabilities = await printer.GetCapabilitiesAsync(TestContext.Current.CancellationToken);
		MediaSize? a5 = capabilities.MediaSizes.FirstOrDefault(size => size.Name == "iso_a5_148x210mm");
		Assert.SkipWhen(a5 is null, "Microsoft Print to PDF doesn't list A5 on this computer.");

		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		string outputFile = Path.Combine(Path.GetTempPath(), $"latiprimere-test-{Guid.NewGuid():N}.pdf");
		PrintOptions options = new PrintOptions { JobName = "Latiprimere A5 test", OutputFile = outputFile, MediaSize = a5 };
		using PrinterLock printerLock = new PrinterLock(PrinterLock.MicrosoftPrintToPdf);

		// Act
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), options, TestContext.Current.CancellationToken);
		PrintJobStatus status = await PrintJobWaiter.WaitUntilFinishedAsync(job, TestContext.Current.CancellationToken);
		byte[]? written = await OutputFileWaiter.WaitForAnyContentAsync(outputFile, TestContext.Current.CancellationToken);

		// Assert — A5 is 148 × 210 mm: 419.5 × 595.3 points, in portrait or landscape as the page needs
		Assert.Equal(PrintJobState.Completed, status.State);
		Assert.NotNull(written);

		using (MemoryStream output = new MemoryStream(written))
		using (Latiprimere.Rendering.IRasterDocument printed = new Latiprimere.Rendering.Pdfium.PdfiumRasterizer().Open(output))
		{
			Assert.True(printed.PageCount > 0);

			for (int page = 0; page < printed.PageCount; page++)
			{
				Latiprimere.Rendering.PageSize size = printed.GetPageSize(page);
				Assert.Equal(419.5, Math.Min(size.WidthPoints, size.HeightPoints), 1.5);
				Assert.Equal(595.3, Math.Max(size.WidthPoints, size.HeightPoints), 1.5);
			}
		}

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
