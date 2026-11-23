using Latiprimere.Ipp.Resources;
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

		// Paper: media-source and media-type exist only inside media-col, and media and media-col can't both be sent,
		// so a tray or a type takes the size into media-col with it, as does a custom size, whose name a printer may
		// not know; a listed size alone goes as the simpler media keyword
		bool isCustomSize = options.MediaSize?.Name.StartsWith(IppJobTemplateMapper.CustomSizePrefix, StringComparison.Ordinal) == true;

		if (options.InputTray is not null || options.MediaType is not null || isCustomSize)
		{
			attributes.MediaCol = new IppValue<MediaCol>(IppJobTemplateMapper.ToMediaCol(options.InputTray, options.MediaSize, options.MediaType));
		}
		else if (options.MediaSize is not null)
		{
			attributes.Media = new IppValue<Media>(new Media(options.MediaSize.Name, true));
		}

		// The settings added before 1.0 (ADR 0056), each only when chosen
		if (options.PageRanges.Count > 0)
		{
			attributes.PageRanges = new IppValue<IppRange[]>(options.PageRanges.Select(range => new IppRange(range.First, range.Last)).ToArray());
		}

		if (options.Collate is bool collate)
		{
			MultipleDocumentHandling handling = collate
				? MultipleDocumentHandling.SeparateDocumentsCollatedCopies
				: MultipleDocumentHandling.SeparateDocumentsUncollatedCopies;

			attributes.MultipleDocumentHandling = new IppValue<MultipleDocumentHandling>(handling);
		}

		SharpIpp.Protocol.Models.PrintQuality? quality = IppJobTemplateMapper.ToPrintQuality(options.Quality);

		if (quality is not null)
		{
			attributes.PrintQuality = new IppValue<SharpIpp.Protocol.Models.PrintQuality>(quality.Value);
		}

		if (options.Finishings.Count > 0)
		{
			attributes.Finishings = new IppValue<Finishings[]>(options.Finishings.Select(IppJobTemplateMapper.ToFinishings).ToArray());
		}

		if (options.OutputBin is not null)
		{
			attributes.OutputBin = new IppValue<SharpIpp.Protocol.Models.OutputBin>(new SharpIpp.Protocol.Models.OutputBin(options.OutputBin.Id, true));
		}

		return attributes;
	}

	/// <summary>The start of the PWG name of a size without a standard name.</summary>
	internal const string CustomSizePrefix = "custom_";

	/// <summary>
	/// Maps a print quality to IPP's <c>print-quality</c>.
	/// </summary>
	/// <param name="quality">The quality.</param>
	/// <returns>The IPP value, or <see langword="null"/> for the printer's default.</returns>
	internal static SharpIpp.Protocol.Models.PrintQuality? ToPrintQuality(PrintQuality quality)
	{
		return quality switch
		{
			PrintQuality.Draft  => SharpIpp.Protocol.Models.PrintQuality.Draft,
			PrintQuality.Normal => SharpIpp.Protocol.Models.PrintQuality.Normal,
			PrintQuality.High   => SharpIpp.Protocol.Models.PrintQuality.High,
			_                   => null,
		};
	}

	/// <summary>
	/// Maps a print quality back from IPP's <c>print-quality</c>.
	/// </summary>
	/// <param name="quality">The IPP value.</param>
	/// <returns>The quality.</returns>
	internal static PrintQuality ToPrintQuality(SharpIpp.Protocol.Models.PrintQuality quality)
	{
		return quality switch
		{
			SharpIpp.Protocol.Models.PrintQuality.Draft => PrintQuality.Draft,
			SharpIpp.Protocol.Models.PrintQuality.High  => PrintQuality.High,
			_                                           => PrintQuality.Normal,
		};
	}

	/// <summary>
	/// Maps a finishing to IPP's <c>finishings</c> enum (PWG 5100.1).
	/// </summary>
	/// <param name="finishing">The finishing.</param>
	/// <returns>The IPP value.</returns>
	/// <exception cref="NotSupportedException">The finishing isn't one this version knows.</exception>
	internal static Finishings ToFinishings(Finishing finishing)
	{
		return finishing switch
		{
			Finishing.Staple            => Finishings.Staple,
			Finishing.StapleTopLeft     => Finishings.StapleTopLeft,
			Finishing.StapleBottomLeft  => Finishings.StapleBottomLeft,
			Finishing.StapleTopRight    => Finishings.StapleTopRight,
			Finishing.StapleBottomRight => Finishings.StapleBottomRight,
			Finishing.StapleDualLeft    => Finishings.StapleDualLeft,
			Finishing.StapleDualTop     => Finishings.StapleDualTop,
			Finishing.Punch             => Finishings.Punch,
			Finishing.PunchDualLeft     => Finishings.PunchDualLeft,
			Finishing.PunchDualTop      => Finishings.PunchDualTop,
			Finishing.Fold              => Finishings.Fold,
			_                           => throw new NotSupportedException(ExceptionMessages.FormatFinishingNotSupported(finishing)),
		};
	}

	/// <summary>
	/// Maps IPP's <c>finishings</c> enum to a finishing, for those <see cref="Finishing"/> names.
	/// </summary>
	/// <param name="finishings">The IPP value.</param>
	/// <returns>The finishing, or <see langword="null"/> for one it doesn't name.</returns>
	internal static Finishing? ToFinishing(Finishings finishings)
	{
		return finishings switch
		{
			Finishings.Staple            => Finishing.Staple,
			Finishings.StapleTopLeft     => Finishing.StapleTopLeft,
			Finishings.StapleBottomLeft  => Finishing.StapleBottomLeft,
			Finishings.StapleTopRight    => Finishing.StapleTopRight,
			Finishings.StapleBottomRight => Finishing.StapleBottomRight,
			Finishings.StapleDualLeft    => Finishing.StapleDualLeft,
			Finishings.StapleDualTop     => Finishing.StapleDualTop,
			Finishings.Punch             => Finishing.Punch,
			Finishings.PunchDualLeft     => Finishing.PunchDualLeft,
			Finishings.PunchDualTop      => Finishing.PunchDualTop,
			Finishings.Fold              => Finishing.Fold,
			_                            => null,
		};
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
	/// Builds the <c>media-col</c> collection for a tray, a kind of paper or a custom size, with the paper size when
	/// one is chosen.
	/// </summary>
	/// <param name="tray">The tray, whose ID is a <c>media-source</c> keyword, or <see langword="null"/>.</param>
	/// <param name="size">The paper size, or <see langword="null"/> to leave it to the printer.</param>
	/// <param name="type">The kind of paper, whose ID is a <c>media-type</c> keyword, or <see langword="null"/>.</param>
	/// <returns>The collection.</returns>
	/// <remarks>
	/// The size goes as <c>media-size</c> dimensions rather than <c>media-size-name</c>: IPP Everywhere requires
	/// printers to support the dimensions, but not the name.
	/// </remarks>
	internal static MediaCol ToMediaCol(InputTray? tray, MediaSize? size, MediaType? type = null)
	{
		MediaCol mediaCol = new MediaCol();

		if (tray is not null)
		{
			mediaCol.MediaSource = new IppValue<MediaSource>(new MediaSource(tray.Id));
		}

		if (type is not null)
		{
			mediaCol.MediaType = new IppValue<SharpIpp.Protocol.Models.MediaType>(new SharpIpp.Protocol.Models.MediaType(type.Id));
		}

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

	/// <summary>
	/// Maps how an image is sized to the IPP <c>print-scaling</c> keyword.
	/// </summary>
	/// <param name="scaling">The scaling.</param>
	/// <returns>The <c>print-scaling</c> value, or <see langword="null"/> to use the printer's default.</returns>
	internal static PrintScaling? ToPrintScaling(ScalingMode scaling)
	{
		return scaling switch
		{
			ScalingMode.Fit  => PrintScaling.Fit,
			ScalingMode.Fill => PrintScaling.Fill,
			ScalingMode.None => PrintScaling.None,
			_                => (PrintScaling?)null,
		};
	}
}
