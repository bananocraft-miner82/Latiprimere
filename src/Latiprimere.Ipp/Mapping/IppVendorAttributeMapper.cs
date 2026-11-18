using System.Globalization;
using System.Text.RegularExpressions;
using Latiprimere.Ipp.Resources;
using SharpIpp.Models.Requests;
using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Turns <see cref="PrintOptions.VendorOptions"/> into IPP attributes (ADR 0026).
/// </summary>
/// <remarks>
/// The name after the prefix is the attribute name. Values separated by commas are several values. Each value's type
/// is inferred: an integer is an <c>integer</c>, or an <c>enum</c> for attributes IPP defines as enums; <c>true</c>
/// and <c>false</c> are a <c>boolean</c>; <c>600dpi</c>, <c>600x1200dpi</c> or <c>118dpcm</c> is a
/// <c>resolution</c> for an attribute whose name ends in <c>-resolution</c>; keyword syntax is a <c>keyword</c>;
/// anything else is a <c>name</c>.
/// </remarks>
internal static partial class IppVendorAttributeMapper
{
	/// <summary>The prefix every IPP print system reads.</summary>
	internal const string IppPrefix = "ipp:";

	/// <summary>The prefix only CUPS reads.</summary>
	internal const string CupsPrefix = "cups:";

	/// <summary>The keys a direct IPP printer reads.</summary>
	internal static readonly IReadOnlyList<string> NetworkPrefixes = [IppVendorAttributeMapper.IppPrefix];

	/// <summary>The keys CUPS reads.</summary>
	internal static readonly IReadOnlyList<string> CupsPrefixes = [IppVendorAttributeMapper.IppPrefix, IppVendorAttributeMapper.CupsPrefix];

	/// <summary>Job attributes IPP defines as enums, whose integer values must be sent with the enum tag.</summary>
	private static readonly HashSet<string> EnumAttributes = new HashSet<string>(StringComparer.Ordinal)
	{
		"finishings",
		"image-orientation",
		"input-orientation-requested",
		"media-source-feed-orientation",
		"orientation-requested",
		"print-quality",
	};

	/// <summary>Print-Job operation attributes a caller may add (RFC 8011 and PWG 5100.7); the rest go with the job.</summary>
	private static readonly HashSet<string> OperationAttributes = new HashSet<string>(StringComparer.Ordinal)
	{
		"compression",
		"document-name",
		"document-natural-language",
		"ipp-attribute-fidelity",
		"job-impressions",
		"job-k-octets",
		"job-mandatory-attributes",
		"job-media-sheets",
	};

	/// <summary>The end of the names of IPP's resolution attributes, such as <c>printer-resolution</c> and
	/// <c>pclm-source-resolution</c>, whose values are resolutions.</summary>
	private const string ResolutionSuffix = "-resolution";

	/// <summary>Operation attributes every Print-Job request carries.</summary>
	private static readonly string[] AlwaysSent =
	[
		"attributes-charset",
		"attributes-natural-language",
		"printer-uri",
		"requesting-user-name",
		"document-format",
	];

	/// <summary>
	/// Creates the vendor attributes for a job.
	/// </summary>
	/// <param name="vendorOptions">The vendor options.</param>
	/// <param name="prefixes">The key prefixes this print system reads; other keys are ignored.</param>
	/// <param name="request">The typed request, whose attributes the vendor options mustn't repeat.</param>
	/// <returns>The attributes, or <see cref="IppVendorAttributes.None"/> if no key applies.</returns>
	/// <exception cref="ArgumentException">A name isn't an IPP keyword, a value is empty, or an attribute is set twice:
	/// by two keys, or by a key and a typed option.</exception>
	internal static IppVendorAttributes ToAttributes
	(
		IReadOnlyDictionary<string, string> vendorOptions,
		IReadOnlyList<string> prefixes,
		PrintJobRequest request
	)
	{
		ArgumentNullException.ThrowIfNull(vendorOptions);
		ArgumentNullException.ThrowIfNull(prefixes);
		ArgumentNullException.ThrowIfNull(request);

		HashSet<string> sent = IppVendorAttributeMapper.GetSentNames(request);
		Dictionary<string, string> keysByName = new Dictionary<string, string>(StringComparer.Ordinal);
		List<IppAttribute> operation = [];
		List<IppAttribute> job = [];

		// Sorted, so the attributes go out in the same order whatever order the caller's dictionary has
		foreach (KeyValuePair<string, string> option in vendorOptions.OrderBy(option => option.Key, StringComparer.Ordinal))
		{
			string? prefix = prefixes.FirstOrDefault(prefix => option.Key.StartsWith(prefix, StringComparison.Ordinal));

			if (prefix is null)
			{
				continue;
			}

			string name = option.Key[prefix.Length..];
			IppVendorAttributeMapper.CheckName(option.Key, name, sent, keysByName);
			keysByName[name] = option.Key;

			List<IppAttribute> group = IppVendorAttributeMapper.OperationAttributes.Contains(name) ? operation : job;
			group.AddRange(IppVendorAttributeMapper.ToValues(option.Key, name, option.Value));
		}

		return operation.Count == 0 && job.Count == 0
			? IppVendorAttributes.None
			: new IppVendorAttributes { Operation = operation, Job = job };
	}

	/// <summary>
	/// Encodes one option's values, inferring each one's type.
	/// </summary>
	/// <param name="key">The option's key, for messages.</param>
	/// <param name="name">The attribute name.</param>
	/// <param name="value">The value, with several values separated by commas.</param>
	/// <returns>One attribute per value, all with the attribute's name, as SharpIppNext expects for a set.</returns>
	/// <exception cref="ArgumentException">The value, or one of several, is empty.</exception>
	internal static IEnumerable<IppAttribute> ToValues(string key, string name, string value)
	{
		string[] parts = value.Split(',');

		if (parts.Any(string.IsNullOrEmpty))
		{
			string message = ExceptionMessages.FormatVendorOptionValueEmpty(key);

			throw new ArgumentException(message);
		}

		return parts.Select(part => IppVendorAttributeMapper.ToValue(name, part)).ToList();
	}

	/// <summary>
	/// Encodes one value.
	/// </summary>
	/// <param name="name">The attribute name.</param>
	/// <param name="value">The value.</param>
	/// <returns>The attribute.</returns>
	private static IppAttribute ToValue(string name, string value)
	{
		if (int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
		{
			Tag tag = IppVendorAttributeMapper.EnumAttributes.Contains(name) ? Tag.Enum : Tag.Integer;

			return new IppAttribute(tag, name, number);
		}

		if (value is "true" or "false")
		{
			return new IppAttribute(Tag.Boolean, name, value == "true");
		}

		// A resolution, as printer-resolution takes: 600dpi, 600x1200dpi or 118dpcm (ADR 0032). Only for IPP's
		// resolution attributes: elsewhere, such as CUPS's resolution option, "600dpi" is a name.
		Match resolution = name.EndsWith(IppVendorAttributeMapper.ResolutionSuffix, StringComparison.Ordinal)
			? IppVendorAttributeMapper.ResolutionPattern().Match(value)
			: Match.Empty;

		if (resolution.Success
			&& int.TryParse(resolution.Groups["width"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int width)
			&& int.TryParse(resolution.Groups["height"].Success ? resolution.Groups["height"].Value : resolution.Groups["width"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int height))
		{
			ResolutionUnit units = resolution.Groups["units"].Value == "dpcm" ? ResolutionUnit.DotsPerCm : ResolutionUnit.DotsPerInch;

			return new IppAttribute(Tag.Resolution, name, new Resolution(width, height, units));
		}

		return IppVendorAttributeMapper.KeywordPattern().IsMatch(value)
			? new IppAttribute(Tag.Keyword, name, value)
			: new IppAttribute(Tag.NameWithoutLanguage, name, value);
	}

	/// <summary>
	/// Checks an attribute name, and that nothing else sets the attribute.
	/// </summary>
	/// <param name="key">The option's key.</param>
	/// <param name="name">The attribute name.</param>
	/// <param name="sent">The attributes the typed request already sends.</param>
	/// <param name="keysByName">The keys already read, by attribute name.</param>
	/// <exception cref="ArgumentException">The name isn't an IPP keyword, or the attribute is already set.</exception>
	private static void CheckName(string key, string name, HashSet<string> sent, Dictionary<string, string> keysByName)
	{
		string? message = null;

		if (!IppVendorAttributeMapper.KeywordPattern().IsMatch(name))
		{
			message = ExceptionMessages.FormatVendorOptionNameInvalid(key);
		}
		else if (sent.Contains(name))
		{
			message = ExceptionMessages.FormatVendorOptionSetByLatiprimere(key, name);
		}
		else if (keysByName.TryGetValue(name, out string? otherKey))
		{
			message = ExceptionMessages.FormatVendorOptionSetTwice(otherKey, key, name);
		}

		if (message is not null)
		{
			throw new ArgumentException(message);
		}
	}

	/// <summary>
	/// Lists the attributes a typed request sends.
	/// </summary>
	/// <param name="request">The request.</param>
	/// <returns>The attribute names.</returns>
	private static HashSet<string> GetSentNames(PrintJobRequest request)
	{
		HashSet<string> names = new HashSet<string>(IppVendorAttributeMapper.AlwaysSent, StringComparer.Ordinal);
		JobTemplateAttributes? job = request.JobTemplateAttributes;

		IppVendorAttributeMapper.AddIf(names, request.OperationAttributes?.JobName is not null, "job-name");
		IppVendorAttributeMapper.AddIf(names, job?.Copies is not null, "copies");
		IppVendorAttributeMapper.AddIf(names, job?.Sides is not null, "sides");
		IppVendorAttributeMapper.AddIf(names, job?.OrientationRequested is not null, "orientation-requested");
		IppVendorAttributeMapper.AddIf(names, job?.PrintColorMode is not null, "print-color-mode");
		IppVendorAttributeMapper.AddIf(names, job?.Media is not null, "media");
		IppVendorAttributeMapper.AddIf(names, job?.MediaCol is not null, "media-col");

		return names;
	}

	/// <summary>
	/// Adds a name when a condition holds.
	/// </summary>
	/// <param name="names">The names.</param>
	/// <param name="condition">Whether to add it.</param>
	/// <param name="name">The name.</param>
	private static void AddIf(HashSet<string> names, bool condition, string name)
	{
		if (condition)
		{
			names.Add(name);
		}
	}

	/// <summary>
	/// IPP keyword syntax (RFC 8011, section 5.1.4): a lower-case letter, then lower-case letters, digits, <c>-</c>,
	/// <c>_</c> and <c>.</c>.
	/// </summary>
	/// <returns>The pattern.</returns>
	[GeneratedRegex("^[a-z][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
	private static partial Regex KeywordPattern();

	/// <summary>
	/// A resolution: dots across, optionally <c>x</c> and dots down, then <c>dpi</c> or <c>dpcm</c>.
	/// </summary>
	/// <returns>The pattern.</returns>
	[GeneratedRegex("^(?<width>[0-9]{1,6})(x(?<height>[0-9]{1,6}))?(?<units>dpi|dpcm)$", RegexOptions.CultureInvariant)]
	private static partial Regex ResolutionPattern();
}
