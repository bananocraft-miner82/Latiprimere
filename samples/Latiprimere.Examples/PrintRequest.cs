namespace LatiprimereExamples;

/// <summary>
/// A document waiting to be printed by <see cref="PrintWorker"/>. Part of "Printing from a web app or background
/// service".
/// </summary>
internal sealed record PrintRequest
{
	/// <summary>
	/// Gets the ID of the printer to print on, as the caller sent it.
	/// </summary>
	public required string PrinterId { get; init; }

	/// <summary>
	/// Gets the PDF document.
	/// </summary>
	public required byte[] Document { get; init; }

	/// <summary>
	/// Gets the name to show in the print queue, or <see langword="null"/> to let the print system choose.
	/// </summary>
	public string? JobName { get; init; }
}
