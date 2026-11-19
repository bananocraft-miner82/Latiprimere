using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Latiprimere.ApiReference;

/// <summary>
/// Turns the content of an XML documentation element, such as a <c>&lt;summary&gt;</c>, into Markdown or plain text.
/// </summary>
internal sealed partial class DocMarkdown
{
	/// <summary>Marks the indentation of a list item's later lines until the text is tidied, so it isn't trimmed.</summary>
	private const char Indent = '\u0001';

	/// <summary>Begins and ends a code block in the Markdown.</summary>
	private const string Fence = "```";

	private readonly ReferenceLinks links; // Turns cref attributes into links

	/// <summary>
	/// Initialises a new instance of the <see cref="DocMarkdown"/> class.
	/// </summary>
	/// <param name="links">Turns <c>cref</c> attributes into links.</param>
	public DocMarkdown(ReferenceLinks links)
	{
		ArgumentNullException.ThrowIfNull(links);

		this.links = links;
	}

	/// <summary>
	/// Writes an element's content as Markdown.
	/// </summary>
	/// <param name="element">The element, or <see langword="null"/>.</param>
	/// <returns>The Markdown, with blank lines between paragraphs and no blank lines around it; empty for no
	/// element.</returns>
	public string ToMarkdown(XElement? element)
	{
		if (element is null)
		{
			return string.Empty;
		}

		StringBuilder markdown = new StringBuilder();

		foreach (XNode node in element.Nodes())
		{
			this.Write(node, markdown);
		}

		return DocMarkdown.Tidy(markdown.ToString());
	}

	/// <summary>
	/// Writes an element's content as plain text on one line, as for a page's description.
	/// </summary>
	/// <param name="element">The element, or <see langword="null"/>.</param>
	/// <returns>The text; empty for no element.</returns>
	public string ToPlainText(XElement? element)
	{
		if (element is null)
		{
			return string.Empty;
		}

		StringBuilder text = new StringBuilder();

		foreach (XNode node in element.Nodes())
		{
			this.WritePlain(node, text);
		}

		return DocMarkdown.Whitespace().Replace(text.ToString(), " ").Trim();
	}

	/// <summary>
	/// Tidies Markdown: lines trimmed, single spaces within them, one blank line between blocks, and code blocks
	/// left exactly as they are.
	/// </summary>
	/// <param name="markdown">The Markdown.</param>
	/// <returns>The tidied Markdown.</returns>
	private static string Tidy(string markdown)
	{
		List<string> lines = [];
		bool isInCode = false;

		foreach (string rawLine in markdown.Split('\n'))
		{
			if (isInCode)
			{
				lines.Add(rawLine.TrimEnd());
				isInCode = !rawLine.TrimStart(DocMarkdown.Indent).StartsWith(DocMarkdown.Fence, StringComparison.Ordinal);

				continue;
			}

			string line = DocMarkdown.Spaces().Replace(rawLine, " ").Trim(' ');
			bool isBlank = line.Trim(DocMarkdown.Indent).Length == 0;

			// One blank line at most, and none at the start
			if (isBlank && (lines.Count == 0 || lines[^1].Length == 0))
			{
				continue;
			}

			lines.Add(isBlank ? string.Empty : line);
			isInCode = line.TrimStart(DocMarkdown.Indent).StartsWith(DocMarkdown.Fence, StringComparison.Ordinal);
		}

		return string.Join('\n', lines).Trim('\n').Replace(DocMarkdown.Indent.ToString(), "  ", StringComparison.Ordinal);
	}

	/// <summary>
	/// Escapes text so Markdown shows it as it is.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The escaped text.</returns>
	private static string Escape(string text)
	{
		StringBuilder escaped = new StringBuilder(text.Length);

		foreach (char character in text)
		{
			switch (character)
			{
				case '<':
					escaped.Append("&lt;");
					break;

				case '\\' or '*' or '_' or '[' or ']' or '`':
					escaped.Append('\\').Append(character);
					break;

				default:
					escaped.Append(character);
					break;
			}
		}

		return escaped.ToString();
	}

	/// <summary>
	/// Writes inline code, choosing enough backticks for any the code contains.
	/// </summary>
	/// <param name="code">The code.</param>
	/// <returns>The Markdown.</returns>
	private static string InlineCode(string code)
	{
		string fence = code.Contains('`', StringComparison.Ordinal) ? "``" : "`";
		string padding = code.StartsWith('`') || code.EndsWith('`') ? " " : string.Empty;

		return fence + padding + code + padding + fence;
	}

	/// <summary>
	/// Removes the indentation every line of a code sample shares, and blank lines around it.
	/// </summary>
	/// <param name="code">The code, as in the XML.</param>
	/// <returns>The code.</returns>
	private static string Dedent(string code)
	{
		List<string> lines = code.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd()).ToList();

		while (lines.Count > 0 && lines[0].Length == 0)
		{
			lines.RemoveAt(0);
		}

		while (lines.Count > 0 && lines[^1].Length == 0)
		{
			lines.RemoveAt(lines.Count - 1);
		}

		int indentation = lines.Where(line => line.Length > 0).Select(line => line.Length - line.TrimStart().Length).DefaultIfEmpty(0).Min();

		return string.Join('\n', lines.Select(line => line.Length == 0 ? line : line[indentation..]));
	}

	/// <summary>Matches runs of whitespace.</summary>
	/// <returns>The expression.</returns>
	[GeneratedRegex(@"\s+")]
	private static partial Regex Whitespace();

	/// <summary>Matches runs of spaces and tabs.</summary>
	/// <returns>The expression.</returns>
	[GeneratedRegex(@"[ \t]+")]
	private static partial Regex Spaces();

	/// <summary>
	/// Writes one node as Markdown.
	/// </summary>
	/// <param name="node">The node.</param>
	/// <param name="markdown">Where to write.</param>
	private void Write(XNode node, StringBuilder markdown)
	{
		if (node is XText text)
		{
			markdown.Append(DocMarkdown.Escape(DocMarkdown.Whitespace().Replace(text.Value, " ")));

			return;
		}

		if (node is not XElement element)
		{
			return;
		}

		switch (element.Name.LocalName)
		{
			case "para":
				markdown.Append("\n\n");
				this.WriteChildren(element, markdown);
				markdown.Append("\n\n");
				break;

			case "br":
				markdown.Append("<br />\n");
				break;

			case "c":
				markdown.Append(DocMarkdown.InlineCode(DocMarkdown.Whitespace().Replace(element.Value, " ").Trim()));
				break;

			case "code":
				markdown.Append("\n\n").Append(DocMarkdown.Fence).Append("csharp\n").Append(DocMarkdown.Dedent(element.Value)).Append('\n').Append(DocMarkdown.Fence).Append("\n\n");
				break;

			case "b" or "strong":
				markdown.Append("**");
				this.WriteChildren(element, markdown);
				markdown.Append("**");
				break;

			case "i" or "em":
				markdown.Append('*');
				this.WriteChildren(element, markdown);
				markdown.Append('*');
				break;

			case "see" or "seealso":
				markdown.Append(this.Reference(element));
				break;

			case "paramref" or "typeparamref":
				markdown.Append(DocMarkdown.InlineCode((string?)element.Attribute("name") ?? string.Empty));
				break;

			case "list":
				this.WriteList(element, markdown);
				break;

			default:
				this.WriteChildren(element, markdown);
				break;
		}
	}

	/// <summary>
	/// Writes an element's children as Markdown.
	/// </summary>
	/// <param name="element">The element.</param>
	/// <param name="markdown">Where to write.</param>
	private void WriteChildren(XElement element, StringBuilder markdown)
	{
		foreach (XNode child in element.Nodes())
		{
			this.Write(child, markdown);
		}
	}

	/// <summary>
	/// Writes a <c>&lt;list&gt;</c> as a Markdown list, numbered for <c>type="number"</c>, each item's later lines
	/// indented under it.
	/// </summary>
	/// <param name="list">The list.</param>
	/// <param name="markdown">Where to write.</param>
	private void WriteList(XElement list, StringBuilder markdown)
	{
		bool isNumbered = (string?)list.Attribute("type") == "number";
		int number = 1;

		markdown.Append("\n\n");

		foreach (XElement item in list.Elements("item"))
		{
			StringBuilder content = new StringBuilder();
			XElement? term = item.Element("term");
			XElement? description = item.Element("description");

			if (term is not null)
			{
				content.Append("**");
				this.WriteChildren(term, content);
				content.Append("**: ");
			}

			this.WriteChildren(description ?? item, content);

			string marker = isNumbered ? $"{number++}. " : "- ";
			string itemText = DocMarkdown.Tidy(content.ToString()).Replace("\n", "\n" + DocMarkdown.Indent, StringComparison.Ordinal);

			markdown.Append(marker).Append(itemText).Append('\n');
		}

		markdown.Append('\n');
	}

	/// <summary>
	/// Writes a <c>&lt;see&gt;</c>: a link for <c>cref</c> or <c>href</c>, or code for <c>langword</c>.
	/// </summary>
	/// <param name="see">The element.</param>
	/// <returns>The Markdown.</returns>
	private string Reference(XElement see)
	{
		string? text = see.Nodes().Any() ? DocMarkdown.Whitespace().Replace(see.Value, " ").Trim() : null;
		string? cref = (string?)see.Attribute("cref");
		string? href = (string?)see.Attribute("href");
		string? langword = (string?)see.Attribute("langword");

		if (cref is not null)
		{
			return this.links.ToMarkdown(cref, text);
		}

		if (href is not null)
		{
			return $"[{DocMarkdown.Escape(text ?? href)}]({href})";
		}

		return DocMarkdown.InlineCode(langword ?? text ?? string.Empty);
	}

	/// <summary>
	/// Writes one node as plain text.
	/// </summary>
	/// <param name="node">The node.</param>
	/// <param name="text">Where to write.</param>
	private void WritePlain(XNode node, StringBuilder text)
	{
		if (node is XText content)
		{
			text.Append(content.Value);

			return;
		}

		if (node is not XElement element)
		{
			return;
		}

		switch (element.Name.LocalName)
		{
			case "code":
				break;

			case "see" or "seealso":
				string? cref = (string?)element.Attribute("cref");
				string? linkText = element.Nodes().Any() ? element.Value : null;

				text.Append
				(
					cref is not null
						? this.links.ToPlainText(cref, linkText)
						: linkText ?? (string?)element.Attribute("langword") ?? (string?)element.Attribute("href") ?? string.Empty
				);
				break;

			case "paramref" or "typeparamref":
				text.Append((string?)element.Attribute("name"));
				break;

			default:
				// Blocks are kept apart by a space; inline elements, such as <c>, run on into the text around them
				bool isBlock = element.Name.LocalName is "para" or "list" or "item" or "term" or "description" or "br";

				text.Append(isBlock ? " " : string.Empty);

				foreach (XNode child in element.Nodes())
				{
					this.WritePlain(child, text);
				}

				text.Append(isBlock ? " " : string.Empty);
				break;
		}
	}
}
