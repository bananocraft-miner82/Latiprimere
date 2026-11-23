using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Maps IPP printer description attributes to <see cref="PrinterCapabilities"/>.
/// </summary>
/// <remarks>
/// Values with no Latiprimere equivalent are left out: reverse orientations, and colour modes such as
/// <c>auto</c> and <c>highlight</c>. Media names that aren't self-describing are left out too.
/// </remarks>
internal static class IppCapabilitiesMapper
{
	/// <summary>
	/// Creates capabilities from a printer's attributes.
	/// </summary>
	/// <param name="attributes">
	/// The attributes, read with <see cref="IppRequestedAttributes.CapabilityAttributes"/>.
	/// </param>
	/// <returns>The capabilities.</returns>
	internal static PrinterCapabilities ToCapabilities(PrinterDescriptionAttributes attributes)
	{
		ArgumentNullException.ThrowIfNull(attributes);

		Media? defaultMedia = IppValueReader.ReadValue(attributes.MediaDefault);
		Sides? defaultSides = IppValueReader.ReadValue(attributes.SidesDefault);
		PrintColorMode? defaultColorMode = IppValueReader.ReadValue(attributes.PrintColorModeDefault);
		SharpIpp.Protocol.Models.Range? copies = IppValueReader.ReadValue(attributes.CopiesSupported);

		return new PrinterCapabilities
		{
			MediaSizes        = IppCapabilitiesMapper.ToMediaSizes(attributes),
			DefaultMediaSize  = defaultMedia is null ? null : PwgMediaNames.ToMediaSize(defaultMedia.Value.Value),
			DuplexModes       = IppCapabilitiesMapper.ToDuplexModes(attributes),
			DefaultDuplexMode = defaultSides is null ? null : IppCapabilitiesMapper.ToDuplexMode(defaultSides.Value),
			ColorModes        = IppCapabilitiesMapper.ToColorModes(attributes, defaultColorMode),
			DefaultColorMode  = defaultColorMode is null ? null : IppCapabilitiesMapper.ToColorMode(defaultColorMode.Value),
			MaxCopies         = copies?.Upper,
			Orientations      = IppCapabilitiesMapper.ToOrientations(attributes),
			InputTrays        = IppCapabilitiesMapper.ToInputTrays(attributes),
			CustomMediaSizes  = PwgMediaNames.ToSizeRanges((IppValueReader.ReadReference(attributes.MediaSupported) ?? []).Select(medium => medium.Value)),
			MediaTypes        = IppCapabilitiesMapper.ToKeywordRecords
			(
				(IppValueReader.ReadReference(attributes.MediaTypeSupported) ?? []).Select(type => type.Value),
				(id, name) => new MediaType { Id = id, Name = name }
			),
			OutputBins        = IppCapabilitiesMapper.ToKeywordRecords
			(
				(IppValueReader.ReadReference(attributes.OutputBinSupported) ?? []).Select(bin => bin.Value),
				(id, name) => new OutputBin { Id = id, Name = name }
			),
			PrintQualities    = (IppValueReader.ReadReference(attributes.PrintQualitySupported) ?? [])
									.Select(IppJobTemplateMapper.ToPrintQuality)
									.Distinct()
									.Order()
									.ToList(),
			DefaultPrintQuality = IppValueReader.ReadValue(attributes.PrintQualityDefault) is { } quality
									? IppJobTemplateMapper.ToPrintQuality(quality)
									: null,
			Finishings        = (IppValueReader.ReadReference(attributes.FinishingsSupported) ?? [])
									.Select(IppJobTemplateMapper.ToFinishing)
									.OfType<Finishing>()
									.Distinct()
									.Order()
									.ToList(),
			SupportsPageRanges = IppValueReader.ReadValue(attributes.PageRangesSupported)
		};
	}

	/// <summary>
	/// Makes records of keywords a printer lists, with names to show people, skipping blanks and repeats.
	/// </summary>
	/// <typeparam name="T">The record.</typeparam>
	/// <param name="keywords">The keywords.</param>
	/// <param name="create">Makes a record from a keyword and its name.</param>
	/// <returns>The records.</returns>
	private static List<T> ToKeywordRecords<T>(IEnumerable<string> keywords, Func<string, string, T> create)
	{
		return keywords
			.Where(keyword => !string.IsNullOrWhiteSpace(keyword))
			.Distinct(StringComparer.Ordinal)
			.Select(keyword => create(keyword, IppKeywords.Describe(keyword)))
			.ToList();
	}

	/// <summary>
	/// Reads <c>media-supported</c>.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>The sizes, without duplicates.</returns>
	private static List<MediaSize> ToMediaSizes(PrinterDescriptionAttributes attributes)
	{
		Media[] media = IppValueReader.ReadReference(attributes.MediaSupported) ?? [];

		return media
			.Select(medium => PwgMediaNames.ToMediaSize(medium.Value))
			.OfType<MediaSize>()
			.DistinctBy(size => size.Name, StringComparer.Ordinal)
			.ToList();
	}

	/// <summary>
	/// Reads <c>sides-supported</c>.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>The duplex modes.</returns>
	private static List<DuplexMode> ToDuplexModes(PrinterDescriptionAttributes attributes)
	{
		Sides[] sides = IppValueReader.ReadReference(attributes.SidesSupported) ?? [];

		return sides
			.Select(IppCapabilitiesMapper.ToDuplexMode)
			.OfType<DuplexMode>()
			.Distinct()
			.ToList();
	}

	/// <summary>
	/// Maps a <c>sides</c> keyword.
	/// </summary>
	/// <param name="sides">The keyword.</param>
	/// <returns>The duplex mode, or <see langword="null"/> for an unknown keyword.</returns>
	private static DuplexMode? ToDuplexMode(Sides sides)
	{
		return sides.Value switch
		{
			"one-sided" => DuplexMode.OneSided,
			"two-sided-long-edge" => DuplexMode.LongEdge,
			"two-sided-short-edge" => DuplexMode.ShortEdge,
			_ => null,
		};
	}

	/// <summary>
	/// Reads <c>print-color-mode-supported</c>, falling back to <c>color-supported</c> and then the default for
	/// printers and queues that don't report it.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <param name="defaultColorMode">The <c>print-color-mode-default</c> value, if reported.</param>
	/// <returns>The colour modes, monochrome first.</returns>
	private static List<ColorMode> ToColorModes
	(
		PrinterDescriptionAttributes attributes,
		PrintColorMode?              defaultColorMode
	)
	{
		PrintColorMode[]? supported = IppValueReader.ReadReference(attributes.PrintColorModeSupported);
		IEnumerable<ColorMode?> modes;

		if (supported is not null)
		{
			modes = supported.Select(IppCapabilitiesMapper.ToColorMode);
		}
		else if (IppValueReader.ReadValue(attributes.ColorSupported) is bool isColor)
		{
			modes = isColor
				? new ColorMode?[] { ColorMode.Monochrome, ColorMode.Color }
				: new ColorMode?[] { ColorMode.Monochrome };
		}
		else
		{
			modes = defaultColorMode is null
				? Array.Empty<ColorMode?>()
				: new ColorMode?[] { IppCapabilitiesMapper.ToColorMode(defaultColorMode.Value) };
		}

		return modes
			.OfType<ColorMode>()
			.Distinct()
			.Order()
			.ToList();
	}

	/// <summary>
	/// Maps a <c>print-color-mode</c> keyword. The monochrome and bi-level variants all become
	/// <see cref="ColorMode.Monochrome"/>.
	/// </summary>
	/// <param name="colorMode">The keyword.</param>
	/// <returns>The colour mode, or <see langword="null"/> for one with no equivalent, such as <c>auto</c>.</returns>
	private static ColorMode? ToColorMode(PrintColorMode colorMode)
	{
		return colorMode.Value switch
		{
			"color" => ColorMode.Color,
			"monochrome" or "process-monochrome" or "bi-level" or "process-bi-level" => ColorMode.Monochrome,
			_ => null,
		};
	}

	/// <summary>
	/// Reads <c>orientation-requested-supported</c>.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>Portrait and landscape, where supported.</returns>
	private static List<PageOrientation> ToOrientations(PrinterDescriptionAttributes attributes)
	{
		Orientation[] orientations = IppValueReader.ReadReference(attributes.OrientationRequestedSupported) ?? [];

		return orientations
			.Select(orientation => orientation switch
			{
				Orientation.Portrait => PageOrientation.Portrait,
				Orientation.Landscape => PageOrientation.Landscape,
				_ => (PageOrientation?)null,
			})
			.OfType<PageOrientation>()
			.Distinct()
			.ToList();
	}

	/// <summary>
	/// Reads <c>media-source-supported</c>.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>The trays, identified by keyword and named from it ("by-pass-tray" becomes "By pass tray").</returns>
	private static List<InputTray> ToInputTrays(PrinterDescriptionAttributes attributes)
	{
		MediaSource[] sources = IppValueReader.ReadReference(attributes.MediaSourceSupported) ?? [];

		return sources
			.Select(source => source.Value)
			.Where(keyword => !string.IsNullOrWhiteSpace(keyword))
			.Distinct(StringComparer.Ordinal)
			.Select(keyword => new InputTray
			{
				Id   = keyword,
				Name = IppKeywords.Describe(keyword)
			})
			.ToList();
	}
}
