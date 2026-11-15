using System.Globalization;
using Latiprimere.Windows.Mapping;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows;

/// <summary>
/// A job submitted to the Windows spooler.
/// </summary>
internal sealed class WindowsPrintJob : IPrintJob
{
	private readonly IWinSpool spool;              // The spooler the job was sent to
	private readonly string printerName;           // The spooler's name for the printer
	private readonly int jobNumber;                // The spooler's job ID
	private readonly TimeProvider timeProvider;    // Clock for status timestamps
	private volatile bool isCancelRequested = false; // Whether Latiprimere has cancelled the job

	/// <summary>
	/// Initialises a new instance of the <see cref="WindowsPrintJob"/> class.
	/// </summary>
	/// <param name="spool">The spooler the job was sent to.</param>
	/// <param name="printerName">The spooler's name for the printer.</param>
	/// <param name="printerId">The Latiprimere identifier of the printer.</param>
	/// <param name="jobNumber">The spooler's job ID.</param>
	/// <param name="timeProvider">The clock for status timestamps.</param>
	public WindowsPrintJob(IWinSpool spool, string printerName, string printerId, int jobNumber, TimeProvider timeProvider)
	{
		this.spool = spool;
		this.printerName = printerName;
		this.PrinterId = printerId;
		this.jobNumber = jobNumber;
		this.timeProvider = timeProvider;
	}

	/// <inheritdoc/>
	public string Id
	{
		get
		{
			return this.jobNumber.ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <inheritdoc/>
	public string PrinterId { get; }

	/// <inheritdoc/>
	public async Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		// Spooler calls block, so run them off the caller's thread
		WindowsJobRecord? job = await Task.Run(() => this.spool.GetJob(this.printerName, this.jobNumber), cancellationToken)
										  .ConfigureAwait(false);

		return WindowsJobStatusMapper.ToPrintJobStatus
		(
			job,
			this.Id,
			this.isCancelRequested,
			this.timeProvider.GetUtcNow()
		);
	}

	/// <inheritdoc/>
	public async Task<bool> CancelAsync(CancellationToken cancellationToken = default)
	{
		bool isDeleted = await Task.Run(() => this.spool.DeleteJob(this.printerName, this.jobNumber), cancellationToken)
								   .ConfigureAwait(false);

		// A job that has already left the queue has finished, so there is nothing to cancel
		if (isDeleted)
		{
			this.isCancelRequested = true;
		}

		return isDeleted;
	}
}
