using SharpIpp.Protocol.Models;

namespace Latiprimere.FakeIppPrinter;

/// <summary>
/// One attribute of a request the fake printer received, with its values as text.
/// </summary>
/// <param name="Name">The attribute's name, such as <c>sides</c>.</param>
/// <param name="Tag">The value tag of its first value, such as <see cref="SharpIpp.Protocol.Models.Tag.Keyword"/>.</param>
/// <param name="Values">Each value as text: numbers and enums as digits, ranges as <c>1-999</c>, and collections as
/// <c>{member=value member={...}}</c>, such as <c>{media-source=manual media-size={x-dimension=21000
/// y-dimension=29700}}</c>.</param>
public sealed record IppAttributeRecord(string Name, Tag Tag, IReadOnlyList<string> Values)
{
	/// <summary>
	/// Gets the first value, or an empty string if there is none.
	/// </summary>
	public string Value
	{
		get
		{
			return this.Values.Count > 0 ? this.Values[0] : string.Empty;
		}
	}

	/// <summary>
	/// Shows the attribute as <c>name = value,value</c>.
	/// </summary>
	/// <returns>The text.</returns>
	public override string ToString()
	{
		return $"{this.Name} = {string.Join(",", this.Values)}";
	}
}
