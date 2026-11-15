using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Builders;
using Latiprimere.Windows.Tests.Fakes;
using Xunit;

namespace Latiprimere.Windows.Tests;

public sealed class WindowsPrintSystemTests
{
	[Fact]
	public async Task GivenPrintersWhenGetPrintersThenReturnsPrefixedPrintersWithDefaultFlaggedIgnoringCase()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool { DefaultPrinterName = "microsoft print to pdf" };
		spool.AddPrinter(new WindowsPrinterRecord { Name = "Microsoft Print to PDF" });
		spool.AddPrinter(new WindowsPrinterRecord { Name = "Office" });
		WindowsPrintSystem printSystem = WindowsPrintSystemTests.CreatePrintSystem(spool);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(2, printers.Count);
		Assert.Equal("windows:Microsoft Print to PDF", printers[0].Id);
		Assert.True(printers[0].IsDefault);
		Assert.Equal("windows:Office", printers[1].Id);
		Assert.False(printers[1].IsDefault);
	}

	[Theory]
	[InlineData("Office")]
	[InlineData("cups:Office")]
	[InlineData("windows:")]
	public async Task GivenIdNotFromWindowsWhenFindPrinterThenReturnsNull(string printerId)
	{
		// Arrange — printerID lacks the windows: prefix or a printer name (provided by InlineData above)
		FakeWinSpool spool = new FakeWinSpool();
		spool.AddPrinter(new WindowsPrinterRecord { Name = "Office" });
		WindowsPrintSystem printSystem = WindowsPrintSystemTests.CreatePrintSystem(spool);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
	}

	[Theory]
	[InlineData(@"windows:\\attacker.example\x")]
	[InlineData("windows:,XcvMonitor Local Port")]
	[InlineData("windows:Office,Job 5")]
	public async Task GivenUnlistedNameThatReachesBeyondThisComputerWhenFindPrinterThenReturnsNullWithoutOpeningIt(string printerId)
	{
		// Arrange — printerId names another computer or a special spooler handle, and Windows lists no such
		// printer (provided by InlineData above)
		FakeWinSpool spool = new FakeWinSpool();
		spool.AddPrinter(new WindowsPrinterRecord { Name = "Office" });
		WindowsPrintSystem printSystem = WindowsPrintSystemTests.CreatePrintSystem(spool);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert — never opened, so OpenPrinter never contacts the host
		Assert.Null(printer);
		Assert.Empty(spool.OpenedPrinterNames);
	}

	[Fact]
	public async Task GivenAnyNameAllowedWhenUnlistedRemotePrinterLookedUpThenOpensIt()
	{
		// Arrange — the application trusts its identifiers and needs printers on servers not yet connected
		FakeWinSpool spool = new FakeWinSpool();
		spool.AddPrinter(new WindowsPrinterRecord { Name = @"\\printserver\Office" });
		WindowsPrintContext context = new WindowsPrintContextBuilder().WithSpool(spool).Build() with { AllowsAnyPrinterName = true };
		WindowsPrintSystem printSystem = new WindowsPrintSystem(context, isSupported: true);

		// Act — the fake lists the printer, but with any name allowed the list isn't consulted first
		IPrinter? printer = await printSystem.FindPrinterAsync(@"windows:\\printserver\Office", TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal(new[] { @"\\printserver\Office" }, spool.OpenedPrinterNames);
	}

	[Fact]
	public async Task GivenPrinterConnectionWindowsListsWhenFindPrinterThenReturnsIt()
	{
		// Arrange — a shared printer the user has connected to is listed under its UNC name
		FakeWinSpool spool = new FakeWinSpool();
		spool.AddPrinter(new WindowsPrinterRecord { Name = @"\\printserver\Office" });
		WindowsPrintSystem printSystem = WindowsPrintSystemTests.CreatePrintSystem(spool);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync(@"windows:\\PRINTSERVER\office", TestContext.Current.CancellationToken);

		// Assert — checked against the names alone, not the details that ask every connected print server
		Assert.Equal(@"windows:\\printserver\Office", printer?.Info.Id);
		Assert.Equal(0, spool.FullEnumerationCount);
	}

	[Fact]
	public async Task GivenKnownPrinterWhenFindPrinterThenReturnsItWithSpoolerName()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool { DefaultPrinterName = "Office" };
		spool.AddPrinter(new WindowsPrinterRecord { Name = "Office" });
		WindowsPrintSystem printSystem = WindowsPrintSystemTests.CreatePrintSystem(spool);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync("windows:office", TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal("windows:Office", printer.Info.Id);
		Assert.True(printer.Info.IsDefault);
	}

	[Fact]
	public async Task GivenUnknownPrinterWhenFindPrinterThenReturnsNull()
	{
		// Arrange
		WindowsPrintSystem printSystem = WindowsPrintSystemTests.CreatePrintSystem(new FakeWinSpool());

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync
		(
			"windows:Missing",
			TestContext.Current.CancellationToken
		);

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public async Task GivenDefaultPrinterWhenFindDefaultPrinterThenReturnsIt()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool { DefaultPrinterName = "Office" };
		spool.AddPrinter(new WindowsPrinterRecord { Name = "Office" });
		WindowsPrintSystem printSystem = WindowsPrintSystemTests.CreatePrintSystem(spool);

		// Act
		IPrinter? printer = await printSystem.FindDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("windows:Office", printer?.Info.Id);
	}

	[Fact]
	public async Task GivenNoDefaultPrinterWhenFindDefaultPrinterThenReturnsNull()
	{
		// Arrange
		FakeWinSpool spool = new FakeWinSpool { DefaultPrinterName = null };
		WindowsPrintSystem printSystem = WindowsPrintSystemTests.CreatePrintSystem(spool);

		// Act
		IPrinter? printer = await printSystem.FindDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public void GivenNullContextWhenConstructedThenThrowsArgumentNullException()
	{
		// Arrange
		WindowsPrintContext? context = null;

		// Act
		// The null-forgiving operator is deliberate: the test checks the null guard
		Func<WindowsPrintSystem> create = () => new WindowsPrintSystem(context!, isSupported: true);

		// Assert
		Assert.Throws<ArgumentNullException>(create);
	}

	[Fact]
	public void GivenFactoryWhenCreateThenSupportedOnlyOnWindows()
	{
		// Arrange
		bool isWindows = OperatingSystem.IsWindows();

		// Act
		IPrintSystem printSystem = WindowsPrintSystemFactory.Create();

		// Assert
		Assert.Equal(isWindows, printSystem.IsSupported);
		Assert.Equal("Windows", printSystem.Name);
	}

	/// <summary>
	/// Creates a supported print system over the given fake spooler.
	/// </summary>
	/// <param name="spool">The fake spooler.</param>
	/// <returns>The print system.</returns>
	private static WindowsPrintSystem CreatePrintSystem(FakeWinSpool spool)
	{
		return new WindowsPrintSystem(new WindowsPrintContextBuilder().WithSpool(spool).Build(), isSupported: true);
	}
}
