using System.Globalization;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows.Mapping;

/// <summary>
/// Maps a Windows printer driver's capabilities to <see cref="PrinterCapabilities"/>.
/// </summary>
internal static class WindowsCapabilitiesMapper
{
	/// <summary>Tenths of a millimetre in a millimetre.</summary>
	private const double TenthsPerMillimetre = 10.0;

	/// <summary>
	/// Creates capabilities from a driver's capabilities.
	/// </summary>
	/// <param name="record">The driver's capabilities and defaults.</param>
	/// <returns>The capabilities.</returns>
	internal static PrinterCapabilities ToCapabilities(WindowsCapabilitiesRecord record)
	{
		ArgumentNullException.ThrowIfNull(record);

		// Name each size once, by its standard name; the first of several driver entries for one size wins
		List<(short Id, MediaSize Size)> papers = record.Papers
			.Select(paper => (paper.Id, Size: WindowsCapabilitiesMapper.ToMediaSize(paper)))
			.DistinctBy(paper => paper.Size.Name, StringComparer.Ordinal)
			.ToList();

		MediaSize? defaultSize = papers
			.Where(paper => paper.Id == record.DefaultPaperId)
			.Select(paper => paper.Size)
			.FirstOrDefault();

		return new PrinterCapabilities
		{
			MediaSizes        = papers.Select(paper => paper.Size).ToList(),
			DefaultMediaSize  = defaultSize,
			DuplexModes       = record.CanDuplex
				? new[] { DuplexMode.OneSided, DuplexMode.LongEdge, DuplexMode.ShortEdge }
				: new[] { DuplexMode.OneSided },
			DefaultDuplexMode = record.DefaultDuplex switch
			{
				DevModeConstants.DuplexSimplex => DuplexMode.OneSided,
				DevModeConstants.DuplexVertical => DuplexMode.LongEdge,
				DevModeConstants.DuplexHorizontal => DuplexMode.ShortEdge,
				_ => null,
			},
			ColorModes        = record.IsColorDevice
				? new[] { ColorMode.Monochrome, ColorMode.Color }
				: new[] { ColorMode.Monochrome },
			DefaultColorMode  = record.DefaultColor switch
			{
				DevModeConstants.ColorMonochrome => ColorMode.Monochrome,
				DevModeConstants.ColorColor => ColorMode.Color,
				_ => null,
			},
			MaxCopies         = record.MaxCopies,
			Orientations      = record.CanPrintLandscape
				? new[] { PageOrientation.Portrait, PageOrientation.Landscape }
				: new[] { PageOrientation.Portrait },
			InputTrays        = record.Bins
				.Select(bin => new InputTray { Id = bin.Id.ToString(CultureInfo.InvariantCulture), Name = bin.Name })
				.ToList()
		};
	}

	/// <summary>
	/// Names a driver's paper size by its PWG name, turned to portrait.
	/// </summary>
	/// <param name="paper">The driver's paper.</param>
	/// <returns>
	/// The size, with the standard PWG name if it is a common size, or a <c>custom_</c> name built from its
	/// dimensions; and the driver's own name to show people.
	/// </returns>
	private static MediaSize ToMediaSize(WindowsPaperRecord paper)
	{
		double first = paper.WidthTenthsOfMillimetre / WindowsCapabilitiesMapper.TenthsPerMillimetre;
		double second = paper.HeightTenthsOfMillimetre / WindowsCapabilitiesMapper.TenthsPerMillimetre;
		double width = Math.Min(first, second);
		double height = Math.Max(first, second);

		string dimensions = string.Format(CultureInfo.InvariantCulture, "{0:0.##}x{1:0.##}mm", width, height);
		string name = StandardMediaSizes.FindName(width, height) ?? "custom_" + dimensions + "_" + dimensions;

		return new MediaSize
		{
			Name              = name,
			DisplayName       = paper.Name,
			WidthMillimetres  = width,
			HeightMillimetres = height
		};
	}
}
