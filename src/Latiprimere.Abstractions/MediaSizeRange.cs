namespace Latiprimere;

/// <summary>
/// The sizes of paper a printer takes beyond its listed sizes: custom sheets, or lengths cut from a roll, such as a
/// receipt or label printer's.
/// </summary>
/// <remarks>
/// A size inside the range, made with <see cref="MediaSize.Custom"/>, can be passed in
/// <see cref="PrintOptions.MediaSize"/>. Widths and heights are in portrait, as <see cref="MediaSize"/>'s are.
/// </remarks>
public sealed record MediaSizeRange
{
	/// <summary>Gets the narrowest width, in millimetres.</summary>
	public required double MinWidthMillimeters { get; init; }

	/// <summary>Gets the widest width, in millimetres.</summary>
	public required double MaxWidthMillimeters { get; init; }

	/// <summary>Gets the shortest height, in millimetres.</summary>
	public required double MinHeightMillimeters { get; init; }

	/// <summary>Gets the longest height, in millimetres.</summary>
	public required double MaxHeightMillimeters { get; init; }

	/// <summary>
	/// Gets a value indicating whether the sizes come from a roll, cut to length, rather than custom sheets.
	/// </summary>
	public bool IsRoll { get; init; }

	/// <summary>
	/// Checks whether a size is inside the range, to within a tenth of a millimetre.
	/// </summary>
	/// <param name="size">The size.</param>
	/// <returns><see langword="true"/> if the printer takes it.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="size"/> is <see langword="null"/>.</exception>
	public bool Contains(MediaSize size)
	{
		ArgumentNullException.ThrowIfNull(size);

		const double tolerance = 0.1;

		return size.WidthMillimeters >= this.MinWidthMillimeters - tolerance
				&& size.WidthMillimeters <= this.MaxWidthMillimeters + tolerance
				&& size.HeightMillimeters >= this.MinHeightMillimeters - tolerance
				&& size.HeightMillimeters <= this.MaxHeightMillimeters + tolerance;
	}
}
