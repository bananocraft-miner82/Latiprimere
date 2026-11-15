using SharpIpp.Protocol.Models;

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

		return attributes;
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
