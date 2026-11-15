namespace Latiprimere.Windows.Mapping;

/// <summary>
/// Common paper sizes with their PWG 5101.1 self-describing names, for naming the sizes Windows drivers report.
/// </summary>
internal static class StandardMediaSizes
{
	/// <summary>How far, in millimetres, a driver's size may differ from a standard one and still match it.</summary>
	private const double Tolerance = 1.0;

	/// <summary>The sizes, width and height in millimetres in portrait, with their PWG names.</summary>
	private static readonly (string Name, double Width, double Height)[] Sizes = new (string, double, double)[]
	{
		("na_letter_8.5x11in", 215.9, 279.4),
		("na_legal_8.5x14in", 215.9, 355.6),
		("na_executive_7.25x10.5in", 184.15, 266.7),
		("na_ledger_11x17in", 279.4, 431.8),
		("na_invoice_5.5x8.5in", 139.7, 215.9),
		("na_foolscap_8.5x13in", 215.9, 330.2),
		("na_govt-letter_8x10in", 203.2, 254.0),
		("na_index-4x6_4x6in", 101.6, 152.4),
		("na_5x7_5x7in", 127.0, 177.8),
		("na_number-10_4.125x9.5in", 104.775, 241.3),
		("na_monarch_3.875x7.5in", 98.425, 190.5),
		("na_number-9_3.875x8.875in", 98.425, 225.425),
		("oe_photo-l_3.5x5in", 88.9, 127.0),
		("iso_a3_297x420mm", 297.0, 420.0),
		("iso_a4_210x297mm", 210.0, 297.0),
		("iso_a5_148x210mm", 148.0, 210.0),
		("iso_a6_105x148mm", 105.0, 148.0),
		("iso_b4_250x353mm", 250.0, 353.0),
		("iso_b5_176x250mm", 176.0, 250.0),
		("iso_c4_229x324mm", 229.0, 324.0),
		("iso_c5_162x229mm", 162.0, 229.0),
		("iso_c6_114x162mm", 114.0, 162.0),
		("iso_dl_110x220mm", 110.0, 220.0),
		("jis_b4_257x364mm", 257.0, 364.0),
		("jis_b5_182x257mm", 182.0, 257.0),
		("jpn_hagaki_100x148mm", 100.0, 148.0),
		("jpn_chou3_120x235mm", 120.0, 235.0),
	};

	/// <summary>
	/// Finds the PWG name of a standard size.
	/// </summary>
	/// <param name="width">The width in millimetres, in portrait.</param>
	/// <param name="height">The height in millimetres, in portrait.</param>
	/// <returns>The name, or <see langword="null"/> if the size isn't one of the common ones.</returns>
	internal static string? FindName(double width, double height)
	{
		foreach ((string name, double standardWidth, double standardHeight) in StandardMediaSizes.Sizes)
		{
			if (Math.Abs(standardWidth - width) <= StandardMediaSizes.Tolerance
				&& Math.Abs(standardHeight - height) <= StandardMediaSizes.Tolerance)
			{
				return name;
			}
		}

		return null;
	}
}
