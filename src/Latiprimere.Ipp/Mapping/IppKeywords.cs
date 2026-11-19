namespace Latiprimere.Ipp.Mapping;

/// <summary>
/// Turns IPP keywords into names for people, where a printer gives none.
/// </summary>
internal static class IppKeywords
{
	/// <summary>
	/// Makes a keyword readable: <c>by-pass-tray</c> becomes "By pass tray", and <c>text_and_graphics</c> "Text and
	/// graphics".
	/// </summary>
	/// <param name="keyword">The keyword.</param>
	/// <returns>The description; the keyword itself if it has no letters to show.</returns>
	internal static string Describe(string keyword)
	{
		ArgumentNullException.ThrowIfNull(keyword);

		string words = keyword.Replace('-', ' ').Replace('_', ' ').Trim();

		return words.Length == 0 ? keyword : char.ToUpperInvariant(words[0]) + words[1..];
	}
}
