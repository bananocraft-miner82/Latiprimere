using System.Diagnostics;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Runs the sample programs in <c>samples/</c> as their own processes, from the same build as the harness.
/// </summary>
/// <remarks>
/// <c>Latiprimere.EndToEnd</c> builds them without referencing them, so they match the harness's configuration and
/// target framework. Each run gets <c>CUPS_SERVER</c> set to the CUPS test server, or removed when there is none, so
/// a sample never reaches a CUPS server the harness doesn't know about.
/// </remarks>
public static class Samples
{
	/// <summary>
	/// The documentation's examples (<c>samples/Latiprimere.Examples</c>).
	/// </summary>
	public const string Examples = "Latiprimere.Examples";

	/// <summary>
	/// The command-line sample (<c>samples/Latiprimere.Cli</c>).
	/// </summary>
	public const string Cli = "Latiprimere.Cli";

	/// <summary>
	/// How long a sample may run unless a scenario says otherwise: listing includes a few seconds of mDNS discovery.
	/// </summary>
	public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

	/// <summary>
	/// Gets the path of a test document as the harness's build has it, to pass to a sample.
	/// </summary>
	/// <param name="fileName">The document's file name, such as <c>three-pages.pdf</c>.</param>
	/// <returns>The full path.</returns>
	public static string GetDocumentPath(string fileName)
	{
		return Path.Combine(AppContext.BaseDirectory, "Data", fileName);
	}

	/// <summary>
	/// Runs a sample to its end, and keeps its output as evidence.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="sample">The sample, <see cref="Examples"/> or <see cref="Cli"/>.</param>
	/// <param name="arguments">Its arguments.</param>
	/// <param name="environment">Variables to set (or, with a <see langword="null"/> value, remove) beyond
	/// <c>CUPS_SERVER</c>.</param>
	/// <param name="timeout">The longest it may run, or <see langword="null"/> for <see cref="DefaultTimeout"/>.</param>
	/// <returns>How it ended.</returns>
	public static async Task<SampleRun> RunAsync
	(
		ScenarioContext context,
		string sample,
		IReadOnlyList<string> arguments,
		IReadOnlyDictionary<string, string?>? environment = null,
		TimeSpan? timeout = null
	)
	{
		ArgumentNullException.ThrowIfNull(context);

		await using RunningSample running = Samples.Start(context, sample, arguments, environment);
		SampleRun run = await running.WaitForExitAsync(timeout ?? Samples.DefaultTimeout, context.CancellationToken);
		await Samples.SaveOutputAsync(context, sample, arguments, run);

		return run;
	}

	/// <summary>
	/// Starts a sample that keeps running, such as the web service. Dispose the result to stop it.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="sample">The sample.</param>
	/// <param name="arguments">Its arguments.</param>
	/// <param name="environment">Variables to set or remove beyond <c>CUPS_SERVER</c>.</param>
	/// <returns>The running sample.</returns>
	/// <exception cref="InvalidOperationException">The sample hasn't been built.</exception>
	public static RunningSample Start
	(
		ScenarioContext context,
		string sample,
		IReadOnlyList<string> arguments,
		IReadOnlyDictionary<string, string?>? environment = null
	)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(arguments);

		ProcessStartInfo startInfo = new ProcessStartInfo(Samples.GetExecutable(sample))
		{
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		foreach (string argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		// Only the CUPS test server, never one the machine happens to be set up for
		startInfo.Environment.Remove("CUPS_SERVER");

		if (context.Targets.CupsServer is not null)
		{
			startInfo.Environment["CUPS_SERVER"] = context.Targets.CupsServer;
		}

		foreach (KeyValuePair<string, string?> variable in environment ?? new Dictionary<string, string?>())
		{
			if (variable.Value is null)
			{
				startInfo.Environment.Remove(variable.Key);
			}
			else
			{
				startInfo.Environment[variable.Key] = variable.Value;
			}
		}

		context.Note($"Running {sample} {string.Join(" ", arguments.Select(Samples.Quote))}");

		return new RunningSample(startInfo);
	}

	/// <summary>
	/// Keeps a run's output as evidence.
	/// </summary>
	/// <param name="context">The scenario.</param>
	/// <param name="sample">The sample.</param>
	/// <param name="arguments">Its arguments.</param>
	/// <param name="run">How it ended.</param>
	/// <returns>A task that completes when the file is written.</returns>
	public static Task SaveOutputAsync(ScenarioContext context, string sample, IReadOnlyList<string> arguments, SampleRun run)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(arguments);
		ArgumentNullException.ThrowIfNull(run);

		string command = arguments.Count > 0 ? arguments[0] : "usage";
		string fileName = $"{sample}-{command}-{context.Evidence.Count + 1}.txt";
		string[] lines = [$"> {sample} {string.Join(" ", arguments.Select(Samples.Quote))}", $"Exit code: {run.ExitCode}", string.Empty, run.Output];

		return context.SaveEvidenceAsync(fileName, lines, $"{sample} {command}: output");
	}

	/// <summary>
	/// Finds a sample's program for the .NET the harness runs on, such as
	/// <c>samples/Latiprimere.Cli/bin/Debug/net10.0/Latiprimere.Cli.exe</c>: from the harness's own configuration when it
	/// runs from a build, otherwise (published with Native AOT, say) whichever configuration was built last.
	/// </summary>
	/// <param name="sample">The sample.</param>
	/// <returns>The program's path.</returns>
	/// <exception cref="InvalidOperationException">The harness wasn't run from inside the repository, or the sample
	/// hasn't been built for this .NET.</exception>
	private static string GetExecutable(string sample)
	{
		string repository = HarnessEnvironment.FindRepositoryRoot()
			?? throw new InvalidOperationException("The samples can only be run from inside the repository.");

		string framework = HarnessEnvironment.FrameworkName;
		string program = OperatingSystem.IsWindows() ? sample + ".exe" : sample;
		string binDirectory = Path.Combine(repository, "samples", sample, "bin");

		// A build runs from bin/<configuration>/<framework>/ (or bin/packaged/<configuration>/<framework>/)
		DirectoryInfo ownFrameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		string? ownConfiguration = ownFrameworkDirectory.Name == framework ? ownFrameworkDirectory.Parent?.Name : null;
		string? preferred = ownConfiguration is null ? null : Path.Combine(binDirectory, ownConfiguration, framework, program);

		if (preferred is not null && File.Exists(preferred))
		{
			return preferred;
		}

		string? newest = new[] { "Release", "Debug" }
			.Select(configuration => Path.Combine(binDirectory, configuration, framework, program))
			.Where(File.Exists)
			.OrderByDescending(File.GetLastWriteTimeUtc)
			.FirstOrDefault();

		return newest ?? throw new InvalidOperationException($"{sample} isn't built for {framework}: build the solution, or samples/{sample}, first.");
	}

	/// <summary>
	/// Quotes an argument with spaces, for showing the command line.
	/// </summary>
	/// <param name="argument">The argument.</param>
	/// <returns>The argument, quoted if it has a space.</returns>
	private static string Quote(string argument)
	{
		return argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
	}
}
