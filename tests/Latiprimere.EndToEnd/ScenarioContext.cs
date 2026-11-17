using Latiprimere.Testing.Fixtures;

namespace Latiprimere.EndToEnd;

/// <summary>
/// What a running scenario works with: the targets, a cancellation token, and somewhere to record its checks,
/// evidence and progress.
/// </summary>
public sealed class ScenarioContext
{
	private readonly List<ScenarioCheck> checks = new List<ScenarioCheck>();       // In the order they were made
	private readonly List<ScenarioEvidence> evidence = new List<ScenarioEvidence>(); // Files kept for the report
	private readonly List<string> log = new List<string>();                         // Progress notes
	private readonly Action<string>? output;                                         // Shows progress as it happens
	private readonly List<IAsyncDisposable> disposables = new List<IAsyncDisposable>(); // Kept until the scenario ends
	private readonly IPerson? person;                                                // Manual mode's person, or null

	/// <summary>
	/// Initialises a new instance of the <see cref="ScenarioContext"/> class.
	/// </summary>
	/// <param name="targets">The test printers available.</param>
	/// <param name="evidenceDirectory">The folder for this scenario's evidence, created when first used.</param>
	/// <param name="output">Shows each check and note as it happens, or <see langword="null"/>.</param>
	/// <param name="person">The person at the keyboard, in manual mode only; otherwise <see langword="null"/>.</param>
	/// <param name="cancellationToken">Cancels the scenario, including when it runs out of time.</param>
	internal ScenarioContext(HarnessTargets targets, string evidenceDirectory, Action<string>? output, IPerson? person, CancellationToken cancellationToken)
	{
		this.Targets = targets;
		this.EvidenceDirectory = evidenceDirectory;
		this.output = output;
		this.person = person;
		this.CancellationToken = cancellationToken;
	}

	/// <summary>
	/// Gets the person at the keyboard, for scenarios that need <see cref="TargetNeeds.Person"/>.
	/// </summary>
	/// <exception cref="InvalidOperationException">There is no person: the scenario isn't running in manual mode.</exception>
	public IPerson Person
	{
		get
		{
			return this.person ?? throw new InvalidOperationException("This scenario needs a person, so runs only in the console harness's manual mode.");
		}
	}

	/// <summary>
	/// Gets the test printers available.
	/// </summary>
	public HarnessTargets Targets { get; }

	/// <summary>
	/// Gets the token that cancels the scenario, including when it runs out of time.
	/// </summary>
	public CancellationToken CancellationToken { get; }

	/// <summary>
	/// Gets the folder for this scenario's evidence. It exists once <see cref="GetEvidencePath"/> has been called.
	/// </summary>
	public string EvidenceDirectory { get; }

	/// <summary>
	/// Gets why the scenario skipped itself, or <see langword="null"/> if it didn't.
	/// </summary>
	public string? SkipReason { get; private set; }

	/// <summary>
	/// Gets the checks made so far.
	/// </summary>
	public IReadOnlyList<ScenarioCheck> Checks
	{
		get
		{
			return this.checks;
		}
	}

	/// <summary>
	/// Gets the evidence kept so far.
	/// </summary>
	public IReadOnlyList<ScenarioEvidence> Evidence
	{
		get
		{
			return this.evidence;
		}
	}

	/// <summary>
	/// Gets the progress notes written so far.
	/// </summary>
	public IReadOnlyList<string> Log
	{
		get
		{
			return this.log;
		}
	}

	/// <summary>
	/// Records a check and carries on, whether or not it passed.
	/// </summary>
	/// <param name="name">What was checked, as a statement that should be true.</param>
	/// <param name="passed">Whether it was true.</param>
	/// <param name="detail">What was found.</param>
	/// <returns><paramref name="passed"/>.</returns>
	/// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/>, empty or whitespace.</exception>
	public bool Check(string name, bool passed, string? detail = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		this.checks.Add(new ScenarioCheck(name, passed, detail));
		string line = (passed ? "PASS " : "FAIL ") + name + (detail is null ? string.Empty : $" ({detail})");
		this.output?.Invoke("  " + line);

		return passed;
	}

	/// <summary>
	/// Records a check that a value is what was expected, and carries on.
	/// </summary>
	/// <typeparam name="T">The value's type.</typeparam>
	/// <param name="name">What was checked.</param>
	/// <param name="expected">The value expected.</param>
	/// <param name="actual">The value found.</param>
	/// <returns><see langword="true"/> if they are equal.</returns>
	public bool CheckEqual<T>(string name, T expected, T actual)
	{
		bool passed = EqualityComparer<T>.Default.Equals(expected, actual);

		return this.Check(name, passed, passed ? $"{actual}" : $"expected {expected}, found {actual}");
	}

	/// <summary>
	/// Records a check, and ends the scenario as failed if it didn't pass, for checks the rest depends on.
	/// </summary>
	/// <param name="name">What was checked, as a statement that should be true.</param>
	/// <param name="passed">Whether it was true.</param>
	/// <param name="detail">What was found.</param>
	/// <exception cref="ScenarioCheckFailedException"><paramref name="passed"/> is <see langword="false"/>.</exception>
	public void Require(string name, bool passed, string? detail = null)
	{
		if (!this.Check(name, passed, detail))
		{
			throw new ScenarioCheckFailedException(name);
		}
	}

	/// <summary>
	/// Records a check that a value is what was expected, and ends the scenario as failed if it isn't.
	/// </summary>
	/// <typeparam name="T">The value's type.</typeparam>
	/// <param name="name">What was checked.</param>
	/// <param name="expected">The value expected.</param>
	/// <param name="actual">The value found.</param>
	/// <exception cref="ScenarioCheckFailedException">The values differ.</exception>
	public void RequireEqual<T>(string name, T expected, T actual)
	{
		if (!this.CheckEqual(name, expected, actual))
		{
			throw new ScenarioCheckFailedException(name);
		}
	}

	/// <summary>
	/// Ends the scenario as skipped, for something only the scenario can find out, such as that this machine's
	/// default printer isn't a test printer. Use <see cref="Scenario.Needs"/> for anything the targets say.
	/// </summary>
	/// <param name="reason">Why it can't run here.</param>
	/// <exception cref="ScenarioSkippedException">Always.</exception>
	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	public void Skip(string reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		this.SkipReason = reason;

		throw new ScenarioSkippedException(reason);
	}

	/// <summary>
	/// Asks the person whether something is so, and records the answer as a check: yes passes, no fails with what
	/// they saw, and no answer is noted but not counted.
	/// </summary>
	/// <param name="name">The check, as a statement that should be true, such as "the page is in grey only".</param>
	/// <param name="question">The question to ask, such as "Is the page in grey only?".</param>
	/// <returns>The answer.</returns>
	/// <exception cref="InvalidOperationException">There is no person.</exception>
	public async Task<PersonAnswer> AskPersonAsync(string name, string question)
	{
		PersonAnswer answer = await this.Person.AskAsync(question, this.CancellationToken);

		if (answer.Reply == PersonReply.Skipped)
		{
			this.Note($"Not answered: {name}");
		}
		else
		{
			this.Check($"{name} (a person's answer)", answer.Reply == PersonReply.Yes, answer.Comment);
		}

		return answer;
	}

	/// <summary>
	/// Writes a progress note.
	/// </summary>
	/// <param name="message">The note.</param>
	public void Note(string message)
	{
		this.log.Add(message);
		this.output?.Invoke("  " + message);
	}

	/// <summary>
	/// Gets a path in this scenario's evidence folder, creating the folder.
	/// </summary>
	/// <param name="fileName">The file's name.</param>
	/// <returns>The full path.</returns>
	/// <exception cref="ArgumentException"><paramref name="fileName"/> is <see langword="null"/>, empty or whitespace.</exception>
	public string GetEvidencePath(string fileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

		Directory.CreateDirectory(this.EvidenceDirectory);

		return Path.Combine(this.EvidenceDirectory, fileName);
	}

	/// <summary>
	/// Keeps a file as evidence.
	/// </summary>
	/// <param name="path">The file's full path.</param>
	/// <param name="description">What the file is.</param>
	public void AddEvidence(string path, string description)
	{
		this.evidence.Add(new ScenarioEvidence(path, description));
	}

	/// <summary>
	/// Writes bytes to a file in the evidence folder and keeps it as evidence.
	/// </summary>
	/// <param name="fileName">The file's name.</param>
	/// <param name="content">What to write.</param>
	/// <param name="description">What the file is.</param>
	/// <returns>A task that completes when the file is written.</returns>
	public async Task SaveEvidenceAsync(string fileName, byte[] content, string description)
	{
		string path = this.GetEvidencePath(fileName);
		await File.WriteAllBytesAsync(path, content, this.CancellationToken);
		this.AddEvidence(path, description);
	}

	/// <summary>
	/// Writes text to a file in the evidence folder and keeps it as evidence.
	/// </summary>
	/// <param name="fileName">The file's name.</param>
	/// <param name="lines">The lines to write.</param>
	/// <param name="description">What the file is.</param>
	/// <returns>A task that completes when the file is written.</returns>
	public async Task SaveEvidenceAsync(string fileName, IEnumerable<string> lines, string description)
	{
		string path = this.GetEvidencePath(fileName);
		await File.WriteAllLinesAsync(path, lines, this.CancellationToken);
		this.AddEvidence(path, description);
	}

	/// <summary>
	/// Keeps something until the scenario ends, then disposes it, for objects a scenario's helpers create but its
	/// later steps still use.
	/// </summary>
	/// <param name="disposable">What to dispose.</param>
	public void RegisterForDisposal(IAsyncDisposable disposable)
	{
		ArgumentNullException.ThrowIfNull(disposable);

		this.disposables.Add(disposable);
	}

	/// <summary>
	/// Disposes everything registered, last first.
	/// </summary>
	/// <returns>A task that completes when everything is disposed.</returns>
	internal async Task DisposeRegisteredAsync()
	{
		for (int index = this.disposables.Count - 1; index >= 0; index--)
		{
			await this.disposables[index].DisposeAsync();
		}

		this.disposables.Clear();
	}

	/// <summary>
	/// Builds a printer catalog through dependency injection, the way applications do, over every print system the
	/// targets cover (<see cref="HarnessTargets.RegisterPrintSystems"/>).
	/// </summary>
	/// <returns>The catalog fixture. The caller disposes it.</returns>
	public CatalogFixture CreateCatalog()
	{
		return new CatalogFixture(this.Targets.RegisterPrintSystems);
	}
}
