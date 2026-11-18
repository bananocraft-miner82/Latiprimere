using System.Globalization;
using SharpIpp.Protocol.Models;
using IppRange = SharpIpp.Protocol.Models.Range;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Lists an IPP printer's job attributes as <see cref="VendorOption"/>s, with the values it supports (ADR 0032).
/// </summary>
/// <remarks>
/// <para>
/// The attributes are those the printer lists in <c>job-creation-attributes-supported</c>; for a printer that
/// doesn't send it, those with both a <c>-supported</c> and a <c>-default</c> attribute. Each is offered under its
/// <c>ipp:</c> key, with values written as <see cref="IppVendorAttributeMapper"/> reads them back: so every listed
/// value can be sent as it is.
/// </para>
/// <para>
/// Left out: attributes the typed options send (<c>copies</c>, <c>sides</c>, <c>media</c> and the rest), operation
/// attributes such as <c>document-name</c>, and attributes whose values vendor options can't express, such as
/// collections and page ranges.
/// </para>
/// </remarks>
internal static class IppVendorOptionMapper
{
	/// <summary>Lists the attributes a printer accepts when a job is created (PWG 5100.11).</summary>
	internal const string JobCreationAttributesSupported = "job-creation-attributes-supported";

	private const string SupportedSuffix = "-supported";        // A printer's supported values for an attribute
	private const string DefaultSuffix = "-default";            // A printer's default value for an attribute
	private const string CollectionSuffix = "-col";             // Collection attributes, which values can't express
	private const string JobPriority = "job-priority";          // Its -supported value is a count of levels
	private const string PrintQuality = "print-quality";        // An enum with names (RFC 8011)
	private const string Finishings = "finishings";             // An enum with names, which takes several values
	private const int LowestPriority = 1;                       // job-priority's range (RFC 8011)
	private const int HighestPriority = 100;

	/// <summary>Attributes the typed options or every request send, or that only describe the request.</summary>
	private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.Ordinal)
	{
		"copies",
		"sides",
		"media",
		"media-col",
		"media-source",
		"orientation-requested",
		"print-color-mode",
		"job-name",
		"ipp-attribute-fidelity",
		"job-mandatory-attributes",
		"compression",
		"document-format",
		"document-name",
		"document-natural-language",
		"document-access",
		"document-charset",
		"document-message",
		"document-metadata",
		"document-password",
		"overrides",
		"page-ranges",
		"job-k-octets",
		"job-impressions",
		"job-media-sheets",
	};

	/// <summary>Set attributes a job may give several values of, separated by commas.</summary>
	private static readonly HashSet<string> SetAttributes = new HashSet<string>(StringComparer.Ordinal)
	{
		IppVendorOptionMapper.Finishings,
	};

	/// <summary>Names for attributes whose keyword reads poorly as words.</summary>
	private static readonly Dictionary<string, string> AttributeNames = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["finishings"] = "Finishing",
		["job-sheets"] = "Banner pages",
		["number-up"] = "Pages per sheet",
		["printer-resolution"] = "Resolution",
		["print-content-optimize"] = "Optimise for",
		["job-hold-until"] = "Hold until",
	};

	/// <summary>Names for the enum values of <c>print-quality</c> (RFC 8011).</summary>
	private static readonly Dictionary<int, string> PrintQualityNames = new Dictionary<int, string>
	{
		[3] = "Draft",
		[4] = "Normal",
		[5] = "High",
	};

	/// <summary>Names for the common enum values of <c>finishings</c> (RFC 8011 and PWG 5100.1).</summary>
	private static readonly Dictionary<int, string> FinishingNames = new Dictionary<int, string>
	{
		[3] = "None",
		[4] = "Staple",
		[5] = "Punch",
		[6] = "Cover",
		[7] = "Bind",
		[8] = "Saddle stitch",
		[9] = "Edge stitch",
		[10] = "Fold",
		[11] = "Trim",
		[12] = "Bale",
		[13] = "Booklet maker",
		[14] = "Jog offset",
		[15] = "Coat",
		[16] = "Laminate",
		[20] = "Staple top left",
		[21] = "Staple bottom left",
		[22] = "Staple top right",
		[23] = "Staple bottom right",
		[24] = "Edge stitch left",
		[25] = "Edge stitch top",
		[26] = "Edge stitch right",
		[27] = "Edge stitch bottom",
		[28] = "Staple dual left",
		[29] = "Staple dual top",
		[30] = "Staple dual right",
		[31] = "Staple dual bottom",
		[32] = "Staple triple left",
		[33] = "Staple triple top",
		[34] = "Staple triple right",
		[35] = "Staple triple bottom",
		[50] = "Bind left",
		[51] = "Bind top",
		[52] = "Bind right",
		[53] = "Bind bottom",
		[60] = "Trim after pages",
		[61] = "Trim after documents",
		[62] = "Trim after copies",
		[63] = "Trim after job",
		[70] = "Punch top left",
		[71] = "Punch bottom left",
		[72] = "Punch top right",
		[73] = "Punch bottom right",
		[74] = "Punch dual left",
		[75] = "Punch dual top",
		[76] = "Punch dual right",
		[77] = "Punch dual bottom",
		[78] = "Punch triple left",
		[79] = "Punch triple top",
		[80] = "Punch triple right",
		[81] = "Punch triple bottom",
		[82] = "Punch quad left",
		[83] = "Punch quad top",
		[84] = "Punch quad right",
		[85] = "Punch quad bottom",
		[86] = "Punch multiple left",
		[87] = "Punch multiple top",
		[88] = "Punch multiple right",
		[89] = "Punch multiple bottom",
		[90] = "Fold accordion",
		[91] = "Fold double gate",
		[92] = "Fold gate",
		[93] = "Fold half",
		[94] = "Fold half Z",
		[95] = "Fold left gate",
		[96] = "Fold letter",
		[97] = "Fold parallel",
		[98] = "Fold poster",
		[99] = "Fold right gate",
		[100] = "Fold Z",
		[101] = "Fold engineering Z",
	};

	/// <summary>
	/// Lists the vendor options in a printer's attributes.
	/// </summary>
	/// <param name="attributes">The attributes, as <see cref="IIppClient.GetPrinterAttributeListAsync"/> returns
	/// them.</param>
	/// <returns>The options, sorted by key.</returns>
	internal static IReadOnlyList<VendorOption> ToVendorOptions(IReadOnlyList<IppAttribute> attributes)
	{
		ArgumentNullException.ThrowIfNull(attributes);

		Dictionary<string, List<IppAttribute>> byName = IppVendorOptionMapper.GroupTopLevel(attributes);

		IEnumerable<string> candidates = byName.TryGetValue(IppVendorOptionMapper.JobCreationAttributesSupported, out List<IppAttribute>? creation)
			? creation.Select(attribute => attribute.Value as string).OfType<string>()
			: byName.Keys
				.Where(name => name.EndsWith(IppVendorOptionMapper.SupportedSuffix, StringComparison.Ordinal))
				.Select(name => name[..^IppVendorOptionMapper.SupportedSuffix.Length])
				.Where(name => byName.ContainsKey(name + IppVendorOptionMapper.DefaultSuffix));

		List<VendorOption> options = [];

		foreach (string name in candidates.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
		{
			if (IppVendorOptionMapper.Excluded.Contains(name)
				|| name.EndsWith(IppVendorOptionMapper.CollectionSuffix, StringComparison.Ordinal)
				|| !byName.TryGetValue(name + IppVendorOptionMapper.SupportedSuffix, out List<IppAttribute>? supported))
			{
				continue;
			}

			VendorOption? option = IppVendorOptionMapper.ToVendorOption(name, supported, byName.GetValueOrDefault(name + IppVendorOptionMapper.DefaultSuffix));

			if (option is not null)
			{
				options.Add(option);
			}
		}

		return options;
	}

	/// <summary>
	/// Groups the top-level attributes by name, leaving out the members of collections, which the flattened encoding
	/// names after their member attributes.
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>Each top-level attribute's values, by name; a collection keeps only its opening entry.</returns>
	private static Dictionary<string, List<IppAttribute>> GroupTopLevel(IReadOnlyList<IppAttribute> attributes)
	{
		Dictionary<string, List<IppAttribute>> byName = new Dictionary<string, List<IppAttribute>>(StringComparer.Ordinal);
		string? current = null;
		int depth = 0;

		foreach (IppAttribute attribute in attributes)
		{
			if (attribute.Tag == Tag.EndCollection)
			{
				depth = Math.Max(0, depth - 1);

				continue;
			}

			if (depth > 0)
			{
				depth += attribute.Tag == Tag.BegCollection ? 1 : 0;

				continue;
			}

			// An additional value of a set may come without its name
			string? name = string.IsNullOrEmpty(attribute.Name) ? current : attribute.Name;

			if (name is null)
			{
				continue;
			}

			current = name;

			if (!byName.TryGetValue(name, out List<IppAttribute>? values))
			{
				values = [];
				byName[name] = values;
			}

			values.Add(attribute);

			if (attribute.Tag == Tag.BegCollection)
			{
				depth = 1;
			}
		}

		return byName;
	}

	/// <summary>
	/// Makes one attribute's vendor option from its supported values and default.
	/// </summary>
	/// <param name="name">The attribute's name.</param>
	/// <param name="supported">Its <c>-supported</c> values.</param>
	/// <param name="defaults">Its <c>-default</c> values, or <see langword="null"/>.</param>
	/// <returns>The option, or <see langword="null"/> if its values can't be sent as vendor options.</returns>
	private static VendorOption? ToVendorOption(string name, List<IppAttribute> supported, List<IppAttribute>? defaults)
	{
		string key = IppVendorAttributeMapper.IppPrefix + name;
		string displayName = IppVendorOptionMapper.AttributeNames.GetValueOrDefault(name) ?? IppKeywords.Describe(name);
		string? defaultValue = defaults?.Select(attribute => IppVendorOptionMapper.FormatValue(attribute)).FirstOrDefault(value => value is not null);

		// job-priority-supported is the number of priority levels; a job asks for 1 to 100
		if (name == IppVendorOptionMapper.JobPriority && supported[0].Value is int levels and > 0)
		{
			return new VendorOption
			{
				Key          = key,
				DisplayName  = displayName,
				Kind         = VendorOptionKind.Number,
				MinimumValue = IppVendorOptionMapper.LowestPriority,
				MaximumValue = IppVendorOptionMapper.HighestPriority,
				DefaultValue = defaultValue
			};
		}

		List<IppRange> ranges = supported.Select(attribute => attribute.Value).OfType<IppRange>().ToList();

		if (ranges.Count > 0)
		{
			return new VendorOption
			{
				Key          = key,
				DisplayName  = displayName,
				Kind         = VendorOptionKind.Number,
				MinimumValue = ranges.Min(range => range.Lower),
				MaximumValue = ranges.Max(range => range.Upper),
				DefaultValue = defaultValue
			};
		}

		// A boolean -supported with one value says whether the attribute is supported, not what it takes
		if (supported.All(attribute => attribute.Tag == Tag.Boolean) && supported.Select(attribute => attribute.Value).Distinct().Count() < 2)
		{
			return null;
		}

		List<VendorOptionChoice> choices = supported
			.Select(attribute => IppVendorOptionMapper.ToChoice(name, attribute))
			.OfType<VendorOptionChoice>()
			.DistinctBy(choice => choice.Value, StringComparer.Ordinal)
			.ToList();

		// Values vendor options can't express, such as collections, are left out of the choices
		return choices.Count == 0 ? null : IppVendorOptionMapper.WithChoices(key, displayName, choices, defaultValue, name);
	}

	/// <summary>
	/// Makes a choice option.
	/// </summary>
	/// <param name="key">The key.</param>
	/// <param name="displayName">The display name.</param>
	/// <param name="choices">The choices.</param>
	/// <param name="defaultValue">The default, or <see langword="null"/>.</param>
	/// <param name="name">The attribute's name.</param>
	/// <returns>The option.</returns>
	private static VendorOption WithChoices(string key, string displayName, List<VendorOptionChoice> choices, string? defaultValue, string name)
	{
		return new VendorOption
		{
			Key                 = key,
			DisplayName         = displayName,
			Kind                = VendorOptionKind.Choice,
			Choices             = choices,
			DefaultValue        = defaultValue,
			AllowsSeveralValues = IppVendorOptionMapper.SetAttributes.Contains(name)
		};
	}

	/// <summary>
	/// Makes a choice from one supported value.
	/// </summary>
	/// <param name="name">The attribute's name.</param>
	/// <param name="attribute">The value.</param>
	/// <returns>The choice, or <see langword="null"/> if the value can't be sent as a vendor option.</returns>
	private static VendorOptionChoice? ToChoice(string name, IppAttribute attribute)
	{
		string? value = IppVendorOptionMapper.FormatValue(attribute);

		if (value is null)
		{
			return null;
		}

		string displayName = attribute.Tag switch
		{
			Tag.Enum when name == IppVendorOptionMapper.PrintQuality && attribute.Value is int quality => IppVendorOptionMapper.PrintQualityNames.GetValueOrDefault(quality, value),
			Tag.Enum when name == IppVendorOptionMapper.Finishings && attribute.Value is int finishing => IppVendorOptionMapper.FinishingNames.GetValueOrDefault(finishing, value),
			Tag.Keyword => IppKeywords.Describe(value),
			Tag.Boolean => attribute.Value is true ? "Yes" : "No",
			_ => value,
		};

		return new VendorOptionChoice { Value = value, DisplayName = displayName };
	}

	/// <summary>
	/// Writes a value as a vendor option takes it, as <see cref="IppVendorAttributeMapper"/> reads it back.
	/// </summary>
	/// <param name="attribute">The value.</param>
	/// <returns>The text, or <see langword="null"/> for a value vendor options can't express: a collection, no
	/// value, or text, which <see cref="IppVendorAttributeMapper"/> would send back as a name or keyword, so a strict
	/// printer would refuse it.</returns>
	private static string? FormatValue(IppAttribute attribute)
	{
		return attribute.Tag switch
		{
			Tag.Keyword or Tag.NameWithoutLanguage or Tag.NameWithLanguage when attribute.Value is string text && text.Length > 0 && !text.Contains(',', StringComparison.Ordinal) => text,
			Tag.NameWithLanguage when attribute.Value is StringWithLanguage text && text.Value.Length > 0 && !text.Value.Contains(',', StringComparison.Ordinal) => text.Value,
			Tag.Integer or Tag.Enum when attribute.Value is int number => number.ToString(CultureInfo.InvariantCulture),
			Tag.Boolean when attribute.Value is bool flag => flag ? "true" : "false",
			Tag.Resolution when attribute.Value is Resolution resolution => IppVendorOptionMapper.FormatResolution(resolution),
			_ => null,
		};
	}

	/// <summary>
	/// Writes a resolution as <c>600dpi</c>, or <c>600x1200dpi</c> when the two directions differ.
	/// </summary>
	/// <param name="resolution">The resolution.</param>
	/// <returns>The text.</returns>
	private static string FormatResolution(Resolution resolution)
	{
		string units = resolution.Units == ResolutionUnit.DotsPerCm ? "dpcm" : "dpi";

		return resolution.Width == resolution.Height
			? string.Create(CultureInfo.InvariantCulture, $"{resolution.Width}{units}")
			: string.Create(CultureInfo.InvariantCulture, $"{resolution.Width}x{resolution.Height}{units}");
	}
}
