using Latiprimere.EndToEnd.Tests.Fixtures;
using Xunit;

[assembly: AssemblyFixture(typeof(HarnessRunFixture))]

namespace Latiprimere.EndToEnd.Tests;

/// <summary>
/// Runs each end-to-end scenario as a test case. A scenario skips when its targets are missing and fails when any
/// of its checks does; the failure message lists the failed checks and where the evidence is.
/// </summary>
[Trait("Category", "EndToEnd")]
[Trait("OS", "Any")]
public sealed class ScenarioTests
{
	private readonly HarnessRunFixture run; // The process's harness run, which writes the report

	/// <summary>
	/// Initialises a new instance of the <see cref="ScenarioTests"/> class.
	/// </summary>
	/// <param name="run">The process's harness run.</param>
	public ScenarioTests(HarnessRunFixture run)
	{
		this.run = run;
	}

	/// <summary>
	/// Gets every scenario's ID.
	/// </summary>
	public static TheoryData<string> ScenarioIds
	{
		get
		{
			return new TheoryData<string>(AllScenarios.List.Select(scenario => scenario.Id));
		}
	}

	[Theory]
	[MemberData(nameof(ScenarioTests.ScenarioIds))]
	public async Task GivenScenarioWhenRunThenEveryCheckPasses(string scenarioId)
	{
		// Arrange
		Assert.SkipUnless
		(
			HarnessEnvironment.IsEnabled,
			$"Set {HarnessEnvironment.EnabledVariable}=1 to run the end-to-end scenarios, or use tools/test-harness (ADR 0029)."
		);

		Scenario scenario = AllScenarios.Find(scenarioId);
		ITestOutputHelper? output = TestContext.Current.TestOutputHelper;

		// Act
		ScenarioResult result = await ScenarioRunner.RunAsync
		(
			scenario,
			this.run.Targets,
			this.run.ReportDirectory,
			line => output?.WriteLine(line),
			TestContext.Current.CancellationToken
		);

		this.run.Add(result);

		// Assert
		Assert.SkipWhen(result.Outcome == ScenarioOutcome.Skipped, $"Skipped: {result.SkipReason}");
		Assert.True(result.Outcome == ScenarioOutcome.Passed, result.DescribeFailure());
	}
}
