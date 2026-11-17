namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// IPP enum values the fake printer uses (RFC 8011).
/// </summary>
internal static class IppValues
{
	/// <summary><c>printer-state</c> idle.</summary>
	public const int PrinterIdle = 3;

	/// <summary><c>printer-state</c> processing.</summary>
	public const int PrinterProcessing = 4;

	/// <summary><c>printer-state</c> stopped.</summary>
	public const int PrinterStopped = 5;

	/// <summary>The operations the fake printer supports, for <c>operations-supported</c>: Print-Job,
	/// Validate-Job, Cancel-Job, Get-Job-Attributes, Get-Jobs and Get-Printer-Attributes.</summary>
	public static readonly int[] SupportedOperations = [0x0002, 0x0004, 0x0008, 0x0009, 0x000A, 0x000B];

	/// <summary><c>job-state</c> pending.</summary>
	public const int JobPending = 3;

	/// <summary><c>job-state</c> processing.</summary>
	public const int JobProcessing = 5;

	/// <summary><c>job-state</c> processing-stopped.</summary>
	public const int JobProcessingStopped = 6;

	/// <summary><c>job-state</c> canceled.</summary>
	public const int JobCanceled = 7;

	/// <summary><c>job-state</c> aborted.</summary>
	public const int JobAborted = 8;

	/// <summary><c>job-state</c> completed.</summary>
	public const int JobCompleted = 9;
}
