using System.Globalization;
using System.Text;

namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Writes numbers and strings as PDF's syntax has them (ISO 32000-1, 7.3).
/// </summary>
internal static class PdfSyntax
{
	/// <summary>
	/// Writes a number: a whole number as one, others to four decimal places, a 1/10000 of a point, without an
	/// exponent, which PDF doesn't allow, and with a full stop whatever the culture.
	/// </summary>
	/// <param name="value">The number.</param>
	/// <returns>The number in PDF syntax.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> isn't finite.</exception>
	internal static string FormatNumber(double value)
	{
		if (!double.IsFinite(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value), value, null);
		}

		string text = value.ToString("0.####", CultureInfo.InvariantCulture);

		// Rounding can leave a negative zero, as -0.00001 does
		return text == "-0" ? "0" : text;
	}

	/// <summary>
	/// Writes bytes as a literal string, between parentheses: printable ASCII as it is, with backslashes before
	/// parentheses and backslashes, and every other byte in octal, so the content stream stays ASCII.
	/// </summary>
	/// <param name="bytes">The string's bytes, in the font's encoding.</param>
	/// <returns>The literal string, parentheses included.</returns>
	internal static string FormatString(ReadOnlySpan<byte> bytes)
	{
		StringBuilder text = new StringBuilder(bytes.Length + 2);
		text.Append('(');

		foreach (byte value in bytes)
		{
			if (value is (byte)'(' or (byte)')' or (byte)'\\')
			{
				text.Append('\\').Append((char)value);
			}
			else if (value is >= 0x20 and <= 0x7E)
			{
				text.Append((char)value);
			}
			else
			{
				text.Append('\\').Append(Convert.ToString(value, 8).PadLeft(3, '0'));
			}
		}

		return text.Append(')').ToString();
	}
}
