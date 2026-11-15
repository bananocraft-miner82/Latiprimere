namespace Latiprimere.Tests.Fakes;

/// <summary>
/// An in-memory print job that reports a scripted sequence of statuses, one per status request.
/// Once the script runs out, the last status is reported for every further request.
/// </summary>
internal sealed class FakePrintJob : IPrintJob
{
	private readonly Queue<PrintJobStatus> scriptedStatuses; // Statuses still to report, in order
	private PrintJobStatus lastStatus;                       // Most recently reported status

	/// <summary>
	/// Initialises a new instance of the <see cref="FakePrintJob"/> class.
	/// </summary>
	/// <param name="firstStatus">The status reported by the first request.</param>
	/// <param name="laterStatuses">The statuses reported by later requests, in order.</param>
	public FakePrintJob(PrintJobStatus firstStatus, params PrintJobStatus[] laterStatuses)
	{
		this.lastStatus = firstStatus;
		this.scriptedStatuses = new Queue<PrintJobStatus>(laterStatuses.Prepend(firstStatus));
	}

	/// <summary>
	/// Gets the job identifier.
	/// </summary>
	public string Id
	{
		get
		{
			return this.lastStatus.JobId;
		}
	}

	/// <summary>
	/// Gets the identifier of the printer the job was submitted to.
	/// </summary>
	public string PrinterId { get; init; } = "printer-1";

	/// <summary>
	/// Gets the number of times the job's status has been requested.
	/// </summary>
	public int StatusRequestCount { get; private set; }

	/// <summary>
	/// Gets an exception to throw once the script has run out, instead of repeating the last status, as when the
	/// printer stops answering partway through.
	/// </summary>
	public PrintException? FailureAfterScript { get; init; }

	/// <summary>
	/// Reports the next scripted status; once the script has run out, throws <see cref="FailureAfterScript"/> if set,
	/// or repeats the last status.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The status.</returns>
	/// <exception cref="PrintException">The script has run out and <see cref="FailureAfterScript"/> is set.</exception>
	public Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken)
	{
		// Advance through the script, holding on the final entry
		this.StatusRequestCount++;

		if (this.scriptedStatuses.Count > 0)
		{
			this.lastStatus = this.scriptedStatuses.Dequeue();
		}
		else if (this.FailureAfterScript is not null)
		{
			return Task.FromException<PrintJobStatus>(this.FailureAfterScript);
		}

		return Task.FromResult(this.lastStatus);
	}

	/// <summary>
	/// Accepts the cancellation unless the last status reported was final.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>Whether the job was still running.</returns>
	public Task<bool> CancelAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(!this.lastStatus.IsTerminal);
	}
}
