using System.Diagnostics;
using System.Globalization;

namespace Latiprimere.EndToEnd;

/// <summary>
/// Runs scenarios: skips those whose needs aren't met, limits how long each may take, and turns what happened into
/// a <see cref="ScenarioResult"/>.
/// </summary>
public static class ScenarioRunner
{
	/// <summary>
	/// The longest a scenario may run. Jobs on the virtual printers take seconds, but a printer that is slow or
	/// busy can make a job wait for a couple of minutes.
	/// </summary>
	public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Runs one scenario.
	/// </summary>
	/// <param name="scenario">The scenario.</param>
	/// <param name="targets">The test printers available.</param>
	/// <param name="reportDirectory">The run's report folder; the scenario's evidence goes in a folder named after
	/// its ID inside it.</param>
	/// <param name="output">Shows progress as it happens, or <see langword="null"/>.</param>
	/// <param name="cancellationToken">Cancels the run.</param>
	/// <returns>What happened. A scenario that throws, times out or makes no checks has failed.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="scenario"/> or <paramref name="targets"/> is
	/// <see langword="null"/>.</exception>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	public static Task<ScenarioResult> RunAsync
	(
		Scenario scenario,
		HarnessTargets targets,
		string reportDirectory,
		Action<string>? output,
		CancellationToken cancellationToken
	)
	{
		return ScenarioRunner.RunAsync(scenario, targets, reportDirectory, output, person: null, cancellationToken);
	}

	/// <summary>
	/// Runs one scenario, with a person to ask, as the console harness's manual mode does.
	/// </summary>
	/// <param name="scenario">The scenario.</param>
	/// <param name="targets">The test printers available.</param>
	/// <param name="reportDirectory">The run's report folder.</param>
	/// <param name="output">Shows progress as it happens, or <see langword="null"/>.</param>
	/// <param name="person">The person at the keyboard, or <see langword="null"/> for none.</param>
	/// <param name="cancellationToken">Cancels the run.</param>
	/// <returns>What happened.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	public static async Task<ScenarioResult> RunAsync
	(
		Scenario scenario,
		HarnessTargets targets,
		string reportDirectory,
		Action<string>? output,
		IPerson? person,
		CancellationToken cancellationToken
	)
	{
		ArgumentNullException.ThrowIfNull(scenario);
		ArgumentNullException.ThrowIfNull(targets);
		ArgumentException.ThrowIfNullOrWhiteSpace(reportDirectory);

		IReadOnlyList<string> missing = targets.DescribeMissing(scenario.Needs);

		if (missing.Count > 0)
		{
			string reason = string.Join("; ", missing);
			output?.Invoke($"SKIP {scenario.Id}: {reason}");

			return new ScenarioResult
			{
				Id = scenario.Id,
				Area = scenario.Area,
				Title = scenario.Title,
				Outcome = ScenarioOutcome.Skipped,
				SkipReason = reason
			};
		}

		output?.Invoke($"RUN  {scenario.Id}: {scenario.Title}");
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(scenario.Timeout ?? ScenarioRunner.Timeout);

		string evidenceDirectory = Path.Combine(reportDirectory, scenario.Id);
		ScenarioContext context = new ScenarioContext(targets, evidenceDirectory, output, person, timeout.Token);
		Stopwatch stopwatch = Stopwatch.StartNew();
		string? error = await ScenarioRunner.RunScenarioAsync(scenario, context, cancellationToken);
		stopwatch.Stop();

		if (error is null && context.SkipReason is not null)
		{
			output?.Invoke($"SKIP {scenario.Id}: {context.SkipReason}");

			return new ScenarioResult
			{
				Id = scenario.Id,
				Area = scenario.Area,
				Title = scenario.Title,
				Outcome = ScenarioOutcome.Skipped,
				SkipReason = context.SkipReason,
				Duration = stopwatch.Elapsed,
				Checks = context.Checks.ToList(),
				Log = context.Log.ToList()
			};
		}

		bool passed = error is null && context.Checks.Count > 0 && context.Checks.All(check => check.Passed);
		ScenarioOutcome outcome = passed ? ScenarioOutcome.Passed : ScenarioOutcome.Failed;
		output?.Invoke(string.Create(CultureInfo.InvariantCulture, $"{(passed ? "OK  " : "FAIL")} {scenario.Id} in {stopwatch.Elapsed.TotalSeconds:0.0} s") + (error is null ? string.Empty : $": {error}"));

		return new ScenarioResult
		{
			Id = scenario.Id,
			Area = scenario.Area,
			Title = scenario.Title,
			Outcome = outcome,
			Duration = stopwatch.Elapsed,
			Error = error,
			Checks = context.Checks.ToList(),
			Evidence = context.Evidence.ToList(),
			Log = context.Log.ToList()
		};
	}

	/// <summary>
	/// Runs a scenario's code, turning whatever ends it early into an error message.
	/// </summary>
	/// <param name="scenario">The scenario.</param>
	/// <param name="context">Its context.</param>
	/// <param name="cancellationToken">Cancels the whole run, as opposed to the scenario's own time limit.</param>
	/// <returns>The error, or <see langword="null"/> if the scenario ran to its end, stopped at a failed required
	/// check, or skipped itself.</returns>
	private static async Task<string?> RunScenarioAsync(Scenario scenario, ScenarioContext context, CancellationToken cancellationToken)
	{
		try
		{
			await scenario.RunAsync(context);

			return null;
		}
		catch (ScenarioCheckFailedException)
		{
			// The failed check is already recorded
			return null;
		}
		catch (ScenarioSkippedException)
		{
			// The reason is already recorded
			return null;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return $"Timed out after {(scenario.Timeout ?? ScenarioRunner.Timeout).TotalMinutes} minutes.";
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// Anything a scenario throws is a finding to report, not a reason to stop the run
			return exception.ToString();
		}
		finally
		{
			await context.DisposeRegisteredAsync();
		}
	}
}
