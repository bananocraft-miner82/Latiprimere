using System.Runtime.Versioning;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Fakes;
using Latiprimere.Windows.Tests.Fixtures;
using Xunit;

namespace Latiprimere.Windows.Tests.Gdi;

/// <summary>
/// Drives the real GDI print path into Microsoft Print to PDF, writing to a temporary file. Nothing reaches paper.
/// Skipped where that printer isn't installed. Each test holds a <see cref="PrinterLock"/>, as concurrent jobs from
/// the test processes of the different target frameworks can stall.
/// </summary>
[Trait("Category", "Integration")]
[Trait("OS", "Windows")]
[SupportedOSPlatform("windows")]
public sealed class GdiRasterPrinterTests
{
	private const string PrintToPdf = "Microsoft Print to PDF"; // Virtual printer that writes PDF files

	[Fact]
	public void GivenTwoPageDocumentWhenPrintedToPdfThenWritesFileAndReturnsJobId()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), "GDI is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(GdiRasterPrinterTests.PrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		string outputFile = Path.Combine(Path.GetTempPath(), $"latiprimere-test-{Guid.NewGuid():N}.pdf");
		using FakeRasterDocument document = new FakeRasterDocument(2);
		using PrinterLock printerLock = new PrinterLock(PrinterLock.MicrosoftPrintToPdf);

		GdiPrintRequest request = new GdiPrintRequest
		{
			PrinterName  = GdiRasterPrinterTests.PrintToPdf,
			DocumentName = "Latiprimere GDI test",
			OutputFile   = outputFile,
			Document     = document,
			Options      = PrintOptions.Default,
			Pages        = Enumerable.Range(0, document.PageCount).ToArray()
		};

		// Act
		int jobId = new GdiRasterPrinter(maxRenderDpi: 72).PrintRasterDocument
		(
			request,
			TestContext.Current.CancellationToken
		);

		// Assert — the driver finishes writing shortly after the job is spooled
		Assert.True(jobId > 0);
		Assert.True(GdiRasterPrinterTests.WaitForFile(outputFile), "The PDF was not written.");
		File.Delete(outputFile);
	}

	[Fact]
	public void GivenCancelledTokenWhenPrintedThenThrowsAndWritesNothing()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), "GDI is only available on Windows.");

		Assert.SkipWhen
		(
			new WinSpoolApi().GetPrinter(GdiRasterPrinterTests.PrintToPdf) is null,
			"Microsoft Print to PDF is not installed."
		);

		string outputFile = Path.Combine(Path.GetTempPath(), $"latiprimere-test-{Guid.NewGuid():N}.pdf");
		using FakeRasterDocument document = new FakeRasterDocument(1);
		using PrinterLock printerLock = new PrinterLock(PrinterLock.MicrosoftPrintToPdf);

		GdiPrintRequest request = new GdiPrintRequest
		{
			PrinterName  = GdiRasterPrinterTests.PrintToPdf,
			DocumentName = "Latiprimere GDI cancel test",
			OutputFile   = outputFile,
			Document     = document,
			Options      = PrintOptions.Default,
			Pages        = Enumerable.Range(0, document.PageCount).ToArray()
		};

		using CancellationTokenSource cancellation = new CancellationTokenSource();
		cancellation.Cancel();

		// Act
		Action print = () => new GdiRasterPrinter(maxRenderDpi: 72).PrintRasterDocument(request, cancellation.Token);

		// Assert
		Assert.ThrowsAny<OperationCanceledException>(print);
		Assert.False(File.Exists(outputFile));
	}

	/// <summary>
	/// Waits up to 30 seconds for a file to appear with content; alone, Microsoft Print to PDF takes a second or two.
	/// </summary>
	/// <param name="path">The file path.</param>
	/// <returns>Whether the file appeared.</returns>
	private static bool WaitForFile(string path)
	{
		DateTime deadline = DateTime.UtcNow.AddSeconds(30);

		while (DateTime.UtcNow < deadline)
		{
			if (File.Exists(path) && new FileInfo(path).Length > 0)
			{
				return true;
			}

			Thread.Sleep(100);
		}

		return false;
	}
}
