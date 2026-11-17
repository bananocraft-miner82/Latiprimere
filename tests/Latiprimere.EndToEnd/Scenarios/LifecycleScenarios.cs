using Latiprimere.EndToEnd.Support;
using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd.Scenarios;

/// <summary>
/// Following jobs to the end, and cancelling them.
/// </summary>
public static class LifecycleScenarios
{
	/// <summary>
	/// A job waiting on the paused CUPS queue can be cancelled.
	/// </summary>
	public static readonly Scenario CupsCancelWaitingJob = new Scenario
	{
		Id = "cups.lifecycle.cancel-waiting-job",
		Area = ScenarioArea.Lifecycle,
		Title = "A job waiting on the paused CUPS queue is cancelled, ends Cancelled, and a second cancel reports it already finished",
		Needs = TargetNeeds.CupsServer,
		RunAsync = context => LifecycleScenarios.CancelWaitingJobAsync(context, TestPrinters.CupsPaused)
	};

	/// <summary>
	/// A job waiting on the paused Windows test printer can be cancelled.
	/// </summary>
	public static readonly Scenario WindowsCancelWaitingJob = new Scenario
	{
		Id = "windows.lifecycle.cancel-waiting-job",
		Area = ScenarioArea.Lifecycle,
		Title = "A job waiting on the paused Windows test printer is cancelled, ends Cancelled, and a second cancel reports it already finished",
		Needs = TargetNeeds.WindowsTestPrinters,
		RunAsync = context => LifecycleScenarios.CancelWaitingJobAsync(context, TestPrinters.WindowsPaused)
	};

	/// <summary>
	/// Sends raw data to a paused printer, cancels the job while it waits, and follows it to the end.
	/// </summary>
	/// <param name="context">The scenario's context.</param>
	/// <param name="printerId">The paused printer.</param>
	/// <returns>A task that completes when the scenario has run.</returns>
	private static async Task CancelWaitingJobAsync(ScenarioContext context, string printerId)
	{
		await using CatalogFixture fixture = context.CreateCatalog();
		IPrinter printer = await fixture.Catalog.GetPrinterAsync(printerId, context.CancellationToken);
		using MemoryStream data = new MemoryStream(RawData.Create(printerId));

		IPrintJob job = await printer.SubmitAsync(PrintContent.FromRaw(data), PrintOptions.Default, context.CancellationToken);
		context.Note($"Submitted job {job.Id}.");
		PrintJobStatus waiting = await job.GetStatusAsync(context.CancellationToken);
		context.Require("the job waits on the paused printer", !waiting.IsTerminal, waiting.State.ToString());

		bool isCancelled = await job.CancelAsync(context.CancellationToken);
		context.Check("cancelling it is accepted", isCancelled);
		IReadOnlyList<PrintJobStatus> changes = await JobFollower.FollowAsync(context, job);
		context.CheckEqual("the job ends cancelled", PrintJobState.Cancelled, changes[^1].State);

		bool isCancelledAgain = await job.CancelAsync(context.CancellationToken);
		context.Check("cancelling it again reports it had already finished", !isCancelledAgain);
	}
}
