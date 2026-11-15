namespace Latiprimere.Windows.Spooler;

/// <summary>
/// A paper source on a printer.
/// </summary>
internal sealed record WindowsBinRecord
{
	/// <summary>Gets the DMBIN_* identifier.</summary>
	public required short Id { get; init; }

	/// <summary>Gets the driver's name for the source, such as "Manual Feed".</summary>
	public required string Name { get; init; }
}
