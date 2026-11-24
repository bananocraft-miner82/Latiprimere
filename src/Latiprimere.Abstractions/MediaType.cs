namespace Latiprimere;

/// <summary>
/// A kind of paper or other medium to print on, such as plain paper, labels or glossy photo paper, as the printer
/// reports it.
/// </summary>
public sealed record MediaType
{
	/// <summary>
	/// Gets the identifier to pass back in <see cref="PrintOptions.MediaType"/>: an IPP <c>media-type</c> keyword,
	/// such as <c>stationery</c> or <c>labels</c>, or a Windows driver's number for the type. It only means something
	/// to the printer that reported it.
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// Gets the name to show people, such as "Labels".
	/// </summary>
	public required string Name { get; init; }
}
