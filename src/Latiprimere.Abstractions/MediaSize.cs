namespace Latiprimere;

/// <summary>
/// A paper size a printer can print on.
/// </summary>
/// <remarks>
/// Sizes are identified by their PWG 5101.1 self-describing name, such as <c>iso_a4_210x297mm</c> or
/// <c>na_letter_8.5x11in</c>, on every print system, so the same paper has the same name whichever print system
/// reports it. Sizes without a standard name are named <c>custom_</c>, followed by their dimensions.
/// Compare sizes by <see cref="Name"/>: record equality also compares <see cref="DisplayName"/>, which differs
/// between printer drivers.
/// </remarks>
public sealed record MediaSize
{
	/// <summary>
	/// Gets the PWG self-describing name, such as <c>iso_a4_210x297mm</c>.
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// Gets a name to show people, such as "A4" or "Letter". Comes from the printer driver where it has one.
	/// </summary>
	public required string DisplayName { get; init; }

	/// <summary>
	/// Gets the width in millimetres, with the paper in portrait.
	/// </summary>
	public required double WidthMillimetres { get; init; }

	/// <summary>
	/// Gets the height in millimetres, with the paper in portrait.
	/// </summary>
	public required double HeightMillimetres { get; init; }
}
