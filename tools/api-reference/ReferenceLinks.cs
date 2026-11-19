using System.Reflection;

namespace Latiprimere.ApiReference;

/// <summary>
/// Turns references to types and members into links: to their page in the reference, to Microsoft's .NET API
/// documentation, or, for anything else, into code with no link.
/// </summary>
internal sealed class ReferenceLinks
{
	/// <summary>Where .NET's own types are documented.</summary>
	private const string DotNetApiUrl = "https://learn.microsoft.com/dotnet/api/";

	private readonly Dictionary<string, LinkTarget> targets; // The reference's types and members, by documentation ID

	/// <summary>
	/// Initialises a new instance of the <see cref="ReferenceLinks"/> class.
	/// </summary>
	/// <param name="pages">The reference's type pages.</param>
	public ReferenceLinks(IEnumerable<TypePage> pages)
	{
		ArgumentNullException.ThrowIfNull(pages);

		this.targets = new Dictionary<string, LinkTarget>(StringComparer.Ordinal);

		foreach (TypePage page in pages)
		{
			this.targets[DocumentationIds.ForType(page.Type)] = new LinkTarget { Url = page.Url, Text = page.Type.Name };

			foreach (MemberInfo member in page.Members)
			{
				string text = member is ConstructorInfo ? page.Type.Name : page.Type.Name + "." + member.Name;

				this.targets[DocumentationIds.ForMember(member)] = new LinkTarget { Url = page.Url + "#" + TypePage.Anchor(member), Text = text };
			}
		}
	}

	/// <summary>
	/// Writes a reference as Markdown.
	/// </summary>
	/// <param name="cref">The documentation ID, as in a <c>cref</c> attribute.</param>
	/// <param name="text">The text to show, or <see langword="null"/> for the type's or member's name.</param>
	/// <returns>A link, or code where there's nothing to link to.</returns>
	public string ToMarkdown(string cref, string? text)
	{
		ArgumentNullException.ThrowIfNull(cref);

		if (this.targets.TryGetValue(cref, out LinkTarget? target))
		{
			return $"[{ReferenceLinks.EscapeLinkText(text ?? target.Text)}]({target.Url})";
		}

		string name = text ?? ReferenceLinks.ShortName(cref);
		string? dotNetUrl = ReferenceLinks.DotNetUrl(cref);

		return dotNetUrl is null ? "`" + name + "`" : $"[{ReferenceLinks.EscapeLinkText(name)}]({dotNetUrl})";
	}

	/// <summary>
	/// Writes a reference as plain text.
	/// </summary>
	/// <param name="cref">The documentation ID.</param>
	/// <param name="text">The text to show, or <see langword="null"/> for the type's or member's name.</param>
	/// <returns>The text.</returns>
	public string ToPlainText(string cref, string? text)
	{
		ArgumentNullException.ThrowIfNull(cref);

		return text ?? (this.targets.TryGetValue(cref, out LinkTarget? target) ? target.Text : ReferenceLinks.ShortName(cref));
	}

	/// <summary>
	/// Writes a type, used in a declaration, as Markdown: a link to its page or .NET's documentation, with its generic
	/// arguments.
	/// </summary>
	/// <param name="type">The type.</param>
	/// <returns>The link.</returns>
	public string ToMarkdown(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);

		string name = TypeNames.Display(type, null);
		string cref = DocumentationIds.ForType(type);

		if (this.targets.TryGetValue(cref, out LinkTarget? target))
		{
			return $"[{ReferenceLinks.EscapeLinkText(name)}]({target.Url})";
		}

		string? dotNetUrl = ReferenceLinks.DotNetUrl(cref);

		return dotNetUrl is null ? "`" + name + "`" : $"[{ReferenceLinks.EscapeLinkText(name)}]({dotNetUrl})";
	}

	/// <summary>
	/// Makes the URL of a .NET type's or member's documentation on Microsoft Learn, for the <c>System</c> and
	/// <c>Microsoft</c> namespaces.
	/// </summary>
	/// <param name="cref">The documentation ID.</param>
	/// <returns>The URL, or <see langword="null"/> for anything else.</returns>
	private static string? DotNetUrl(string cref)
	{
		string name = ReferenceLinks.WithoutPrefixAndParameters(cref);

		if (!name.StartsWith("System.", StringComparison.Ordinal) && !name.StartsWith("Microsoft.", StringComparison.Ordinal))
		{
			return null;
		}

		string page = name
			.Replace("#ctor", "-ctor", StringComparison.Ordinal)
			.Replace("``", "-", StringComparison.Ordinal)
			.Replace('`', '-')
			.ToLowerInvariant();

		return ReferenceLinks.DotNetApiUrl + page;
	}

	/// <summary>
	/// Makes the name to show for a reference with no page of its own: a type's name, or <c>Type.Member</c>.
	/// </summary>
	/// <param name="cref">The documentation ID.</param>
	/// <returns>The name.</returns>
	private static string ShortName(string cref)
	{
		string[] parts = ReferenceLinks.WithoutPrefixAndParameters(cref).Split('.');
		bool isType = cref.StartsWith("T:", StringComparison.Ordinal) || parts.Length < 2;

		if (isType)
		{
			return TypeNames.WithoutArity(parts[^1]);
		}

		string typeName = TypeNames.WithoutArity(parts[^2]);

		return parts[^1] == "#ctor" ? typeName : typeName + "." + TypeNames.WithoutArity(parts[^1]);
	}

	/// <summary>
	/// Removes the kind prefix, such as <c>T:</c>, and any parameter list from a documentation ID.
	/// </summary>
	/// <param name="cref">The documentation ID.</param>
	/// <returns>The type's or member's full name.</returns>
	private static string WithoutPrefixAndParameters(string cref)
	{
		string name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
		int parameters = name.IndexOf('(', StringComparison.Ordinal);

		return parameters < 0 ? name : name[..parameters];
	}

	/// <summary>
	/// Escapes the characters Markdown would read as the end of a link's text.
	/// </summary>
	/// <param name="text">The text.</param>
	/// <returns>The escaped text.</returns>
	private static string EscapeLinkText(string text)
	{
		return text.Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal);
	}
}
