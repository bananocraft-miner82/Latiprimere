using System.Globalization;
using SharpIpp.Protocol.Models;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// Works with attributes as SharpIppNext reads them: one <see cref="IppAttribute"/> per value, each named after its
/// attribute, with collections spelt out between <c>begCollection</c> and <c>endCollection</c> values.
/// </summary>
internal static class IppAttributeLists
{
	/// <summary>
	/// Groups a flat list into one list per attribute: its values, and for collections every member inside them.
	/// </summary>
	/// <param name="attributes">The flat list.</param>
	/// <returns>One list per attribute, in order.</returns>
	public static List<List<IppAttribute>> Group(IEnumerable<IppAttribute> attributes)
	{
		List<List<IppAttribute>> entries = new List<List<IppAttribute>>();
		List<IppAttribute>? current = null;
		int depth = 0;

		foreach (IppAttribute attribute in attributes)
		{
			// Outside collections, a new name starts a new attribute; the same name again is another value
			if (depth == 0 && (current is null || attribute.Name != current[0].Name))
			{
				current = new List<IppAttribute>();
				entries.Add(current);
			}

			current!.Add(attribute);

			if (attribute.Tag == Tag.BegCollection)
			{
				depth++;
			}
			else if (attribute.Tag == Tag.EndCollection)
			{
				depth--;
			}
		}

		return entries;
	}

	/// <summary>
	/// Describes one attribute's values as text.
	/// </summary>
	/// <param name="entry">The attribute, as <see cref="Group"/> returns it.</param>
	/// <returns>The description.</returns>
	public static IppAttributeRecord Describe(List<IppAttribute> entry)
	{
		List<string> values = new List<string>();
		int index = 0;

		while (index < entry.Count)
		{
			if (entry[index].Tag == Tag.BegCollection)
			{
				values.Add(IppAttributeLists.FormatCollection(entry, ref index));
			}
			else
			{
				values.Add(IppAttributeLists.FormatValue(entry[index].Value));
				index++;
			}
		}

		return new IppAttributeRecord(entry[0].Name, entry[0].Tag, values);
	}

	/// <summary>
	/// Describes every attribute in a flat list.
	/// </summary>
	/// <param name="attributes">The flat list.</param>
	/// <returns>One description per attribute.</returns>
	public static List<IppAttributeRecord> DescribeAll(IEnumerable<IppAttribute> attributes)
	{
		return IppAttributeLists.Group(attributes).Select(IppAttributeLists.Describe).ToList();
	}

	/// <summary>
	/// Prepares attributes for writing as RFC 8010 encodes them: values inside collections, and every collection of
	/// a set after the first, have no name. SharpIppNext reads them with names and would otherwise write the names
	/// back, which <c>ipptool</c> reports as duplicate attributes. (It leaves other sets' extra values unnamed by
	/// itself.)
	/// </summary>
	/// <param name="attributes">The attributes.</param>
	/// <returns>The attributes to write.</returns>
	public static List<IppAttribute> ForWriting(IEnumerable<IppAttribute> attributes)
	{
		List<IppAttribute> written = new List<IppAttribute>();
		string? previousName = null; // The last attribute outside collections
		int depth = 0;

		foreach (IppAttribute attribute in attributes)
		{
			bool isInsideCollection = depth > 0;
			bool isAnotherCollectionOfSet = !isInsideCollection && attribute.Tag == Tag.BegCollection && attribute.Name == previousName;
			bool isUnnamed = (isInsideCollection || isAnotherCollectionOfSet) && attribute.Name.Length > 0;
			written.Add(isUnnamed ? IppAttributeLists.Rename(attribute, string.Empty) : attribute);

			if (!isInsideCollection)
			{
				previousName = attribute.Name;
			}

			if (attribute.Tag == Tag.BegCollection)
			{
				depth++;
			}
			else if (attribute.Tag == Tag.EndCollection)
			{
				depth--;
			}
		}

		return written;
	}

	/// <summary>
	/// Formats one value as text.
	/// </summary>
	/// <param name="value">The value as SharpIppNext read it.</param>
	/// <returns>The text.</returns>
	public static string FormatValue(object? value)
	{
		return value switch
		{
			null => string.Empty,
			string text => text,
			bool flag => flag ? "true" : "false",
			SharpIpp.Protocol.Models.Range range => string.Create(CultureInfo.InvariantCulture, $"{range.Lower}-{range.Upper}"),
			Resolution resolution => string.Create(CultureInfo.InvariantCulture, $"{resolution.Width}x{resolution.Height}{(resolution.Units == ResolutionUnit.DotsPerInch ? "dpi" : "dpcm")}"),
			StringWithLanguage text => text.Value,
			DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
			NoValue => "no-value",
			byte[] bytes => Convert.ToHexString(bytes),
			IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
			_ => value.ToString() ?? string.Empty
		};
	}

	/// <summary>
	/// Formats a collection, starting at its <c>begCollection</c> value.
	/// </summary>
	/// <param name="entry">The attribute holding the collection.</param>
	/// <param name="index">The index of <c>begCollection</c>; moved past the matching <c>endCollection</c>.</param>
	/// <returns>The collection as <c>{member=value ...}</c>.</returns>
	private static string FormatCollection(List<IppAttribute> entry, ref int index)
	{
		List<string> members = new List<string>();
		index++;

		while (index < entry.Count && entry[index].Tag != Tag.EndCollection)
		{
			// Each member: its name, then one or more values, which may be collections themselves
			string memberName = IppAttributeLists.FormatValue(entry[index].Value);
			index++;
			List<string> values = new List<string>();

			while (index < entry.Count && entry[index].Tag is not (Tag.MemberAttrName or Tag.EndCollection))
			{
				if (entry[index].Tag == Tag.BegCollection)
				{
					values.Add(IppAttributeLists.FormatCollection(entry, ref index));
				}
				else
				{
					values.Add(IppAttributeLists.FormatValue(entry[index].Value));
					index++;
				}
			}

			members.Add($"{memberName}={string.Join(",", values)}");
		}

		index++; // Past endCollection

		return "{" + string.Join(" ", members) + "}";
	}

	/// <summary>
	/// Copies an attribute value under another name.
	/// </summary>
	/// <param name="attribute">The attribute value.</param>
	/// <param name="name">The new name.</param>
	/// <returns>The copy.</returns>
	private static IppAttribute Rename(IppAttribute attribute, string name)
	{
		return attribute.Value switch
		{
			int number => new IppAttribute(attribute.Tag, name, number),
			bool flag => new IppAttribute(attribute.Tag, name, flag),
			string text => new IppAttribute(attribute.Tag, name, text),
			DateTimeOffset time => new IppAttribute(attribute.Tag, name, time),
			NoValue noValue => new IppAttribute(attribute.Tag, name, noValue),
			SharpIpp.Protocol.Models.Range range => new IppAttribute(attribute.Tag, name, range),
			Resolution resolution => new IppAttribute(attribute.Tag, name, resolution),
			StringWithLanguage text => new IppAttribute(attribute.Tag, name, text),
			OctetString octets => new IppAttribute(attribute.Tag, name, octets),
			byte[] bytes => new IppAttribute(attribute.Tag, name, bytes),
			_ => attribute
		};
	}
}
