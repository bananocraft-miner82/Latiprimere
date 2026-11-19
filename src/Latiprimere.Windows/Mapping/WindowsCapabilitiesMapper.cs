using System.Globalization;
using System.Xml;
using Latiprimere.Windows.Interop;
using Latiprimere.Windows.PrintTickets;
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
	/// Finds the driver's paper with a given PWG name, named as <see cref="ToCapabilities"/> names it.
	/// </summary>
	/// <param name="record">The driver's capabilities.</param>
	/// <param name="mediaSizeName">The PWG name, such as <c>iso_a4_210x297mm</c>.</param>
	/// <returns>The DMPAPER_* number, or <see langword="null"/> if the driver has no such paper.</returns>
	internal static short? FindPaperId(WindowsCapabilitiesRecord record, string mediaSizeName)
	{
		ArgumentNullException.ThrowIfNull(record);
		ArgumentNullException.ThrowIfNull(mediaSizeName);

		// The first of several driver entries for one size, as in the capabilities
		return record.Papers
			.Where(paper => string.Equals(WindowsCapabilitiesMapper.ToMediaSize(paper).Name, mediaSizeName, StringComparison.Ordinal))
			.Select(paper => (short?)paper.Id)
			.FirstOrDefault();
	}

	/// <summary>
	/// Finds the driver's paper source with a given tray ID.
	/// </summary>
	/// <param name="record">The driver's capabilities.</param>
	/// <param name="trayId">The tray ID, a DMBIN_* number as <see cref="ToCapabilities"/> writes it.</param>
	/// <returns>The DMBIN_* number, or <see langword="null"/> if the driver has no such source.</returns>
	internal static short? FindBinId(WindowsCapabilitiesRecord record, string trayId)
	{
		ArgumentNullException.ThrowIfNull(record);
		ArgumentNullException.ThrowIfNull(trayId);

		if (!short.TryParse(trayId, NumberStyles.None, CultureInfo.InvariantCulture, out short binId))
		{
			return null;
		}

		return record.Bins.Any(bin => bin.Id == binId) ? binId : null;
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

	/// <summary>
	/// Lists the driver's Print Capabilities features and parameters as vendor options (ADR 0032).
	/// </summary>
	/// <param name="record">The driver's capabilities, with its Print Capabilities document if it has one.</param>
	/// <param name="withDefaults">Whether to read each option's default from the default Print Ticket.</param>
	/// <returns>The vendor options; empty without Print Ticket support.</returns>
	/// <exception cref="XmlException">The Print Capabilities, or with <paramref name="withDefaults"/> the default
	/// ticket, aren't XML.</exception>
	internal static IReadOnlyList<VendorOption> ToVendorOptions(WindowsCapabilitiesRecord record, bool withDefaults)
	{
		ArgumentNullException.ThrowIfNull(record);

		return record.PrintCapabilities is null
			? Array.Empty<VendorOption>()
			: PrintCapabilitiesDocument.Parse(record.PrintCapabilities).ToVendorOptions(withDefaults ? record.DefaultPrintTicket : null);
	}
}
