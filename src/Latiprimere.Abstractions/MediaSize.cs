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
	public required double WidthMillimeters { get; init; }

	/// <summary>
	/// Gets the height in millimetres, with the paper in portrait.
	/// </summary>
	public required double HeightMillimeters { get; init; }

	/// <summary>
	/// Creates a size of your own, such as a label or a length of a receipt roll, for a printer whose
	/// <see cref="PrinterCapabilities.CustomMediaSizes"/> contain it.
	/// </summary>
	/// <param name="widthMillimeters">The width in millimetres, in portrait.</param>
	/// <param name="heightMillimeters">The height in millimetres, in portrait.</param>
	/// <returns>The size, named <c>custom_</c> and its dimensions, as a printer names sizes without a standard
	/// name: <c>custom_102x152mm_102x152mm</c>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">A dimension is zero, negative or not a number.</exception>
	public static MediaSize Custom(double widthMillimeters, double heightMillimeters)
	{
		bool isPositive = widthMillimeters > 0 && heightMillimeters > 0;

		if (!isPositive || double.IsInfinity(widthMillimeters) || double.IsInfinity(heightMillimeters))
		{
			string message = Latiprimere.Resources.ExceptionMessages.FormatMediaSizeNotPositive(widthMillimeters, heightMillimeters);

			throw new ArgumentOutOfRangeException(nameof(widthMillimeters), message);
		}

		// To a hundredth of a millimetre, IPP's unit of paper size, without trailing zeros
		string width = Math.Round(widthMillimeters, 2, MidpointRounding.AwayFromZero).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
		string height = Math.Round(heightMillimeters, 2, MidpointRounding.AwayFromZero).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
		string dimensions = width + "x" + height + "mm";

		return new MediaSize
		{
			Name              = "custom_" + dimensions + "_" + dimensions,
			DisplayName       = width + " × " + height + " mm",
			WidthMillimeters  = Math.Round(widthMillimeters, 2, MidpointRounding.AwayFromZero),
			HeightMillimeters = Math.Round(heightMillimeters, 2, MidpointRounding.AwayFromZero)
		};
	}
}
