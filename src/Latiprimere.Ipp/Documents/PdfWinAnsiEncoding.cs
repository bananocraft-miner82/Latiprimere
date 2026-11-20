namespace Latiprimere.Ipp.Documents;

/// <summary>
/// Encodes text for PDF's <c>WinAnsiEncoding</c>, which the standard Courier font shows without being embedded
/// (ISO 32000-1, annex D): Windows-1252, which covers Western European languages. Characters outside it, and control
/// characters, aren't encoded, so text is refused rather than printed wrongly (Phase 4 plan, D6).
/// </summary>
internal static class PdfWinAnsiEncoding
{
	/// <summary>
	/// The characters of bytes 0x80 to 0x9F, where Windows-1252 differs from Latin-1, by byte; <c>'\0'</c> for the five
	/// bytes it leaves unused.
	/// </summary>
	private static readonly char[] HighCharacters =
	[
		'€', '\0',     '‚', 'ƒ', '„', '…', '†', '‡',
		'ˆ', '‰', 'Š', '‹', 'Œ', '\0',     'Ž', '\0',
		'\0',     '‘', '’', '“', '”', '•', '–', '—',
		'˜', '™', 'š', '›', 'œ', '\0',     'ž', 'Ÿ'
	];

	/// <summary>
	/// Encodes text, or finds the first character that can't be.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <param name="bytes">The encoded text, or empty if a character can't be encoded.</param>
	/// <param name="unsupportedIndex">The index of the first character that can't be encoded, or -1.</param>
	/// <returns>Whether every character could be encoded.</returns>
	internal static bool TryEncode(string text, out byte[] bytes, out int unsupportedIndex)
	{
		ArgumentNullException.ThrowIfNull(text);

		byte[] encoded = new byte[text.Length];

		for (int index = 0; index < text.Length; index++)
		{
			if (!PdfWinAnsiEncoding.TryEncode(text[index], out encoded[index]))
			{
				bytes = Array.Empty<byte>();
				unsupportedIndex = index;

				return false;
			}
		}

		bytes = encoded;
		unsupportedIndex = -1;

		return true;
	}

	/// <summary>
	/// Encodes one character.
	/// </summary>
	/// <param name="character">The character.</param>
	/// <param name="value">Its byte, or 0 if it has none.</param>
	/// <returns>Whether it has one: printable ASCII, Latin-1's no-break space to ÿ, and the 27 characters Windows-1252
	/// adds, such as € and curly quotes. Not control characters, nor half of a surrogate pair.</returns>
	internal static bool TryEncode(char character, out byte value)
	{
		if (character is (>= ' ' and <= '~') or (>= ' ' and <= 'ÿ'))
		{
			value = (byte)character;

			return true;
		}

		int index = Array.IndexOf(PdfWinAnsiEncoding.HighCharacters, character);

		if (character != '\0' && index >= 0)
		{
			value = (byte)(0x80 + index);

			return true;
		}

		value = 0;

		return false;
	}
}
