using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples.Tests.Fakes;

/// <summary>
/// A print job that reports a scripted series of states, one for each status check, then stays on the last.
/// </summary>
internal sealed class FakePrintJob : IPrintJob
{
	private readonly Queue<PrintJobState> upcomingStates; // States still to report, in order
	private PrintJobState currentState;                  // The state most recently reported

	/// <summary>
	/// Initialises a new instance of the <see cref="FakePrintJob"/> class.
	/// </summary>
	/// <param name="id">The job's ID.</param>
	/// <param name="printerId">The ID of the printer the job was sent to.</param>
	/// <param name="states">The states to report, in order. There must be at least one.</param>
	/// <exception cref="ArgumentException"><paramref name="states"/> is empty.</exception>
	public FakePrintJob(string id, string printerId, IReadOnlyList<PrintJobState> states)
	{
		ArgumentOutOfRangeException.ThrowIfZero(states.Count, nameof(states));

		this.Id = id;
		this.PrinterId = printerId;
		this.upcomingStates = new Queue<PrintJobState>(states);
		this.currentState = states[0];
	}

	/// <summary>
	/// Gets the job's ID.
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Gets the ID of the printer the job was sent to.
	/// </summary>
	public string PrinterId { get; }

	/// <summary>
	/// Reports the next scripted state, or the last one once the script has run out.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The job's status.</returns>
	public Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		if (this.upcomingStates.Count > 0)
		{
			this.currentState = this.upcomingStates.Dequeue();
		}

		PrintJobStatus status = new PrintJobStatus
		{
			JobId = this.Id,
			State = this.currentState,
			Timestamp = DateTimeOffset.UtcNow,
		};

		return Task.FromResult(status);
	}

	/// <summary>
	/// Cancels the job unless it has already finished, as the print systems do.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns><see langword="true"/> if the job was cancelled; <see langword="false"/> if it had already
	/// finished.</returns>
	public Task<bool> CancelAsync(CancellationToken cancellationToken = default)
	{
		bool hasFinished = this.currentState is PrintJobState.Completed or PrintJobState.Canceled or PrintJobState.Aborted;

		if (!hasFinished)
		{
			this.upcomingStates.Clear();
			this.currentState = PrintJobState.Canceled;
		}

		return Task.FromResult(!hasFinished);
	}
}
