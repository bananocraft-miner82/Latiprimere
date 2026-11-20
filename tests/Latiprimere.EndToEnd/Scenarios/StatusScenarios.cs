using System.Diagnostics;
using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Printers' status, and following printers and jobs as they change (ADR 0049).
/// </summary>
public static class StatusScenarios
{
	/// <summary>
	/// A paused CUPS queue reports itself stopped, with CUPS's reason and message.
	/// </summary>
	public static readonly Scenario CupsPaused = new Scenario
	{
		Id = "cups.printer-status.paused",
		Area = ScenarioArea.Lifecycle,
		Title = "CUPS-PDF reports itself idle and accepting jobs, and the paused queue stopped with the reason paused and CUPS's message for it",
		Needs = TargetNeeds.CupsServer | TargetNeeds.CupsContainer,
		RunAsync = StatusScenarios.CupsPausedAsync
	};

	/// <summary>
	/// A paused Windows queue reports itself stopped with the IPP reason the spooler's flag matches.
	/// </summary>
	public static readonly Scenario WindowsPaused = new Scenario
	{
		Id = "windows.printer-status.paused",
		Area = ScenarioArea.Lifecycle,
		Title = "The paused Windows test printer reports itself stopped with the reason paused, and still accepting jobs, which wait in its queue",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = StatusScenarios.WindowsPausedAsync
	};

	/// <summary>
	/// A Windows job is followed by the spooler's change notifications, not by polling.
	/// </summary>
	public static readonly Scenario WindowsJobNotified = new Scenario
	{
		Id = "windows.jobs.watch-by-notification",
		Area = ScenarioArea.Lifecycle,
		Title = "A raw job waiting in the paused Windows test printer, followed with a poll interval of an hour, reports canceled within seconds of being cancelled, as the spooler signals the change",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = StatusScenarios.WindowsJobNotifiedAsync
	};

	/// <summary>
	/// A network printer's status is read over IPP and followed by polling.
	/// </summary>
	public static readonly Scenario IppPolled = new Scenario
	{
		Id = "ipp.printer-status.polled",
		Area = ScenarioArea.Lifecycle,
		Title = "A printer with ippeveprinter's profile reports itself idle and accepting jobs, and following it reports that once, polling",
		Needs = TargetNeeds.None,
		RunAsync = StatusScenarios.IppPolledAsync
	};

	/// <summary>
	/// Runs <see cref="CupsPaused"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CupsPausedAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter pdf = await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsPdf, context.CancellationToken);
		IPrinter paused = await fixture.Catalog.GetPrinterAsync(TestPrinters.CupsPaused, context.CancellationToken);

		PrinterStatus pdfStatus = await pdf.GetStatusAsync(context.CancellationToken);
		PrinterStatus pausedStatus = await paused.GetStatusAsync(context.CancellationToken);
		context.Note($"CUPS-PDF: {StatusScenarios.Describe(pdfStatus)}");
		context.Note($"Paused: {StatusScenarios.Describe(pausedStatus)}");

		context.CheckEqual("CUPS-PDF is idle and accepting jobs", (PrinterState.Idle, (bool?)true), (pdfStatus.State, pdfStatus.IsAcceptingJobs));
		context.CheckEqual("the paused queue is stopped", PrinterState.Stopped, pausedStatus.State);
		context.Check("its reasons include paused", pausedStatus.StateReasons.Contains("paused"), string.Join(", ", pausedStatus.StateReasons));
		context.CheckEqual("its message is the one it was paused with", "Paused for testing", pausedStatus.StateMessage);
	}

	/// <summary>
	/// Runs <see cref="WindowsPaused"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsPausedAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.WindowsPaused, context.CancellationToken);

		PrinterStatus status = await printer.GetStatusAsync(context.CancellationToken);
		context.Note(StatusScenarios.Describe(status));

		context.CheckEqual("it is stopped", PrinterState.Stopped, status.State);
		context.Check("its reasons include paused", status.StateReasons.Contains("paused"), string.Join(", ", status.StateReasons));
		context.CheckEqual("it accepts jobs, which wait in its queue", true, status.IsAcceptingJobs);
	}

	/// <summary>
	/// Runs <see cref="WindowsJobNotified"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task WindowsJobNotifiedAsync(ScenarioContext context)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(TestPrinters.WindowsPaused, context.CancellationToken);
		using MemoryStream data = new MemoryStream(RawData.Create(StatusScenarios.WindowsJobNotified.Id));
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, context.CancellationToken);
		context.Note($"Submitted job {job.Id}.");

		using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
		limit.CancelAfter(TimeSpan.FromSeconds(60));
		List<PrintJobStatus> statuses = [];
		Stopwatch sinceCancel = new Stopwatch();

		try
		{
			// An hour between polls: the job can only be seen to end promptly by the spooler's signal
			await foreach (PrintJobStatus status in job.WatchStatusAsync(TimeSpan.FromHours(1), limit.Token))
			{
				statuses.Add(status);
				context.Note($"{status.State} {status.StateMessage}");

				if (statuses.Count == 1)
				{
					sinceCancel.Start();
					await job.CancelAsync(context.CancellationToken);
				}
			}
		}
		catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
		{
			// Never leave a job in the test printer's queue
			await job.CancelAsync(CancellationToken.None);
		}

		sinceCancel.Stop();
		context.Require("the watch reports the waiting job, then its end", statuses.Count >= 2, string.Join(", ", statuses.Select(status => status.State)));
		context.CheckEqual("it ends canceled", PrintJobState.Canceled, statuses[^1].State);
		context.Check("within seconds of the cancel, not an hour", sinceCancel.Elapsed < TimeSpan.FromSeconds(10), $"{sinceCancel.Elapsed.TotalSeconds:0.0} s");
	}

	/// <summary>
	/// Runs <see cref="IppPolled"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task IppPolledAsync(ScenarioContext context)
	{
		FakePrinterProfile profile = await FakePrinterProfile.LoadIppeveprinterAsync(context.CancellationToken);

		await using FakePrinter fake = await FakePrinters.StartAsync(context, new FakePrinterOptions { Profile = profile });
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);

		PrinterStatus status = await printer.GetStatusAsync(context.CancellationToken);
		context.Note(StatusScenarios.Describe(status));
		context.CheckEqual("it is idle and accepting jobs", (PrinterState.Idle, (bool?)true), (status.State, status.IsAcceptingJobs));

		using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
		IAsyncEnumerator<PrinterStatus> watch = printer.WatchStatusAsync(TimeSpan.FromMilliseconds(100), stop.Token).GetAsyncEnumerator(stop.Token);
		bool first = await watch.MoveNextAsync();
		PrinterState firstState = watch.Current.State;

		// Nothing changes, so nothing more is reported while it is polled a few times
		Task<bool> next = watch.MoveNextAsync().AsTask();
		bool more = await Task.WhenAny(next, Task.Delay(TimeSpan.FromMilliseconds(600), context.CancellationToken)) == next;
		await stop.CancelAsync();

		try
		{
			await next;
		}
		catch (OperationCanceledException)
		{
			// Ended by the cancel, as a printer watch is
		}

		await watch.DisposeAsync();
		context.CheckEqual("following it reports its status first", (true, PrinterState.Idle), (first, firstState));
		context.Check("and nothing again while it doesn't change", !more);
	}

	/// <summary>
	/// Describes a status for the report.
	/// </summary>
	/// <param name="status">The status.</param>
	/// <returns>Its state, message, reasons and whether it accepts jobs.</returns>
	private static string Describe(PrinterStatus status)
	{
		return $"{status.State}, \"{status.StateMessage}\", reasons [{string.Join(", ", status.StateReasons)}], accepting {status.IsAcceptingJobs}";
	}
}
