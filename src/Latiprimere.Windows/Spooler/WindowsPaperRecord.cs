namespace Latiprimere.Windows.Spooler;

/// <summary>
/// A paper size a printer driver supports.
/// </summary>
internal sealed record WindowsPaperRecord
{
	/// <summary>Gets the DMPAPER_* identifier.</summary>
	public required short Id { get; init; }

	/// <summary>Gets the driver's name for the paper, such as "A4".</summary>
	public required string Name { get; init; }

	/// <summary>Gets the width in tenths of a millimetre.</summary>
	public required int WidthTenthsOfMillimetre { get; init; }

	/// <summary>Gets the height in tenths of a millimetre.</summary>
	public required int HeightTenthsOfMillimetre { get; init; }
}
