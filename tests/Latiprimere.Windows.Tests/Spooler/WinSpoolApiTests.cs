using System.Runtime.Versioning;
using Latiprimere.Windows.Spooler;
using Xunit;

namespace Latiprimere.Windows.Tests.Spooler;

/// <summary>
/// Read-only checks against the real Windows spooler. They change nothing on the machine.
/// </summary>
[Trait("Category", "Integration")]
[Trait("OS", "Windows")]
[SupportedOSPlatform("windows")]
public sealed class WinSpoolApiTests
{
	private const string SkipReason = "The Windows spooler is only available on Windows."; // Reason shown when skipped

	[Fact]
	public void GivenThisMachineWhenEnumeratePrintersThenEveryPrinterHasAName()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), WinSpoolApiTests.SkipReason);
		WinSpoolApi spool = new WinSpoolApi();

		// Act
		IReadOnlyList<WindowsPrinterRecord> printers = spool.EnumeratePrinters();

		// Assert
		Assert.All(printers, printer => Assert.False(string.IsNullOrWhiteSpace(printer.Name)));
	}

	[Fact]
	public void GivenListedPrinterWhenGetPrinterThenReturnsSamePrinter()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), WinSpoolApiTests.SkipReason);
		WinSpoolApi spool = new WinSpoolApi();
		IReadOnlyList<WindowsPrinterRecord> printers = spool.EnumeratePrinters();
		WindowsPrinterRecord? firstPrinter = printers.Count > 0 ? printers[0] : null;
		Assert.SkipWhen(firstPrinter is null, "This machine has no printers.");

		// Act
		WindowsPrinterRecord? printer = spool.GetPrinter(firstPrinter!.Name); // Not null: skipped above otherwise

		// Assert
		Assert.Equal(firstPrinter.Name, printer?.Name);
	}

	[Fact]
	public void GivenUnknownPrinterNameWhenGetPrinterThenReturnsNull()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), WinSpoolApiTests.SkipReason);
		WinSpoolApi spool = new WinSpoolApi();

		// Act
		WindowsPrinterRecord? printer = spool.GetPrinter("Latiprimere printer that does not exist " + Guid.NewGuid());

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public void GivenThisMachineWhenGetDefaultPrinterNameThenDefaultIsListedOrNone()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), WinSpoolApiTests.SkipReason);
		WinSpoolApi spool = new WinSpoolApi();

		// Act
		string? defaultName = spool.GetDefaultPrinterName();

		// Assert
		if (defaultName is not null)
		{
			Assert.Contains(spool.EnumeratePrinters(), printer => printer.Name == defaultName);
		}
	}

	[Fact]
	public void GivenMicrosoftPrintToPdfWhenGetCapabilitiesThenReadsPapersAndDefaults()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), WinSpoolApiTests.SkipReason);
		WinSpoolApi spool = new WinSpoolApi();

		Assert.SkipWhen
		(
			spool.GetPrinter("Microsoft Print to PDF") is null,
			"Microsoft Print to PDF is not installed."
		);

		// Act
		WindowsCapabilitiesRecord? capabilities = spool.GetCapabilities("Microsoft Print to PDF");

		// Assert — the driver offers A4 (DMPAPER_A4 = 9) and Letter (1), and sets a default paper
		Assert.NotNull(capabilities);
		Assert.Contains(capabilities.Papers, paper => paper.Id == 9 && paper.WidthTenthsOfMillimetre == 2100);
		Assert.Contains(capabilities.Papers, paper => paper.Id == 1);
		Assert.All(capabilities.Papers, paper => Assert.False(string.IsNullOrWhiteSpace(paper.Name)));
		Assert.NotNull(capabilities.DefaultPaperId);
		Assert.True(capabilities.CanPrintLandscape);
	}

	[Fact]
	public void GivenUnknownPrinterWhenGetCapabilitiesThenReturnsNull()
	{
		// Arrange
		Assert.SkipUnless(OperatingSystem.IsWindows(), WinSpoolApiTests.SkipReason);
		WinSpoolApi spool = new WinSpoolApi();

		// Act
		WindowsCapabilitiesRecord? capabilities = spool.GetCapabilities("Latiprimere No Such Printer");

		// Assert
		Assert.Null(capabilities);
	}
}
