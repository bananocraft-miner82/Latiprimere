namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// What the fake printer does while it runs. Every property can be changed at any time, and applies from the next
/// request on.
/// </summary>
public sealed class FakePrinterBehaviour
{
	/// <summary>
	/// Gets or sets how many of the next Print-Job requests are turned away with <c>server-error-busy</c>. Each one
	/// turned away counts down by one.
	/// </summary>
	public int BusyPrintJobs { get; set; }

	/// <summary>
	/// Gets or sets how long the printer waits before answering each request.
	/// </summary>
	public TimeSpan ResponseDelay { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the printer closes the connection, without answering, after receiving
	/// a Print-Job request.
	/// </summary>
	public bool DropConnectionOnPrintJob { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the printer is stopped. Jobs it accepts wait until it starts again.
	/// </summary>
	public bool IsStopped { get; set; }

	/// <summary>
	/// Gets or sets the <c>printer-state-reasons</c> keyword while the printer is stopped, such as
	/// <c>media-empty-error</c>.
	/// </summary>
	public string StoppedReason { get; set; } = "paused";

	/// <summary>
	/// Gets or sets the <c>printer-state-message</c>, or <see langword="null"/> for none.
	/// </summary>
	public string? StateMessage { get; set; }

	/// <summary>
	/// Gets or sets how long each job stays processing before it ends.
	/// </summary>
	public TimeSpan ProcessingTime { get; set; }

	/// <summary>
	/// Gets or sets how jobs end.
	/// </summary>
	public FakeJobOutcome JobOutcome { get; set; } = FakeJobOutcome.Completed;

	/// <summary>
	/// Gets or sets the <c>job-state-message</c> a job has once it ends, such as <c>Paper jam</c>.
	/// </summary>
	public string? JobOutcomeMessage { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether Print-Job and Validate-Job are checked against the profile's
	/// <c>*-supported</c> attributes, as a printer would, and refused with
	/// <c>client-error-attributes-or-values-not-supported</c> or <c>client-error-document-format-not-supported</c>.
	/// </summary>
	public bool ValidateJobs { get; set; } = true;
}
