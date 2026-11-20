using Latiprimere.Ipp.Testing.Builders;
using Latiprimere.Ipp.Testing.Fakes;
using SharpIpp.Protocol.Models;
using Xunit;
using IppPrinterState = SharpIpp.Protocol.Models.PrinterState;

namespace Latiprimere.Ipp.Tests;

/// <summary>
/// A printer's status read over IPP, and followed by polling it (ADR 0049).
/// </summary>
public sealed class IppPrinterStatusTests
{
	private static readonly Uri PrinterUri = new Uri("ipp://192.168.1.20:631/ipp/print"); // Printer under test

	[Fact]
	public async Task GivenAStoppedPrinterWhenStatusIsReadThenItsStateMessageReasonsAndAcceptingAreGiven()
	{
		// Arrange — "none" is left out, and reasons are given once, in lower case
		FakeIppClient client = new FakeIppClient();

		client.AddPrinter(IppPrinterStatusTests.PrinterUri, new PrinterDescriptionAttributes
		{
			PrinterState           = new IppValue<IppPrinterState>(IppPrinterState.Stopped),
			PrinterStateMessage    = new StringWithLanguage("Load paper in tray 1"),
			PrinterStateReasons    = new IppValue<PrinterStateReason[]>([new PrinterStateReason("media-empty-error"), new PrinterStateReason("NONE"), new PrinterStateReason("paused"), new PrinterStateReason("media-empty-error")]),
			PrinterIsAcceptingJobs = new IppValue<bool>(false)
		});

		IppPrinter printer = IppPrinterStatusTests.CreatePrinter(client);

		// Act
		PrinterStatus status = await printer.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert — only the state attributes are asked for
		Assert.Equal(("ipp:x", PrinterState.Stopped, "Load paper in tray 1", false), (status.PrinterId, status.State, status.StateMessage, status.IsAcceptingJobs));
		Assert.Equal(["media-empty-error", "paused"], status.StateReasons);
		string[]? requested = Assert.Single(client.GetPrinterAttributesRequests).OperationAttributes?.RequestedAttributes?.Value;
		Assert.Equal(["printer-state", "printer-state-message", "printer-state-reasons", "printer-is-accepting-jobs"], Assert.IsType<string[]>(requested));
	}

	[Fact]
	public async Task GivenAPrinterGivingOnlyItsStateWhenStatusIsReadThenTheRestIsNotGiven()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrinterStatusTests.PrinterUri, new PrinterAttributesBuilder().WithState(IppPrinterState.Idle).Build());
		IppPrinter printer = IppPrinterStatusTests.CreatePrinter(client);

		// Act
		PrinterStatus status = await printer.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal((PrinterState.Idle, null), (status.State, status.StateMessage));
		Assert.Empty(status.StateReasons);
	}

	[Fact]
	public async Task GivenAPrinterThatIsGoneWhenStatusIsReadThenThrowsPrintException()
	{
		// Arrange — a client with no printer at that URI
		IppPrinter printer = IppPrinterStatusTests.CreatePrinter(new FakeIppClient());

		// Act
		Func<Task> read = () => printer.GetStatusAsync(TestContext.Current.CancellationToken);

		// Assert
		await Assert.ThrowsAsync<PrintException>(read);
	}

	[Fact]
	public async Task GivenAnIppPrinterWhenWatchedThenItIsPolledAndReportsItsStatus()
	{
		// Arrange
		FakeIppClient client = new FakeIppClient();
		client.AddPrinter(IppPrinterStatusTests.PrinterUri, new PrinterAttributesBuilder().WithState(IppPrinterState.Processing).Build());
		IPrinter printer = IppPrinterStatusTests.CreatePrinter(client);
		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

		// Act
		IAsyncEnumerator<PrinterStatus> watch = printer.WatchStatusAsync(TimeSpan.FromMilliseconds(5), stop.Token).GetAsyncEnumerator(stop.Token);
		bool moved = await watch.MoveNextAsync();
		PrinterState state = watch.Current.State;
		await stop.CancelAsync();
		await watch.DisposeAsync();

		// Assert
		Assert.True(moved);
		Assert.Equal(PrinterState.Processing, state);
	}

	/// <summary>
	/// Creates the printer under test.
	/// </summary>
	/// <param name="client">The client.</param>
	/// <returns>The printer.</returns>
	private static IppPrinter CreatePrinter(FakeIppClient client)
	{
		IppPrintContext context = new IppPrintContextBuilder().WithClient(client).WithDocumentFormats(IppDocumentFormats.Standard).Build();

		return new IppPrinter(context, IppPrinterStatusTests.PrinterUri, new PrinterInfo { Id = "ipp:x", Name = "x" }, ["application/pdf"]);
	}
}
