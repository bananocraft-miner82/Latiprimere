namespace Latiprimere;

/// <summary>
/// A job that has been submitted to a printer.
/// </summary>
/// <remarks>
/// Safe to use from several threads at once, for example to read its status on one while cancelling it on another.
/// </remarks>
public interface IPrintJob
{
	/// <summary>
	/// Gets the identifier of the job, as assigned by the print system. Unique only within its printer.
	/// </summary>
	string Id { get; }

	/// <summary>
	/// Gets the identifier of the printer the job was submitted to.
	/// </summary>
	string PrinterId { get; }

	/// <summary>
	/// Reads the job's current status from the print system.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The job's current status.</returns>
	/// <exception cref="PrintException">The print system failed to report the job's status.</exception>
	Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Asks the print system to cancel the job.
	/// </summary>
	/// <param name="cancellationToken">A token to cancel the request.</param>
	/// <returns>
	/// <see langword="true"/> if the print system accepted the cancellation; the job then reports
	/// <see cref="PrintJobState.Canceled"/> once it has stopped. <see langword="false"/> if the job had already
	/// finished, which can happen between reading its status and asking to cancel it.
	/// </returns>
	/// <exception cref="NotSupportedException">The print system can't cancel jobs.</exception>
	/// <exception cref="PrintException">The print system refused or failed to cancel the job.</exception>
	Task<bool> CancelAsync(CancellationToken cancellationToken = default);
}
