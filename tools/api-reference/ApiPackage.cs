using System.Reflection;

namespace Latiprimere.ApiReference;

/// <summary>
/// A NuGet package whose public types the reference documents.
/// </summary>
internal sealed record ApiPackage
{
	/// <summary>Gets the package's name, which is also its assembly's.</summary>
	public required string Name { get; init; }

	/// <summary>Gets the package's assembly.</summary>
	public required Assembly Assembly { get; init; }

	/// <summary>Gets what the package is for, in Markdown, shown on the reference's index page.</summary>
	public required string Description { get; init; }

	/// <summary>
	/// Gets the package's folder under the reference: its name in lower case, with hyphens for dots, since the
	/// website's slugs drop dots.
	/// </summary>
	public string Slug
	{
		get
		{
			return this.Name.ToLowerInvariant().Replace('.', '-');
		}
	}

	/// <summary>
	/// Gets the package's anchor on the index page, where its heading is its name: the website's slugs drop dots.
	/// </summary>
	public string Anchor
	{
		get
		{
			return this.Name.ToLowerInvariant().Replace(".", string.Empty, StringComparison.Ordinal);
		}
	}
}
