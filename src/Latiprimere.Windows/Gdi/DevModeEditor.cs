using Latiprimere.Windows.Interop;

namespace Latiprimere.Windows.Gdi;

/// <summary>
/// Applies <see cref="PrintOptions"/> to a printer's DEVMODE.
/// </summary>
internal static class DevModeEditor
{
	/// <summary>
	/// Sets copies, duplex, colour, paper size, tray and orientation on a DEVMODE. Settings left at their defaults
	/// keep the printer's values, except orientation, which follows the document's first page when not chosen.
	/// </summary>
	/// <param name="devMode">The DEVMODE to change.</param>
	/// <param name="options">The print options.</param>
	/// <param name="paperId">The DMPAPER_* number of the chosen paper size, or <see langword="null"/>.</param>
	/// <param name="binId">The DMBIN_* number of the chosen tray, or <see langword="null"/>.</param>
	/// <param name="isFirstPageLandscape">Whether the document's first page is wider than it is tall.</param>
	internal static void Apply
	(
		ref DevMode  devMode,
		PrintOptions options,
		short?       paperId,
		short?       binId,
		bool         isFirstPageLandscape
	)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Copies, collated, so a multi-page document comes out in order
		devMode.Copies = (short)Math.Min(options.Copies, short.MaxValue);
		devMode.Fields |= DevModeConstants.FieldCopies;

		if (options.Copies > PrintOptions.MinimumCopies)
		{
			devMode.Collate = DevModeConstants.CollateTrue;
			devMode.Fields |= DevModeConstants.FieldCollate;
		}

		// Duplex, only when chosen
		short? duplex = DevModeEditor.ToDuplex(options.Duplex);

		if (duplex is not null)
		{
			devMode.Duplex = duplex.Value;
			devMode.Fields |= DevModeConstants.FieldDuplex;
		}

		// Colour, only when chosen
		short? color = DevModeEditor.ToColor(options.ColorMode);

		if (color is not null)
		{
			devMode.Color = color.Value;
			devMode.Fields |= DevModeConstants.FieldColor;
		}

		// Paper size, only when chosen; a length, width or form name left set would override it
		if (paperId is not null)
		{
			devMode.PaperSize = paperId.Value;
			devMode.Fields |= DevModeConstants.FieldPaperSize;
			devMode.Fields &= ~(DevModeConstants.FieldPaperLength | DevModeConstants.FieldPaperWidth | DevModeConstants.FieldFormName);
		}

		// Tray, only when chosen
		if (binId is not null)
		{
			devMode.DefaultSource = binId.Value;
			devMode.Fields |= DevModeConstants.FieldDefaultSource;
		}

		// Orientation: the caller's choice, otherwise the first page's
		devMode.Orientation = DevModeEditor.ToOrientation(options.Orientation, isFirstPageLandscape);
		devMode.Fields |= DevModeConstants.FieldOrientation;
	}

	/// <summary>
	/// Decides who makes the copies, after the driver has merged the DEVMODE: the driver, if it kept the count asked
	/// for, or Latiprimere otherwise, by rendering the document once per copy with the driver set to one copy.
	/// </summary>
	/// <param name="devMode">The merged DEVMODE, which is set to one copy if Latiprimere makes the copies.</param>
	/// <param name="requestedCopies">The number of copies asked for.</param>
	/// <returns>How many times to render the document.</returns>
	/// <remarks>
	/// A driver that can't make as many copies may reset the count to 1, or cap it at its maximum. Rendering every
	/// copy while a capped count is still set would print each copy that many times, so the count must be reset.
	/// </remarks>
	internal static int TakeOverCopies(ref DevMode devMode, int requestedCopies)
	{
		if (devMode.Copies >= requestedCopies)
		{
			return 1;
		}

		devMode.Copies = 1;
		devMode.Fields |= DevModeConstants.FieldCopies;

		return requestedCopies;
	}

	/// <summary>
	/// Maps a duplex mode to a DMDUP_* value.
	/// </summary>
	/// <param name="duplex">The duplex mode.</param>
	/// <returns>The value, or <see langword="null"/> to keep the printer's setting.</returns>
	internal static short? ToDuplex(DuplexMode duplex)
	{
		return duplex switch
		{
			DuplexMode.OneSided  => DevModeConstants.DuplexSimplex,
			DuplexMode.LongEdge  => DevModeConstants.DuplexVertical,
			DuplexMode.ShortEdge => DevModeConstants.DuplexHorizontal,
			_                    => null,
		};
	}

	/// <summary>
	/// Maps a colour mode to a DMCOLOR_* value.
	/// </summary>
	/// <param name="colorMode">The colour mode.</param>
	/// <returns>The value, or <see langword="null"/> to keep the printer's setting.</returns>
	internal static short? ToColor(ColorMode colorMode)
	{
		return colorMode switch
		{
			ColorMode.Monochrome => DevModeConstants.ColorMonochrome,
			ColorMode.Color      => DevModeConstants.ColorColor,
			_                    => null,
		};
	}

	/// <summary>
	/// Maps a page orientation to a DMORIENT_* value.
	/// </summary>
	/// <param name="orientation">The requested orientation.</param>
	/// <param name="isFirstPageLandscape">Whether the document's first page is landscape, used when none is
	/// requested.</param>
	/// <returns>The value.</returns>
	internal static short ToOrientation(PageOrientation orientation, bool isFirstPageLandscape)
	{
		return orientation switch
		{
			PageOrientation.Portrait  => DevModeConstants.OrientationPortrait,
			PageOrientation.Landscape => DevModeConstants.OrientationLandscape,
			_ when isFirstPageLandscape => DevModeConstants.OrientationLandscape,
			_ => DevModeConstants.OrientationPortrait,
		};
	}
}
