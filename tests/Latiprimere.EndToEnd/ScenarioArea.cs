namespace Latiprimere.EndToEnd;

/// <summary>
/// The part of Latiprimere a scenario checks, for choosing which scenarios to run.
/// </summary>
public enum ScenarioArea
{
	/// <summary>
	/// The printer catalog over several print systems: listing, lookup, default printer, failure isolation.
	/// </summary>
	Catalog = 0,

	/// <summary>
	/// Finding printers: CUPS queues, Windows queues, configured IPP printers, mDNS.
	/// </summary>
	Discovery = 1,

	/// <summary>
	/// What printers report they can do.
	/// </summary>
	Capabilities = 2,

	/// <summary>
	/// Sending documents and options, and what the printer receives.
	/// </summary>
	Submission = 3,

	/// <summary>
	/// Following jobs to the end, and cancelling them.
	/// </summary>
	Lifecycle = 4,

	/// <summary>
	/// Printers that are busy, slow, unreachable or refuse a job.
	/// </summary>
	Faults = 5,

	/// <summary>
	/// Authentication and encrypted connections.
	/// </summary>
	Security = 6,

	/// <summary>
	/// Rendering PDFs for printers that can't take them as they are.
	/// </summary>
	Rendering = 7,

	/// <summary>
	/// The samples and the documentation's examples.
	/// </summary>
	Samples = 8,

	/// <summary>
	/// Checks a person makes on a printer they name, which may be real: the console harness's manual mode.
	/// </summary>
	Manual = 9
}
