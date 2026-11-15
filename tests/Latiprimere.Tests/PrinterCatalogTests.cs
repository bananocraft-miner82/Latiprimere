using Latiprimere.Tests.Builders;
using Latiprimere.Tests.Fakes;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Latiprimere.Tests;

public sealed class PrinterCatalogTests
{
	[Fact]
	public void GivenNullPrintSystemsWhenConstructedThenThrowsArgumentNullException()
	{
		// Arrange
		IEnumerable<IPrintSystem>? printSystems = null;

		// Act
		// The null-forgiving operator is deliberate: the test checks the null guard
		Func<PrinterCatalog> createCatalog = () => new PrinterCatalog(printSystems!);

		// Assert
		Assert.Throws<ArgumentNullException>(createCatalog);
	}

	[Fact]
	public async Task GivenNoPrintSystemsWhenGetPrintersThenThrowsPrintSystemUnavailableException()
	{
		// Arrange
		PrinterCatalog catalog = new PrinterCatalog(Array.Empty<IPrintSystem>());

		// Act
		Func<Task> getPrinters = () => catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintSystemUnavailableException>(getPrinters);
	}

	[Fact]
	public async Task GivenSlowPrintSystemsWhenGetPrintersThenAsksThemAllAtOnceAndKeepsTheirOrder()
	{
		// Arrange — neither print system answers until both have been asked
		TaskCompletionSource release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		FakePrintSystem first = new FakePrintSystem("First", true, new FakePrinter(new PrinterInfoBuilder().WithId("first:A").Build()))
		{
			ListingGate = release.Task
		};

		FakePrintSystem second = new FakePrintSystem("Second", true, new FakePrinter(new PrinterInfoBuilder().WithId("second:B").Build()))
		{
			ListingGate = release.Task
		};

		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { first, second });

		// Act
		Task<IReadOnlyList<PrinterInfo>> listing = catalog.GetPrintersAsync(TestContext.Current.CancellationToken);
		await Task
			.WhenAll(first.ListingStarted, second.ListingStarted)
			.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		release.SetResult();
		IReadOnlyList<PrinterInfo> printers = await listing;

		// Assert
		Assert.Equal(new[] { "first:A", "second:B" }, printers.Select(printer => printer.Id));
	}

	[Fact]
	public async Task GivenCancellationWhileListingWhenGetPrintersThenThrowsOperationCanceledExceptionWithoutLoggingFailures()
	{
		// Arrange — both print systems are still listing when the caller gives up
		TaskCompletionSource neverReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		FakePrintSystem first = new FakePrintSystem("First", true) { ListingGate = neverReleased.Task };
		FakePrintSystem second = new FakePrintSystem("Second", true) { ListingGate = neverReleased.Task };
		FakeLogger<PrinterCatalog> logger = new FakeLogger<PrinterCatalog>();
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { first, second }, logger);
		using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		// Act
		Task<IReadOnlyList<PrinterInfo>> listing = catalog.GetPrintersAsync(cancellation.Token);
		await Task
			.WhenAll(first.ListingStarted, second.ListingStarted)
			.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
		await cancellation.CancelAsync();

		// Assert — the caller's own cancellation, not a print system failing, so nothing is skipped or logged
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => listing);
		Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
	}

	[Fact]
	public async Task GivenOnlyUnsupportedPrintSystemWhenGetPrintersThenThrowsAndNeverQueriesIt()
	{
		// Arrange
		FakePrinter printer = new FakePrinter(new PrinterInfoBuilder().Build());
		FakePrintSystem unsupportedSystem = new FakePrintSystem("Unsupported", false, printer);
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { unsupportedSystem });

		// Act
		Func<Task> getPrinters = () => catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintSystemUnavailableException>(getPrinters);
		Assert.Equal(0, unsupportedSystem.RequestCount);
	}

	[Fact]
	public async Task GivenTwoSupportedPrintSystemsWhenGetPrintersThenReturnsPrintersFromBoth()
	{
		// Arrange
		FakePrintSystem firstSystem = new FakePrintSystem
		(
			"First",
			true,
			new FakePrinter(new PrinterInfoBuilder().WithId("first-a").Build()),
			new FakePrinter(new PrinterInfoBuilder().WithId("first-b").Build())
		);

		FakePrintSystem secondSystem = new FakePrintSystem
		(
			"Second",
			true,
			new FakePrinter(new PrinterInfoBuilder().WithId("second-a").Build())
		);

		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { firstSystem, secondSystem });

		// Act
		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		List<string> printerIds = printers
			.Select(printer => printer.Id)
			.ToList();

		Assert.Equal(new[] { "first-a", "first-b", "second-a" }, printerIds);
	}

	[Fact]
	public async Task GivenSupportedPrintSystemWithNoPrintersWhenGetPrintersThenReturnsEmptyList()
	{
		// Arrange
		FakePrintSystem emptySystem = new FakePrintSystem("Empty", true);
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { emptySystem });

		// Act
		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Empty(printers);
	}

	[Fact]
	public async Task GivenPrinterInSecondPrintSystemWhenGetPrinterThenReturnsIt()
	{
		// Arrange
		FakePrintSystem firstSystem = new FakePrintSystem("First", true);
		FakePrinter expectedPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("label-printer").Build());
		FakePrintSystem secondSystem = new FakePrintSystem("Second", true, expectedPrinter);
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { firstSystem, secondSystem });

		// Act
		IPrinter printer = await catalog.GetPrinterAsync("label-printer", TestContext.Current.CancellationToken);

		// Assert
		Assert.Same(expectedPrinter, printer);
	}

	[Fact]
	public async Task GivenPrinterInFirstPrintSystemWhenGetPrinterThenDoesNotQuerySecondSystem()
	{
		// Arrange
		FakePrinter expectedPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("office").Build());
		FakePrintSystem firstSystem = new FakePrintSystem("First", true, expectedPrinter);
		FakePrintSystem secondSystem = new FakePrintSystem("Second", true);
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { firstSystem, secondSystem });

		// Act
		await catalog.GetPrinterAsync("office", TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(0, secondSystem.RequestCount);
	}

	[Fact]
	public async Task GivenUnknownPrinterIdWhenGetPrinterThenThrowsPrinterNotFoundExceptionWithId()
	{
		// Arrange
		FakePrintSystem printSystem = new FakePrintSystem
		(
			"Only",
			true,
			new FakePrinter(new PrinterInfoBuilder().WithId("office").Build())
		);

		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { printSystem });

		// Act
		Func<Task> getPrinter = () => catalog.GetPrinterAsync("warehouse", TestContext.Current.CancellationToken);

		// Assert
		PrinterNotFoundException exception = await Assert.ThrowsAsync<PrinterNotFoundException>(getPrinter);
		Assert.Equal("warehouse", exception.PrinterId);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task GivenBlankPrinterIdWhenGetPrinterThenThrowsArgumentException(string printerId)
	{
		// Arrange — printerID is empty or whitespace (provided by InlineData above)
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { new FakePrintSystem("Only", true) });

		// Act
		Func<Task> getPrinter = () => catalog.GetPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<ArgumentException>(getPrinter);
	}

	[Fact]
	public async Task GivenNullPrinterIdWhenGetPrinterThenThrowsArgumentNullException()
	{
		// Arrange
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { new FakePrintSystem("Only", true) });
		string? printerId = null;

		// Act
		Func<Task> getPrinter = () => catalog.GetPrinterAsync
		(
			printerId!, // Deliberately null to test the guard
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<ArgumentNullException>(getPrinter);
	}

	[Fact]
	public async Task GivenDefaultPrinterInSecondPrintSystemWhenGetDefaultPrinterThenReturnsIt()
	{
		// Arrange
		FakePrintSystem firstSystem = new FakePrintSystem
		(
			"First",
			true,
			new FakePrinter(new PrinterInfoBuilder().WithId("not-default").Build())
		);

		FakePrinter expectedPrinter = new FakePrinter(new PrinterInfoBuilder().WithId("default").AsDefault().Build());
		FakePrintSystem secondSystem = new FakePrintSystem("Second", true, expectedPrinter);
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { firstSystem, secondSystem });

		// Act
		IPrinter? printer = await catalog.GetDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Same(expectedPrinter, printer);
	}

	[Fact]
	public async Task GivenNoDefaultPrinterWhenGetDefaultPrinterThenReturnsNull()
	{
		// Arrange
		FakePrintSystem printSystem = new FakePrintSystem
		(
			"Only",
			true,
			new FakePrinter(new PrinterInfoBuilder().Build())
		);

		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { printSystem });

		// Act
		IPrinter? printer = await catalog.GetDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public async Task GivenNoPrintSystemsWhenGetDefaultPrinterThenThrowsPrintSystemUnavailableException()
	{
		// Arrange
		PrinterCatalog catalog = new PrinterCatalog(Array.Empty<IPrintSystem>());

		// Act
		Func<Task> getDefaultPrinter = () => catalog.GetDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintSystemUnavailableException>(getDefaultPrinter);
	}

	[Fact]
	public async Task GivenOneFailingPrintSystemWhenGetPrintersThenReturnsOthersAndLogsWarning()
	{
		// Arrange
		PrintException failure = new PrintException("CUPS unreachable.");
		FakePrintSystem failingSystem = new FakePrintSystem("CUPS", true) { FailureToThrow = failure };

		FakePrintSystem workingSystem = new FakePrintSystem
		(
			"Windows",
			true,
			new FakePrinter(new PrinterInfoBuilder().WithId("windows:Office").Build())
		);

		FakeLogger<PrinterCatalog> logger = new FakeLogger<PrinterCatalog>();
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { failingSystem, workingSystem }, logger);

		// Act
		IReadOnlyList<PrinterInfo> printers = await catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrinterInfo printer = Assert.Single(printers);
		Assert.Equal("windows:Office", printer.Id);
		LoggedEntry warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
		Assert.Contains("CUPS", warning.Message, StringComparison.Ordinal);
		Assert.Same(failure, warning.Exception);
	}

	[Fact]
	public async Task GivenOnlyPrintSystemFailsWhenGetPrintersThenRethrowsItsException()
	{
		// Arrange
		PrintException failure = new PrintException("CUPS unreachable.") { NativeErrorCode = 400 };
		FakePrintSystem failingSystem = new FakePrintSystem("CUPS", true) { FailureToThrow = failure };
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { failingSystem });

		// Act
		Func<Task> getPrinters = () => catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(getPrinters);
		Assert.Same(failure, exception);
	}

	[Fact]
	public async Task GivenEveryPrintSystemFailsWhenGetPrintersThenThrowsPrintExceptionWrappingEachFailure()
	{
		// Arrange
		PrintException firstFailure = new PrintException("CUPS unreachable.");
		PrintException secondFailure = new PrintException("Spooler stopped.");

		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[]
		{
			new FakePrintSystem("CUPS", true) { FailureToThrow = firstFailure },
			new FakePrintSystem("Windows", true) { FailureToThrow = secondFailure },
		});

		// Act
		Func<Task> getPrinters = () => catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(getPrinters);
		AggregateException failures = Assert.IsType<AggregateException>(exception.InnerException);
		Assert.Equal(new Exception[] { firstFailure, secondFailure }, failures.InnerExceptions);
	}

	[Fact]
	public async Task GivenPrintSystemThrowsNonPrintExceptionWhenGetPrintersThenItPropagates()
	{
		// Arrange
		FakePrintSystem brokenSystem = new FakePrintSystem("Broken", true)
		{
			FailureToThrow = new InvalidOperationException("Bug in adapter.")
		};

		FakePrintSystem workingSystem = new FakePrintSystem("Working", true);
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { brokenSystem, workingSystem });

		// Act
		Func<Task> getPrinters = () => catalog.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<InvalidOperationException>(getPrinters);
	}

	[Fact]
	public async Task GivenFirstPrintSystemFailsWhenGetDefaultPrinterThenReturnsDefaultFromSecond()
	{
		// Arrange
		FakePrintSystem failingSystem = new FakePrintSystem("CUPS", true)
		{
			FailureToThrow = new PrintException("CUPS unreachable.")
		};

		FakePrinter expectedPrinter = new FakePrinter
		(
			new PrinterInfoBuilder().WithId("windows:Office").AsDefault().Build()
		);

		FakePrintSystem workingSystem = new FakePrintSystem("Windows", true, expectedPrinter);
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { failingSystem, workingSystem });

		// Act
		IPrinter? printer = await catalog.GetDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Same(expectedPrinter, printer);
	}

	[Fact]
	public async Task GivenOneFailingAndOneWithoutDefaultWhenGetDefaultPrinterThenReturnsNull()
	{
		// Arrange
		FakePrintSystem failingSystem = new FakePrintSystem("CUPS", true)
		{
			FailureToThrow = new PrintException("CUPS unreachable.")
		};

		FakePrintSystem systemWithoutDefault = new FakePrintSystem("Windows", true);
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { failingSystem, systemWithoutDefault });

		// Act
		IPrinter? printer = await catalog.GetDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public async Task GivenEveryPrintSystemFailsWhenGetDefaultPrinterThenThrowsPrintException()
	{
		// Arrange
		PrintException failure = new PrintException("CUPS unreachable.");
		FakePrintSystem failingSystem = new FakePrintSystem("CUPS", true) { FailureToThrow = failure };
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { failingSystem });

		// Act
		Func<Task> getDefaultPrinter = () => catalog.GetDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(getDefaultPrinter);
		Assert.Same(failure, exception);
	}

	[Fact]
	public async Task GivenOwningPrintSystemFailsWhenGetPrinterThenFailurePropagates()
	{
		// Arrange
		PrintException failure = new PrintException("CUPS unreachable.");
		FakePrintSystem failingSystem = new FakePrintSystem("CUPS", true) { FailureToThrow = failure };
		PrinterCatalog catalog = new PrinterCatalog(new IPrintSystem[] { failingSystem });

		// Act
		Func<Task> getPrinter = () => catalog.GetPrinterAsync("cups:Office", TestContext.Current.CancellationToken);

		// Assert
		PrintException exception = await Assert.ThrowsAsync<PrintException>(getPrinter);
		Assert.Same(failure, exception);
	}
}
