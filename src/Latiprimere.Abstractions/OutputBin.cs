namespace Latiprimere;

/// <summary>
/// Where a printer delivers the printed sheets, such as its top tray or a mailbox, as the printer reports it.
/// </summary>
public sealed record OutputBin
{
	/// <summary>
	/// Gets the identifier to pass back in <see cref="PrintOptions.OutputBin"/>: an IPP <c>output-bin</c> keyword, such
	/// as <c>face-down</c> or <c>mailbox-1</c>.
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// Gets the name to show people, such as "Face down".
	/// </summary>
	public required string Name { get; init; }
}
