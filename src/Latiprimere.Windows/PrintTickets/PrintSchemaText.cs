using System.Text;
using System.Xml;

namespace Latiprimere.Windows.PrintTickets;

/// <summary>
/// Reads Print Schema documents: Print Tickets and Print Capabilities.
/// </summary>
internal static class PrintSchemaText
{
	/// <summary>
	/// Creates the settings every Print Schema document is read with: document type definitions are refused, so a
	/// document can't make the parser fetch or expand anything.
	/// </summary>
	/// <returns>The settings.</returns>
	internal static XmlReaderSettings CreateReaderSettings()
	{
		return new XmlReaderSettings
		{
			DtdProcessing = DtdProcessing.Prohibit,
			XmlResolver   = null
		};
	}

	/// <summary>
	/// Decodes a Print Ticket or Print Capabilities document: UTF-8 unless a byte order mark says UTF-16, as the API
	/// writes either.
	/// </summary>
	/// <param name="document">The document's bytes.</param>
	/// <returns>The text, without a byte order mark.</returns>
	internal static string ToText(byte[] document)
	{
		ArgumentNullException.ThrowIfNull(document);

		ReadOnlySpan<byte> bytes = document;

		if (bytes.StartsWith(Encoding.UTF8.Preamble))
		{
			return Encoding.UTF8.GetString(bytes[Encoding.UTF8.Preamble.Length..]);
		}

		if (bytes.StartsWith(Encoding.Unicode.Preamble))
		{
			return Encoding.Unicode.GetString(bytes[Encoding.Unicode.Preamble.Length..]);
		}

		if (bytes.StartsWith(Encoding.BigEndianUnicode.Preamble))
		{
			return Encoding.BigEndianUnicode.GetString(bytes[Encoding.BigEndianUnicode.Preamble.Length..]);
		}

		return Encoding.UTF8.GetString(bytes);
	}
}
