namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// How the fake printer ends the jobs it prints.
/// </summary>
public enum FakeJobOutcome
{
	/// <summary>
	/// <c>job-state</c> completed (9).
	/// </summary>
	Completed = 0,

	/// <summary>
	/// <c>job-state</c> aborted (8), as a printer does after a jam or an error it can't recover from.
	/// </summary>
	Aborted = 1
}
