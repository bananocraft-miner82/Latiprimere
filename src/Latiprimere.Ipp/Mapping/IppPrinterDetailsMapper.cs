using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Maps a printer's raw IPP attributes to <see cref="PrinterDetails"/> (ADR 0059).
/// </summary>
internal static class IppPrinterDetailsMapper
{
	/// <summary>
	/// Creates a printer's details from its attributes.
	/// </summary>
	/// <param name="attributes">The printer's attributes, as they were encoded: those of
	/// <see cref="IppRequestedAttributes.DetailAttributes"/>, and any others, which are ignored.</param>
	/// <param name="printerId">The Latiprimere identifier of the printer.</param>
	/// <returns>The details.</returns>
	internal static PrinterDetails ToDetails(IReadOnlyList<IppAttribute> attributes, string printerId)
	{
		ArgumentNullException.ThrowIfNull(attributes);

		Dictionary<string, List<IppAttribute>> byName = IppVendorOptionMapper.GroupTopLevel(attributes);

		return new PrinterDetails
		{
			PrinterId       = printerId,
			WebPage         = IppPrinterDetailsMapper.ReadWebAddresses(byName, IppRequestedAttributes.PrinterMoreInfo).FirstOrDefault(),
			SuppliesWebPage = IppPrinterDetailsMapper.ReadWebAddresses(byName, IppRequestedAttributes.PrinterSupplyInfoUri).FirstOrDefault(),
			Icons           = IppPrinterDetailsMapper.ReadWebAddresses(byName, IppRequestedAttributes.PrinterIcons),
			Firmware        = IppPrinterDetailsMapper.ReadFirmware(byName),
			IdentifyActions = IppPrinterDetailsMapper.ReadIdentifyActions(byName)
		};
	}

	/// <summary>
	/// Maps an IPP <c>identify-actions</c> keyword.
	/// </summary>
	/// <param name="keyword">The keyword.</param>
	/// <returns>The action, or <see langword="null"/> for one Latiprimere doesn't know.</returns>
	internal static IdentifyAction? ToIdentifyAction(string? keyword)
	{
		return keyword switch
		{
			"display" => IdentifyAction.Display,
			"flash"   => IdentifyAction.Flash,
			"sound"   => IdentifyAction.Sound,
			"speak"   => IdentifyAction.Speak,
			_         => null,
		};
	}

	/// <summary>
	/// Reads an attribute's values that are absolute <c>http</c> or <c>https</c> URIs, leaving out any other: they
	/// come from the printer, and an application may fetch them.
	/// </summary>
	/// <param name="byName">The attributes, by name.</param>
	/// <param name="name">The attribute's name.</param>
	/// <returns>The URIs, in the printer's order, without repeats.</returns>
	private static List<Uri> ReadWebAddresses(Dictionary<string, List<IppAttribute>> byName, string name)
	{
		if (!byName.TryGetValue(name, out List<IppAttribute>? values))
		{
			return [];
		}

		return values
			.Select(attribute => IppPrinterDetailsMapper.ToUri(attribute.Value))
			.OfType<Uri>()
			.Where(uri => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
			.Distinct()
			.ToList();
	}

	/// <summary>
	/// Pairs the firmware's names with their versions by position, leaving out a name without a version and a version
	/// without a name.
	/// </summary>
	/// <param name="byName">The attributes, by name.</param>
	/// <returns>The firmware's parts.</returns>
	private static List<PrinterFirmware> ReadFirmware(Dictionary<string, List<IppAttribute>> byName)
	{
		List<string?> names = IppPrinterDetailsMapper.ReadTexts(byName, IppRequestedAttributes.PrinterFirmwareName);
		List<string?> versions = IppPrinterDetailsMapper.ReadTexts(byName, IppRequestedAttributes.PrinterFirmwareStringVersion);
		List<PrinterFirmware> firmware = [];

		for (int index = 0; index < Math.Min(names.Count, versions.Count); index++)
		{
			string? name = names[index];
			string? version = versions[index];

			if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(version))
			{
				firmware.Add(new PrinterFirmware { Name = name.Trim(), Version = version.Trim() });
			}
		}

		return firmware;
	}

	/// <summary>
	/// Reads the identify actions the printer lists, without repeats or keywords Latiprimere doesn't know.
	/// </summary>
	/// <param name="byName">The attributes, by name.</param>
	/// <returns>The actions, in the printer's order.</returns>
	private static List<IdentifyAction> ReadIdentifyActions(Dictionary<string, List<IppAttribute>> byName)
	{
		return IppPrinterDetailsMapper.ReadTexts(byName, IppRequestedAttributes.IdentifyActionsSupported)
			.Select(IppPrinterDetailsMapper.ToIdentifyAction)
			.OfType<IdentifyAction>()
			.Distinct()
			.ToList();
	}

	/// <summary>
	/// Reads an attribute's values as text, keeping each one's position: a value that isn't text is
	/// <see langword="null"/>, so values of two attributes can be paired.
	/// </summary>
	/// <param name="byName">The attributes, by name.</param>
	/// <param name="name">The attribute's name.</param>
	/// <returns>The values.</returns>
	private static List<string?> ReadTexts(Dictionary<string, List<IppAttribute>> byName, string name)
	{
		if (!byName.TryGetValue(name, out List<IppAttribute>? values))
		{
			return [];
		}

		return values
			.Select
			(
				attribute => attribute.Value switch
				{
					string text              => text,
					StringWithLanguage text  => text.Value,
					_                        => (string?)null,
				}
			)
			.ToList();
	}

	/// <summary>
	/// Reads a URI value, which SharpIppNext may give as a <see cref="Uri"/> or as text.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>The absolute URI, or <see langword="null"/> if it isn't one.</returns>
	private static Uri? ToUri(object? value)
	{
		return value switch
		{
			Uri uri when uri.IsAbsoluteUri                                                    => uri,
			string text when Uri.TryCreate(text, UriKind.Absolute, out Uri? parsed)           => parsed,
			_                                                                                 => null,
		};
	}
}
