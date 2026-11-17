using Latiprimere.EndToEnd.Reporting;
using Xunit;

namespace Latiprimere.EndToEnd.Tests.Fixtures;

/// <summary>
/// One harness run per test process: finds the targets once, gives every scenario the same report folder, and
/// writes the report when the process's tests are done.
/// </summary>
/// <remarks>
/// Does nothing unless <see cref="HarnessEnvironment.EnabledVariable"/> is set, so a plain <c>dotnet test</c> leaves
/// no report behind.
/// </remarks>
public sealed class HarnessRunFixture : IAsyncLifetime
{
	private readonly List<ScenarioResult> results = new List<ScenarioResult>(); // In the order the scenarios finished
	private readonly Lock resultsLock = new Lock();                             // Guards results
	private readonly DateTimeOffset startedAt = DateTimeOffset.Now;             // When the process's run started
	private HarnessTargets? targets;                                            // Found when the run starts
	private string? reportDirectory;                                            // Created when the run starts

	/// <summary>
	/// Gets the targets found.
	/// </summary>
	/// <exception cref="InvalidOperationException">The scenarios are switched off.</exception>
	public HarnessTargets Targets
	{
		get
		{
			return this.targets ?? throw new InvalidOperationException($"{HarnessEnvironment.EnabledVariable} is not set.");
		}
	}

	/// <summary>
	/// Gets this run's report folder.
	/// </summary>
	/// <exception cref="InvalidOperationException">The scenarios are switched off.</exception>
	public string ReportDirectory
	{
		get
		{
			return this.reportDirectory ?? throw new InvalidOperationException($"{HarnessEnvironment.EnabledVariable} is not set.");
		}
	}

	/// <summary>
	/// Finds the targets and creates the report folder, when the scenarios are switched on.
	/// </summary>
	/// <returns>A task that completes when the run is ready.</returns>
	public async ValueTask InitializeAsync()
	{
		if (!HarnessEnvironment.IsEnabled)
		{
			return;
		}

		this.targets = await HarnessTargets.DetectAsync(hasPerson: false, CancellationToken.None);
		this.reportDirectory = HarnessEnvironment.CreateReportDirectory("xunit");
	}

	/// <summary>
	/// Keeps a scenario's result for the report.
	/// </summary>
	/// <param name="result">The result.</param>
	public void Add(ScenarioResult result)
	{
		lock (this.resultsLock)
		{
			this.results.Add(result);
		}
	}

	/// <summary>
	/// Writes the report, if any scenario ran.
	/// </summary>
	/// <returns>A task that completes when the report is written.</returns>
	public async ValueTask DisposeAsync()
	{
		if (this.targets is null || this.reportDirectory is null)
		{
			return;
		}

		List<ScenarioResult> finished;

		lock (this.resultsLock)
		{
			finished = this.results.ToList();
		}

		HarnessReport report = new HarnessReport
		{
			Host = "xunit",
			OperatingSystem = HarnessEnvironment.OperatingSystemName,
			Framework = HarnessEnvironment.FrameworkName,
			StartedAt = this.startedAt,
			FinishedAt = DateTimeOffset.Now,
			Targets = this.targets.Describe(),
			Results = finished
		};

		await ReportWriter.WriteAsync(report, this.reportDirectory, CancellationToken.None);
	}
}
