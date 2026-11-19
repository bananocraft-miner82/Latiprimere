using Latiprimere.Ipp;
using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using SharpIpp.Protocol.Models;
using Xunit;

namespace Latiprimere.Cups.Tests;

public sealed class CupsPrintSystemTests
{
	/// <summary>The endpoint used by every test: a CUPS server reached over TCP.</summary>
	private static readonly CupsEndpoint Endpoint = new CupsEndpoint { ServerUri = new Uri("ipp://localhost:631/") };

	[Fact]
	public async Task GivenQueuesWhenGetPrintersThenReturnsPrefixedPrintersWithDefaultFlagged()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			CupsPrinters =
			[
				new PrinterAttributesBuilder().WithName("CUPS-PDF").Build(),
				new PrinterAttributesBuilder()
					.WithName("Raw")
					.WithState(SharpIpp.Protocol.Models.PrinterState.Stopped)
					.Build(),
			],
			DefaultPrinterName = "CUPS-PDF"
		};
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal(2, printers.Count);
		Assert.Equal("cups:CUPS-PDF", printers[0].Id);
		Assert.True(printers[0].IsDefault);
		Assert.Equal("cups:Raw", printers[1].Id);
		Assert.False(printers[1].IsDefault);
		Assert.Equal(Latiprimere.PrinterState.Stopped, printers[1].State);
	}

	[Fact]
	public async Task GivenQueueWithoutNameWhenGetPrintersThenSkipsIt()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			CupsPrinters =
			[
				new PrinterAttributesBuilder().WithName(null).Build(),
				new PrinterAttributesBuilder().WithName("Raw").Build(),
			]
		};
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrinterInfo printer = Assert.Single(printers);
		Assert.Equal("cups:Raw", printer.Id);
	}

	[Fact]
	public async Task GivenGetPrintersWhenRequestSentThenAsksServerForSummaryAttributes()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		SharpIpp.Models.Requests.CUPSGetPrintersRequest request = Assert.Single(client.CupsGetPrintersRequests);
		Assert.Equal(CupsPrintSystemTests.Endpoint.ServerUri, request.OperationAttributes?.PrinterUri?.Value);

		Assert.Equal
		(
			IppRequestedAttributes.PrinterSummaryAttributes,
			request.OperationAttributes?.RequestedAttributes?.Value
		);
	}

	[Theory]
	[InlineData("Office")]
	[InlineData("windows:Office")]
	[InlineData("cups:")]
	public async Task GivenIdNotFromCupsWhenFindPrinterThenReturnsNullWithoutAskingCups(string printerId)
	{
		// Arrange — printerID lacks the cups: prefix or a queue name (provided by InlineData above)
		FakeIppClient client = new FakeIppClient();
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
		Assert.Empty(client.GetPrinterAttributesRequests);
	}

	[Theory]
	[InlineData("cups:..")]
	[InlineData("cups:.")]
	public async Task GivenADotSegmentAsTheQueueNameWhenFindPrinterThenReturnsNullWithoutAskingCups(string printerId)
	{
		// Arrange — ".." would make the printer URI the server's, which CUPS takes as its default destination
		FakeIppClient client = new FakeIppClient();
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync(printerId, TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
		Assert.Empty(client.GetPrinterAttributesRequests);
	}

	[Fact]
	public async Task GivenUnknownQueueWhenFindPrinterThenReturnsNull()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync("cups:Missing", TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public async Task GivenKnownQueueWhenFindPrinterThenReturnsPrinterWithInfo()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient { DefaultPrinterName = "Office" };

		client.AddPrinter
		(
			new Uri("ipp://localhost:631/printers/Office"),
			new PrinterAttributesBuilder().WithName("Office").Build()
		);
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		IPrinter? printer = await printSystem.FindPrinterAsync("cups:Office", TestContext.Current.CancellationToken);

		// Assert
		Assert.NotNull(printer);
		Assert.Equal("cups:Office", printer.Info.Id);
		Assert.Equal("Office", printer.Info.Name);
		Assert.True(printer.Info.IsDefault);
	}

	[Fact]
	public async Task GivenServerFailureWhenFindPrinterThenPrintExceptionPropagates()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient
		{
			FailureToThrow = new PrintException("Unreachable.")
			{
				NativeErrorCode = (int)IppStatusCode.ServerErrorServiceUnavailable
			}
		};
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		Func<Task> findPrinter = () => printSystem.FindPrinterAsync
		(
			"cups:Office",
			TestContext.Current.CancellationToken
		);

		// Assert
		await Assert.ThrowsAsync<PrintException>(findPrinter);
	}

	[Fact]
	public async Task GivenInvalidEnvironmentServerWhenGetPrintersThenThrowsPrintExceptionSayingWhy()
	{
		// Arrange — what the factory builds for a CUPS_SERVER that isn't a server
		IppPrintContext context = new IppPrintContextBuilder().WithClient(new InvalidServerIppClient("printserver:notaport")).Build();
		using CupsPrintSystem printSystem = new CupsPrintSystem(context, CupsPrintSystemTests.Endpoint, isSupported: true, ownedClient: null);

		// Act
		Func<Task> getPrinters = () => printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert — a PrintException, so the printer catalog skips CUPS and still lists other print systems
		PrintException exception = await Assert.ThrowsAsync<PrintException>(getPrinters);
		Assert.Contains("CUPS_SERVER", exception.Message, StringComparison.Ordinal);
		Assert.Contains("printserver:notaport", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task GivenNoDefaultQueueWhenFindDefaultPrinterThenReturnsNull()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient { DefaultPrinterName = null };
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		IPrinter? printer = await printSystem.FindDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Null(printer);
	}

	[Fact]
	public async Task GivenDefaultQueueWhenFindDefaultPrinterThenReturnsIt()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient { DefaultPrinterName = "CUPS-PDF" };

		client.AddPrinter
		(
			new Uri("ipp://localhost:631/printers/CUPS-PDF"),
			new PrinterAttributesBuilder().WithName("CUPS-PDF").Build()
		);
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		IPrinter? printer = await printSystem.FindDefaultPrinterAsync(TestContext.Current.CancellationToken);

		// Assert — the default is asked for once, not again while looking the queue up
		Assert.Equal("cups:CUPS-PDF", printer?.Info.Id);
		Assert.True(printer?.Info.IsDefault);
		Assert.Equal(1, client.CupsGetDefaultRequestCount);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void GivenSupportFlagWhenConstructedThenReportsItAndIsNamedCups(bool isSupported)
	{
		// Arrange — whether CUPS can be used on this platform (provided by InlineData above)
		FakeIppClient client = new FakeIppClient();

		// Act
		using CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client, isSupported);

		// Assert
		Assert.Equal(isSupported, printSystem.IsSupported);
		Assert.Equal("CUPS", printSystem.Name);
	}

	[Fact]
	public void GivenOwnedClientWhenDisposedThenDisposesClient()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		CupsPrintSystem printSystem = CupsPrintSystemTests.CreatePrintSystem(client);

		// Act
		printSystem.Dispose();

		// Assert
		Assert.True(client.IsDisposed);
	}

	/// <summary>
	/// Creates a print system that uses the fake client and owns it.
	/// </summary>
	/// <param name="client">The fake IPP client.</param>
	/// <param name="isSupported">Whether the print system reports itself as supported.</param>
	/// <returns>The print system.</returns>
	private static CupsPrintSystem CreatePrintSystem(FakeIppClient client, bool isSupported = true)
	{
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).Build();

		return new CupsPrintSystem(context, CupsPrintSystemTests.Endpoint, isSupported, client);
	}
}
