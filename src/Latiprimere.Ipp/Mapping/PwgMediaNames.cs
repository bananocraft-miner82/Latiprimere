using System.Globalization;
using System.Text.RegularExpressions;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Reads PWG 5101.1 self-describing media names, such as <c>iso_a4_210x297mm</c>, into media sizes.
/// </summary>
/// <remarks>
/// A self-describing name has three parts separated by underscores: a class (<c>iso</c>, <c>na</c>, <c>jis</c>,
/// <c>custom</c> and so on), a size name, and the dimensions in inches or millimetres, width first.
/// </remarks>
internal static partial class PwgMediaNames
{
	/// <summary>Millimetres in an inch.</summary>
	private const decimal MillimetresPerInch = 25.4m;

	/// <summary>The class of names that have no standard size name.</summary>
	private const string CustomClass = "custom";

	/// <summary>
	/// Reads a self-describing media name.
	/// </summary>
	/// <param name="name">The name, such as <c>na_letter_8.5x11in</c>.</param>
	/// <returns>
	/// The size, or <see langword="null"/> if the name isn't self-describing, or is a <c>min</c> or <c>max</c> limit
	/// for custom or roll sizes rather than a size.
	/// </returns>
	internal static MediaSize? ToMediaSize(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		// Class first, dimensions last; the size name in between may itself hold underscores
		string[] parts = name.Split('_');

		if (parts.Length < 3)
		{
			return null;
		}

		Match dimensions = PwgMediaNames.DimensionsPattern().Match(parts[^1]);
		string sizeName = string.Join('_', parts[1..^1]);

		if (!dimensions.Success || sizeName is "min" or "max")
		{
			return null;
		}

		// Decimal arithmetic keeps exact values such as 4.125in = 104.775mm, so rounding gives the same result on
		// every runtime; double rounding of 104.77499999... differs between .NET 10 and .NET 11
		decimal width = decimal.Parse(dimensions.Groups["width"].Value, CultureInfo.InvariantCulture);
		decimal height = decimal.Parse(dimensions.Groups["height"].Value, CultureInfo.InvariantCulture);
		string unit = dimensions.Groups["unit"].Value;
		decimal scale = unit == "in" ? PwgMediaNames.MillimetresPerInch : 1m;

		return new MediaSize
		{
			Name              = name,
			DisplayName       = PwgMediaNames.ToDisplayName(parts[0], sizeName, width, height, unit),
			WidthMillimetres  = (double)Math.Round(width * scale, 2, MidpointRounding.AwayFromZero),
			HeightMillimetres = (double)Math.Round(height * scale, 2, MidpointRounding.AwayFromZero)
		};
	}

	/// <summary>
	/// Makes a name to show people: "A4", "Letter", "Number 10", "B5 (JIS)"; or the dimensions for custom sizes.
	/// </summary>
	/// <param name="mediaClass">The class part of the name.</param>
	/// <param name="sizeName">The size-name part.</param>
	/// <param name="width">The width, in <paramref name="unit"/>.</param>
	/// <param name="height">The height, in <paramref name="unit"/>.</param>
	/// <param name="unit">The unit, <c>in</c> or <c>mm</c>.</param>
	/// <returns>The display name.</returns>
	private static string ToDisplayName(string mediaClass, string sizeName, decimal width, decimal height, string unit)
	{
		if (mediaClass == PwgMediaNames.CustomClass)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0} × {1} {2}", width, height, unit);
		}

		// Dimensions stay as they are ("4x6"), short words are codes ("a4", "dl", "edp"), longer ones are words
		IEnumerable<string> words = sizeName
			.Split('-', '_')
			.Where(word => word.Length > 0)
			.Select(word => char.IsAsciiDigit(word[0])
				? word
				: word.Length <= 3
					? word.ToUpperInvariant()
					: char.ToUpperInvariant(word[0]) + word[1..]);

		string displayName = string.Join(' ', words);

		// JIS and ISO B sizes differ, so say which
		return mediaClass == "jis" ? displayName + " (JIS)" : displayName;
	}

	/// <summary>
	/// Matches the dimensions part, such as <c>210x297mm</c> or <c>8.5x11in</c>.
	/// </summary>
	/// <returns>The pattern.</returns>
	/// <remarks>
	/// The names come from the printer. Up to six digits each side of the point covers any real medium, and keeps
	/// a name such as <c>custom_x_99999999999999999999999999999x1mm</c> from overflowing decimal, which skips it
	/// like any other name that isn't understood.
	/// </remarks>
	[GeneratedRegex(@"^(?<width>\d{1,6}(\.\d{1,6})?)x(?<height>\d{1,6}(\.\d{1,6})?)(?<unit>in|mm)$", RegexOptions.CultureInvariant)]
	private static partial Regex DimensionsPattern();
}
