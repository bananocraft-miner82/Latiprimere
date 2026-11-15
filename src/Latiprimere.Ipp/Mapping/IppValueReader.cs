using SharpIpp.Protocol.Models;

namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Reads optional IPP attribute values, treating absent and out-of-band values (such as <c>no-value</c>) as missing.
/// </summary>
internal static class IppValueReader
{
	/// <summary>
	/// Reads a text attribute.
	/// </summary>
	/// <param name="value">The attribute value, if present.</param>
	/// <returns>The text, or <see langword="null"/> if the attribute is absent, out-of-band or blank.</returns>
	internal static string? ReadString(StringWithLanguage? value)
	{
		// Out-of-band values and empty strings carry no information for callers
		if (value is not { IsValue: true } presentValue
			  || string.IsNullOrWhiteSpace(presentValue.Value))
		{
			return null;
		}

		return presentValue.Value;
	}

	/// <summary>
	/// Reads a value-type attribute.
	/// </summary>
	/// <typeparam name="T">The attribute's value type.</typeparam>
	/// <param name="value">The attribute value, if present.</param>
	/// <returns>The value, or <see langword="null"/> if the attribute is absent or out-of-band.</returns>
	internal static T? ReadValue<T>(IppValue<T>? value)
		where T : struct
	{
		if (value is not { IsValue: true } presentValue)
		{
			return null;
		}

		return presentValue.Value;
	}

	/// <summary>
	/// Reads a reference-type attribute.
	/// </summary>
	/// <typeparam name="T">The attribute's value type.</typeparam>
	/// <param name="value">The attribute value, if present.</param>
	/// <returns>The value, or <see langword="null"/> if the attribute is absent or out-of-band.</returns>
	internal static T? ReadReference<T>(IppValue<T>? value)
		where T : class
	{
		if (value is not { IsValue: true } presentValue)
		{
			return null;
		}

		return presentValue.Value;
	}
}
