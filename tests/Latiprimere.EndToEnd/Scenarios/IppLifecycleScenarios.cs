using Latiprimere.EndToEnd.Support;
using Latiprimere.FakeIppPrinter;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Following direct IPP jobs to the end, on the scripted printer, which moves them through their states on cue.
/// </summary>
public static class IppLifecycleScenarios
{
	/// <summary>
	/// A job that takes a while is seen processing, then completed.
	/// </summary>
	public static readonly Scenario StatesInOrder = new Scenario
	{
		Id = "ipp.lifecycle.states-in-order",
		Area = ScenarioArea.Lifecycle,
		Title = "A job the printer takes two seconds over is reported Processing, then Completed",
		Needs = TargetNeeds.None,
		RunAsync = IppLifecycleScenarios.StatesInOrderAsync
	};

	/// <summary>
	/// A job the printer aborts ends Aborted with the printer's reason.
	/// </summary>
	public static readonly Scenario AbortedWithReason = new Scenario
	{
		Id = "ipp.lifecycle.aborted-with-reason",
		Area = ScenarioArea.Lifecycle,
		Title = "A job the printer aborts ends Aborted, with the printer's job-state-message as the reason",
		Needs = TargetNeeds.None,
		RunAsync = IppLifecycleScenarios.AbortedWithReasonAsync
	};

	/// <summary>
	/// A stopped printer is listed as stopped, and its jobs wait until it starts again.
	/// </summary>
	public static readonly Scenario StoppedPrinter = new Scenario
	{
		Id = "ipp.lifecycle.stopped-printer",
		Area = ScenarioArea.Lifecycle,
		Title = "A printer out of paper is Stopped when looked up (listing doesn't ask it, so shows Unknown); its job waits Pending, then completes once the printer starts again",
		Needs = TargetNeeds.None,
		RunAsync = IppLifecycleScenarios.StoppedPrinterAsync
	};

	/// <summary>
	/// A job waiting on a stopped printer can be cancelled.
	/// </summary>
	public static readonly Scenario CancelWaitingJob = new Scenario
	{
		Id = "ipp.lifecycle.cancel-waiting-job",
		Area = ScenarioArea.Lifecycle,
		Title = "A job waiting on a stopped IPP printer is cancelled, ends Cancelled, and a second cancel reports it already finished",
		Needs = TargetNeeds.None,
		RunAsync = IppLifecycleScenarios.CancelWaitingJobAsync
	};

	/// <summary>
	/// Runs <see cref="StatesInOrder"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task StatesInOrderAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.ProcessingTime = TimeSpan.FromSeconds(2);
		IPrintJob job = await IppLifecycleScenarios.SubmitAsync(context, fake);

		IReadOnlyList<PrintJobStatus> changes = await JobFollower.FollowAsync(context, job);
		List<PrintJobState> states = changes.Select(change => change.State).ToList();
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.Check("the job is reported Processing", states.Contains(PrintJobState.Processing), string.Join(" > ", states));
		context.CheckEqual("then Completed, last", PrintJobState.Completed, states[^1]);
		context.Check("Processing comes before Completed", states.IndexOf(PrintJobState.Processing) < states.IndexOf(PrintJobState.Completed));
	}

	/// <summary>
	/// Runs <see cref="AbortedWithReason"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task AbortedWithReasonAsync(ScenarioContext context)
	{
		const string reason = "Paper jam in tray 2";
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.JobOutcome = FakeJobOutcome.Aborted;
		fake.Behaviour.JobOutcomeMessage = reason;
		IPrintJob job = await IppLifecycleScenarios.SubmitAsync(context, fake);

		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);

		context.CheckEqual("the job ends Aborted", PrintJobState.Aborted, status.State);
		context.Check("its message is the printer's reason", status.StateMessage?.Contains(reason, StringComparison.Ordinal) == true, status.StateMessage ?? "no message");
	}

	/// <summary>
	/// Runs <see cref="StoppedPrinter"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task StoppedPrinterAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.IsStopped = true;
		fake.Behaviour.StoppedReason = "media-empty-error";
		fake.Behaviour.StateMessage = "Out of paper";

		// Listing doesn't contact configured printers, so its state is unknown there (IppNetworkPrintSystem)
		await using CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		IReadOnlyList<PrinterInfo> printers = await fixture.Catalog.GetPrintersAsync(context.CancellationToken);
		PrinterInfo? listed = printers.FirstOrDefault(printer => printer.Id == FakePrinters.GetPrinterId(fake));
		context.Require("the printer is listed", listed is not null);
		context.CheckEqual("the listing gives its state as Unknown, without asking it", PrinterState.Unknown, listed!.State);

		IPrinter printer = await fixture.Catalog.GetPrinterAsync(listed.Id, context.CancellationToken);
		context.CheckEqual("looking it up gives its state as Stopped", PrinterState.Stopped, printer.Info.State);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		PrintJobStatus waiting = await job.GetStatusAsync(context.CancellationToken);
		context.CheckEqual("the job waits Pending", PrintJobState.Pending, waiting.State);

		// Paper is loaded
		fake.Behaviour.IsStopped = false;
		fake.Behaviour.StateMessage = null;
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.CheckEqual("once the printer starts again, the job completes", PrintJobState.Completed, status.State);
	}

	/// <summary>
	/// Runs <see cref="CancelWaitingJob"/>.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CancelWaitingJobAsync(ScenarioContext context)
	{
		await using FakePrinter fake = await FakePrinters.StartAsync(context);
		fake.Behaviour.IsStopped = true;
		IPrintJob job = await IppLifecycleScenarios.SubmitAsync(context, fake);
		PrintJobStatus waiting = await job.GetStatusAsync(context.CancellationToken);
		context.Require("the job waits on the stopped printer", !waiting.IsTerminal, waiting.State.ToString());

		bool isCancelled = await job.CancelAsync(context.CancellationToken);
		context.Check("cancelling it is accepted", isCancelled);
		PrintJobStatus status = await JobFollower.WaitUntilFinishedAsync(context, job);
		context.CheckEqual("the job ends Cancelled", PrintJobState.Canceled, status.State);

		bool isCancelledAgain = await job.CancelAsync(context.CancellationToken);
		await FakePrinters.SaveRequestsAsync(context, fake);
		context.Check("cancelling it again reports it had already finished", !isCancelledAgain);
	}

	/// <summary>
	/// Sends the three-page PDF to a fake printer through a catalog, which the job needs, so it is kept until the
	/// scenario ends.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <param name="fake">The printer.</param>
	/// <returns>The job.</returns>
	private static async Task<IPrintJob> SubmitAsync(ScenarioContext context, FakePrinter fake)
	{
		CatalogFixture fixture = FakePrinters.CreateCatalog(fake.PrinterUri);
		context.RegisterForDisposal(fixture);
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(FakePrinters.GetPrinterId(fake), context.CancellationToken);
		using FileStream document = TestDocuments.Open(TestDocuments.ThreePages);
		IPrintJob job = await printer.SubmitAsync(PrintContent.FromPdf(document), PrintOptions.Default, context.CancellationToken);
		context.Note($"Submitted job {job.Id}.");

		return job;
	}
}
