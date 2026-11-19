using SharpIpp.Protocol.Models;
using IppMediaSize = SharpIpp.Protocol.Models.MediaSize;
using IppRange = SharpIpp.Protocol.Models.Range;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Maps <see cref="PrintOptions"/> to IPP job template attributes.
/// </summary>
/// <remarks>
/// Settings left at their defaults are not sent, so the printer applies its own defaults and is not asked about
/// attributes it may not support.
/// </remarks>
internal static class IppJobTemplateMapper
{
	/// <summary>Hundredths of a millimetre in a millimetre, the unit of IPP <c>media-size</c>.</summary>
	private const double HundredthsPerMillimetre = 100.0;

	/// <summary>
	/// Creates the job template attributes for a set of print options.
	/// </summary>
	/// <param name="options">The print options.</param>
	/// <returns>The job template attributes.</returns>
	internal static JobTemplateAttributes ToJobTemplateAttributes(PrintOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		JobTemplateAttributes attributes = new JobTemplateAttributes();

		// Copies: only when more than the default single copy is wanted
		if (options.Copies > PrintOptions.MinimumCopies)
		{
			attributes.Copies = new IppValue<int>(options.Copies);
		}

		// Sides and orientation: only when the caller chose one
		Sides? sides = IppJobTemplateMapper.ToSides(options.Duplex);

		if (sides is not null)
		{
			attributes.Sides = new IppValue<Sides>(sides.Value);
		}

		Orientation? orientation = IppJobTemplateMapper.ToOrientation(options.Orientation);

		if (orientation is not null)
		{
			attributes.OrientationRequested = new IppValue<Orientation>(orientation.Value);
		}

		// Colour mode: only when the caller chose one
		PrintColorMode? colorMode = IppJobTemplateMapper.ToPrintColorMode(options.ColorMode);

		if (colorMode is not null)
		{
			attributes.PrintColorMode = new IppValue<PrintColorMode>(colorMode.Value);
		}

		// Paper: media-source exists only inside media-col, and media and media-col can't both be sent, so a tray
		// takes the size into media-col with it; a size alone goes as the simpler media keyword
		if (options.InputTray is not null)
		{
			attributes.MediaCol = new IppValue<MediaCol>(IppJobTemplateMapper.ToMediaCol(options.InputTray, options.MediaSize));
		}
		else if (options.MediaSize is not null)
		{
			attributes.Media = new IppValue<Media>(new Media(options.MediaSize.Name, true));
		}

		return attributes;
	}

	/// <summary>
	/// Maps a colour mode to the IPP <c>print-color-mode</c> keyword.
	/// </summary>
	/// <param name="colorMode">The colour mode.</param>
	/// <returns>The <c>print-color-mode</c> value, or <see langword="null"/> to use the printer's default.</returns>
	internal static PrintColorMode? ToPrintColorMode(ColorMode colorMode)
	{
		return colorMode switch
		{
			ColorMode.Monochrome => PrintColorMode.Monochrome,
			ColorMode.Color      => PrintColorMode.Color,
			_                    => (PrintColorMode?)null,
		};
	}

	/// <summary>
	/// Builds the <c>media-col</c> collection for a tray, with the paper size when one is chosen.
	/// </summary>
	/// <param name="tray">The tray, whose ID is a <c>media-source</c> keyword.</param>
	/// <param name="size">The paper size, or <see langword="null"/> to leave it to the printer.</param>
	/// <returns>The collection.</returns>
	/// <remarks>
	/// The size goes as <c>media-size</c> dimensions rather than <c>media-size-name</c>: IPP Everywhere requires
	/// printers to support the dimensions, but not the name.
	/// </remarks>
	internal static MediaCol ToMediaCol(InputTray tray, MediaSize? size)
	{
		ArgumentNullException.ThrowIfNull(tray);

		MediaCol mediaCol = new MediaCol
		{
			MediaSource = new IppValue<MediaSource>(new MediaSource(tray.Id))
		};

		if (size is not null)
		{
			mediaCol.MediaSize = new IppValue<IppMediaSize>
			(
				new IppMediaSize
				{
					XDimension = IppJobTemplateMapper.ToDimension(size.WidthMillimeters),
					YDimension = IppJobTemplateMapper.ToDimension(size.HeightMillimeters)
				}
			);
		}

		return mediaCol;
	}

	/// <summary>
	/// Converts millimetres to an IPP <c>media-size</c> dimension, in hundredths of a millimetre.
	/// </summary>
	/// <param name="millimetres">The length in millimetres.</param>
	/// <returns>The dimension. SharpIppNext sends a range with equal bounds as a single integer.</returns>
	private static IppValue<IppRange> ToDimension(double millimetres)
	{
		int hundredths = (int)Math.Round(millimetres * IppJobTemplateMapper.HundredthsPerMillimetre, MidpointRounding.AwayFromZero);

		return new IppValue<IppRange>(new IppRange(hundredths, hundredths));
	}

	/// <summary>
	/// Maps a duplex mode to the IPP <c>sides</c> keyword.
	/// </summary>
	/// <param name="duplex">The duplex mode.</param>
	/// <returns>The <c>sides</c> value, or <see langword="null"/> to use the printer's default.</returns>
	internal static Sides? ToSides(DuplexMode duplex)
	{
		return duplex switch
		{
			DuplexMode.OneSided  => Sides.OneSided,
			DuplexMode.LongEdge  => Sides.TwoSidedLongEdge,
			DuplexMode.ShortEdge => Sides.TwoSidedShortEdge,
			_                    => (Sides?)null,
		};
	}

	/// <summary>
	/// Maps a page orientation to the IPP <c>orientation-requested</c> enum.
	/// </summary>
	/// <param name="orientation">The page orientation.</param>
	/// <returns>The <c>orientation-requested</c> value, or <see langword="null"/> to use the printer's
	/// default.</returns>
	internal static Orientation? ToOrientation(PageOrientation orientation)
	{
		return orientation switch
		{
			PageOrientation.Portrait  => Orientation.Portrait,
			PageOrientation.Landscape => Orientation.Landscape,
			_                         => null,
		};
	}
}
