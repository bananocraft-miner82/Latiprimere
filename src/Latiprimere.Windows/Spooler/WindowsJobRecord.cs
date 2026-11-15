using Latiprimere.Windows.Interop;

namespace Latiprimere.Windows.Spooler;

/// <summary>
/// A print job as reported by the Windows spooler, copied out of the native buffer.
/// </summary>
internal sealed record WindowsJobRecord
{
	/// <summary>Gets the job ID.</summary>
	public required int JobId { get; init; }

	/// <summary>Gets the job's status flags.</summary>
	public JobStatusFlags Status { get; init; }

	/// <summary>Gets a status description from the port monitor or driver, if any.</summary>
	public string? StatusText { get; init; }

	/// <summary>Gets the number of pages in the job, or zero if unknown.</summary>
	public int TotalPages { get; init; }

	/// <summary>Gets the number of pages printed so far.</summary>
	public int PagesPrinted { get; init; }
}
