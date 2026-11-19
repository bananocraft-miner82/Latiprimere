using System.Globalization;
using Latiprimere.Ipp.Mapping;
using Latiprimere.Ipp.Resources;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Raster;

/// <summary>
/// Chooses how to send a PDF as raster, from the printer's attributes and the job's options (ADR 0035).
/// </summary>
internal static class RasterJobPlanner
{
	/// <summary>PWG raster's MIME type.</summary>
	internal const string PwgRasterMimeType = "image/pwg-raster";

	/// <summary>Apple raster's MIME type.</summary>
	internal const string AppleRasterMimeType = "image/urf";

	/// <summary>Without a default from the printer, the highest resolution used: finer costs time for little gain.</summary>
	internal const int PreferredMaxDpi = 600;

	/// <summary>
	/// The highest resolution ever used, whatever the printer lists or <see cref="IppNetworkOptions.MaxRenderDpi"/>
	/// allows: the printer's answer is untrusted, and pages grow with the square of the resolution.
	/// </summary>
	internal const int MaxDpi = 2400;

	/// <summary>The most pixels a page may be across or down, so a page size can't make rendering run without end.</summary>
	internal const int MaxPagePixels = 100_000;

	/// <summary>The printer attributes a raster job is planned from.</summary>
	internal static readonly string[] Attributes =
	[
		"pwg-raster-document-resolution-supported",
		"pwg-raster-document-type-supported",
		"pwg-raster-document-sheet-back",
		"urf-supported",
		"printer-resolution-default",
		"media-default",
		"sides-default",
		"print-color-mode-default",
		"compression-supported",
	];

	/// <summary>
	/// Chooses the raster format to send a PDF in: PWG raster, then Apple raster (decision D2).
	/// </summary>
	/// <param name="supportedFormats">The MIME types the printer accepts.</param>
	/// <returns>The format, or <see langword="null"/> if it takes neither.</returns>
	internal static RasterFormat? ChooseFormat(IEnumerable<string> supportedFormats)
	{
		ArgumentNullException.ThrowIfNull(supportedFormats);

		HashSet<string> formats = new HashSet<string>(supportedFormats, StringComparer.OrdinalIgnoreCase);

		if (formats.Contains(RasterJobPlanner.PwgRasterMimeType))
		{
			return RasterFormat.PwgRaster;
		}

		return formats.Contains(RasterJobPlanner.AppleRasterMimeType) ? RasterFormat.AppleRaster : null;
	}

	/// <summary>
	/// Plans a raster job.
	/// </summary>
	/// <param name="format">The raster format, from <see cref="ChooseFormat"/>.</param>
	/// <param name="attributes">The printer's <see cref="Attributes"/>, as encoded.</param>
	/// <param name="options">The job's options.</param>
	/// <param name="maxRenderDpi">The highest resolution to choose, or <see langword="null"/> for no limit.</param>
	/// <returns>The plan.</returns>
	/// <exception cref="NotSupportedException">The printer lists no resolution or colour space Latiprimere can use
	/// in the format, or its paper size can't be worked out.</exception>
	internal static RasterJobPlan Plan(RasterFormat format, IReadOnlyList<IppAttribute> attributes, PrintOptions options, int? maxRenderDpi)
	{
		ArgumentNullException.ThrowIfNull(attributes);
		ArgumentNullException.ThrowIfNull(options);

		Dictionary<string, List<IppAttribute>> byName = IppVendorOptionMapper.GroupTopLevel(attributes);
		string mimeType = format == RasterFormat.PwgRaster ? RasterJobPlanner.PwgRasterMimeType : RasterJobPlanner.AppleRasterMimeType;

		// Apple raster lists what it takes as keywords in one attribute, such as RS300-600, W8, SRGB24 and DM3
		List<string> urf = format == RasterFormat.AppleRaster
			? RasterJobPlanner.Keywords(byName, "urf-supported")
				.SelectMany(keyword => keyword.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				.ToList()
			: [];

		List<int> resolutions = format == RasterFormat.PwgRaster
			? RasterJobPlanner.PwgResolutions(byName)
			: RasterJobPlanner.UrfResolutions(urf);

		if (resolutions.Count == 0)
		{
			throw new NotSupportedException(ExceptionMessages.FormatRasterNoResolution(mimeType));
		}

		int? defaultDpi = RasterJobPlanner.ReadResolutions(byName, "printer-resolution-default").Cast<int?>().FirstOrDefault();

		DuplexMode duplex = options.Duplex != DuplexMode.PrinterDefault
			? options.Duplex
			: RasterJobPlanner.Keywords(byName, "sides-default").FirstOrDefault() switch
			{
				"two-sided-long-edge"  => DuplexMode.LongEdge,
				"two-sided-short-edge" => DuplexMode.ShortEdge,
				_                      => DuplexMode.OneSided,
			};

		RasterSettings settings = new RasterSettings
		{
			Format      = format,
			ColorSpace  = RasterJobPlanner.ChooseColorSpace(format, byName, urf, options.ColorMode, mimeType),
			Dpi         = RasterJobPlanner.ChooseDpi(resolutions, defaultDpi, maxRenderDpi),
			Media       = RasterJobPlanner.ChooseMedia(byName, options.MediaSize),
			Duplex      = duplex,
			BackSide    = format == RasterFormat.PwgRaster ? RasterJobPlanner.PwgBackSide(byName) : RasterJobPlanner.UrfBackSide(urf),
			Orientation = options.Orientation
		};

		RasterJobPlanner.EnsurePageFits(settings.Media, settings.Dpi);

		return new RasterJobPlan
		{
			MimeType = mimeType,
			Settings = settings,
			Gzip     = RasterJobPlanner.Keywords(byName, "compression-supported").Contains("gzip", StringComparer.Ordinal)
		};
	}

	/// <summary>
	/// Chooses the resolution: the printer's default, unless above the limit; otherwise the highest it lists up to
	/// the limit and <see cref="PreferredMaxDpi"/>; and if it lists none that low, its lowest.
	/// </summary>
	/// <param name="resolutions">The resolutions it lists for the format, in dots per inch.</param>
	/// <param name="defaultDpi">Its default resolution, or <see langword="null"/>.</param>
	/// <param name="maxRenderDpi">The limit, or <see langword="null"/> for none.</param>
	/// <returns>The resolution.</returns>
	internal static int ChooseDpi(IReadOnlyList<int> resolutions, int? defaultDpi, int? maxRenderDpi)
	{
		List<int> allowed = resolutions.Where(dpi => maxRenderDpi is null || dpi <= maxRenderDpi).Order().ToList();

		if (allowed.Count == 0)
		{
			return resolutions.Min();
		}

		if (defaultDpi is not null && allowed.Contains(defaultDpi.Value))
		{
			return defaultDpi.Value;
		}

		List<int> preferred = allowed.Where(dpi => dpi <= RasterJobPlanner.PreferredMaxDpi).ToList();

		return preferred.Count > 0 ? preferred[^1] : allowed[0];
	}

	/// <summary>
	/// Chooses the colour space: colour when asked for, or by the printer's default, and it takes colour; grey
	/// otherwise; 1-bit black when that is all it takes, or its default is bi-level and it takes it.
	/// </summary>
	/// <param name="format">The raster format.</param>
	/// <param name="byName">The printer's attributes, by name.</param>
	/// <param name="urf">Apple raster's keywords.</param>
	/// <param name="colorMode">The job's colour mode.</param>
	/// <param name="mimeType">The format's MIME type, for the message.</param>
	/// <returns>The colour space.</returns>
	/// <exception cref="NotSupportedException">The printer lists none Latiprimere can send.</exception>
	private static RasterColorSpace ChooseColorSpace
	(
		RasterFormat                             format,
		Dictionary<string, List<IppAttribute>>   byName,
		List<string>                             urf,
		ColorMode                                colorMode,
		string                                   mimeType
	)
	{
		bool srgb;
		bool gray;
		bool black;

		if (format == RasterFormat.PwgRaster)
		{
			List<string> types = RasterJobPlanner.Keywords(byName, "pwg-raster-document-type-supported");
			srgb = types.Contains("srgb_8", StringComparer.Ordinal);
			gray = types.Contains("sgray_8", StringComparer.Ordinal);
			black = types.Contains("black_1", StringComparer.Ordinal);
		}
		else
		{
			// W8 or SRGB24, possibly with other depths after a dash, as W8-16
			srgb = urf.Any(keyword => keyword == "SRGB24" || keyword.StartsWith("SRGB24-", StringComparison.Ordinal));
			gray = urf.Any(keyword => keyword == "W8" || keyword.StartsWith("W8-", StringComparison.Ordinal));
			black = false;
		}

		string printerDefault = RasterJobPlanner.Keywords(byName, "print-color-mode-default").FirstOrDefault() ?? string.Empty;
		bool defaultIsMonochrome = printerDefault.Contains("monochrome", StringComparison.Ordinal) || printerDefault.Contains("bi-level", StringComparison.Ordinal);
		bool wantsColor = colorMode == ColorMode.Color || (colorMode == ColorMode.PrinterDefault && !defaultIsMonochrome);
		bool wantsBlack = colorMode == ColorMode.PrinterDefault && printerDefault.Contains("bi-level", StringComparison.Ordinal);

		if (wantsColor && srgb)
		{
			return RasterColorSpace.Srgb8;
		}

		if (wantsBlack && black)
		{
			return RasterColorSpace.Black1;
		}

		if (gray)
		{
			return RasterColorSpace.Gray8;
		}

		if (black)
		{
			return RasterColorSpace.Black1;
		}

		// A printer that takes only colour: grey pages would be sent as colour anyway
		if (srgb)
		{
			return RasterColorSpace.Srgb8;
		}

		throw new NotSupportedException(ExceptionMessages.FormatRasterNoColorSpace(mimeType));
	}

	/// <summary>
	/// Chooses the paper: the job's, or the printer's default.
	/// </summary>
	/// <param name="byName">The printer's attributes, by name.</param>
	/// <param name="chosen">The job's paper size, or <see langword="null"/>.</param>
	/// <returns>The paper size.</returns>
	/// <exception cref="NotSupportedException">No paper was chosen, and the printer's default doesn't give its
	/// size.</exception>
	private static MediaSize ChooseMedia(Dictionary<string, List<IppAttribute>> byName, MediaSize? chosen)
	{
		if (chosen is not null)
		{
			return chosen;
		}

		string? name = RasterJobPlanner.Keywords(byName, "media-default").FirstOrDefault();
		MediaSize? media = name is null ? null : PwgMediaNames.ToMediaSize(name);

		return media ?? throw new NotSupportedException(ExceptionMessages.FormatRasterMediaUnknown(name ?? string.Empty));
	}

	/// <summary>
	/// Reads how a PWG raster printer wants back sides of two-sided jobs.
	/// </summary>
	/// <param name="byName">The printer's attributes, by name.</param>
	/// <returns>The back side; normal if it doesn't say.</returns>
	private static RasterBackSide PwgBackSide(Dictionary<string, List<IppAttribute>> byName)
	{
		return RasterJobPlanner.Keywords(byName, "pwg-raster-document-sheet-back").FirstOrDefault() switch
		{
			"flipped"       => RasterBackSide.Flipped,
			"rotated"       => RasterBackSide.Rotated,
			"manual-tumble" => RasterBackSide.ManualTumble,
			_               => RasterBackSide.Normal,
		};
	}

	/// <summary>
	/// Reads how an Apple raster printer wants back sides of two-sided jobs: <c>DM1</c> to <c>DM4</c>.
	/// </summary>
	/// <param name="urf">Its keywords.</param>
	/// <returns>The back side; normal if it doesn't say.</returns>
	private static RasterBackSide UrfBackSide(List<string> urf)
	{
		return urf.FirstOrDefault(keyword => keyword.StartsWith("DM", StringComparison.Ordinal)) switch
		{
			"DM2" => RasterBackSide.Flipped,
			"DM3" => RasterBackSide.Rotated,
			"DM4" => RasterBackSide.ManualTumble,
			_     => RasterBackSide.Normal,
		};
	}

	/// <summary>
	/// Reads the square resolutions a PWG raster printer lists.
	/// </summary>
	/// <param name="byName">The printer's attributes, by name.</param>
	/// <returns>The resolutions, in dots per inch.</returns>
	private static List<int> PwgResolutions(Dictionary<string, List<IppAttribute>> byName)
	{
		return RasterJobPlanner.ReadResolutions(byName, "pwg-raster-document-resolution-supported").Distinct().ToList();
	}

	/// <summary>
	/// Reads the resolutions an Apple raster printer lists, as <c>RS300-600</c>.
	/// </summary>
	/// <param name="urf">Its keywords.</param>
	/// <returns>The resolutions, in dots per inch.</returns>
	private static List<int> UrfResolutions(List<string> urf)
	{
		string? keyword = urf.FirstOrDefault(value => value.StartsWith("RS", StringComparison.Ordinal));

		if (keyword is null)
		{
			return [];
		}

		return keyword[2..]
			.Split('-', StringSplitOptions.RemoveEmptyEntries)
			.Select(value => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int dpi) ? dpi : 0)
			.Where(dpi => dpi is > 0 and <= RasterJobPlanner.MaxDpi)
			.Distinct()
			.ToList();
	}

	/// <summary>
	/// Reads an attribute's square resolutions, in dots per inch, up to <see cref="MaxDpi"/>; others are left out.
	/// </summary>
	/// <param name="byName">The printer's attributes, by name.</param>
	/// <param name="name">The attribute.</param>
	/// <returns>The resolutions.</returns>
	private static IEnumerable<int> ReadResolutions(Dictionary<string, List<IppAttribute>> byName, string name)
	{
		const double centimetresPerInch = 2.54;

		// Converted as double, as a printer's huge dots per centimetre would overflow an int
		return byName.GetValueOrDefault(name, [])
			.Select(attribute => attribute.Value)
			.OfType<Resolution>()
			.Where(resolution => resolution.Width == resolution.Height && resolution.Width > 0)
			.Select(resolution => resolution.Units == ResolutionUnit.DotsPerCm
				? Math.Round(resolution.Width * centimetresPerInch, MidpointRounding.AwayFromZero)
				: resolution.Width)
			.Where(dpi => dpi <= RasterJobPlanner.MaxDpi)
			.Select(dpi => (int)dpi);
	}

	/// <summary>
	/// Checks a page can be rendered: no more than <see cref="MaxPagePixels"/> across or down.
	/// </summary>
	/// <param name="media">The paper, chosen by the application or the printer's default.</param>
	/// <param name="dpi">The resolution.</param>
	/// <exception cref="NotSupportedException">The page would be larger.</exception>
	private static void EnsurePageFits(MediaSize media, int dpi)
	{
		const double millimetresPerInch = 25.4;

		// As double, since a printer's paper size, or the application's, could overflow integer pixels
		double longest = Math.Max(media.WidthMillimeters, media.HeightMillimeters) / millimetresPerInch * dpi;

		if (!(longest <= RasterJobPlanner.MaxPagePixels))
		{
			string message = ExceptionMessages.FormatRasterPageTooLarge
			(
				media.WidthMillimeters, media.HeightMillimeters, dpi, RasterJobPlanner.MaxPagePixels
			);

			throw new NotSupportedException(message);
		}
	}

	/// <summary>
	/// Reads an attribute's keyword or name values.
	/// </summary>
	/// <param name="byName">The printer's attributes, by name.</param>
	/// <param name="name">The attribute.</param>
	/// <returns>Its values; empty if absent.</returns>
	private static List<string> Keywords(Dictionary<string, List<IppAttribute>> byName, string name)
	{
		return byName.GetValueOrDefault(name, [])
			.Select
			(
				attribute => attribute.Value switch
				{
					string text => text,
					StringWithLanguage text => text.Value,
					_ => null,
				}
			)
			.OfType<string>()
			.ToList();
	}
}
