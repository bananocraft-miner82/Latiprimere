using System.Text;

namespace Latiprimere.ApiReference;

/// <summary>
/// Writes the whole reference: an index page, and a page for each public type, in a folder for each package.
/// </summary>
internal sealed class ReferenceGenerator
{
	private readonly IReadOnlyList<ApiPackage> packages; // The packages to document, in the index's order
	private readonly XmlDocumentation documentation;     // Their XML documentation

	/// <summary>
	/// Initialises a new instance of the <see cref="ReferenceGenerator"/> class.
	/// </summary>
	/// <param name="packages">The packages to document, in the order the index lists them.</param>
	/// <param name="documentation">Their XML documentation.</param>
	public ReferenceGenerator(IReadOnlyList<ApiPackage> packages, XmlDocumentation documentation)
	{
		ArgumentNullException.ThrowIfNull(packages);
		ArgumentNullException.ThrowIfNull(documentation);

		this.packages = packages;
		this.documentation = documentation;
	}

	/// <summary>
	/// Writes the reference into a folder, replacing the Markdown pages already there.
	/// </summary>
	/// <param name="folder">The folder, such as docs/site/reference.</param>
	/// <returns>How many pages it wrote.</returns>
	public int Write(string folder)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(folder);

		List<TypePage> pages = this.packages
			.SelectMany(package => TypePage.FindTypes(package).Select(type => new TypePage(package, type)))
			.ToList();

		ReferenceLinks links = new ReferenceLinks(pages);
		DocMarkdown markdown = new DocMarkdown(links);
		TypePageWriter writer = new TypePageWriter(this.documentation, markdown, links);

		ReferenceGenerator.RemovePages(folder);

		foreach (TypePage page in pages)
		{
			ReferenceGenerator.WriteFile(Path.Combine(folder, page.Package.Slug, page.FileName), writer.Write(page));
		}

		ReferenceGenerator.WriteFile(Path.Combine(folder, "index.md"), this.WriteIndex(pages, markdown));

		return pages.Count + 1;
	}

	/// <summary>
	/// Removes the Markdown pages a previous run wrote, and the folders that leaves empty, so a type that has gone
	/// loses its page.
	/// </summary>
	/// <param name="folder">The reference's folder.</param>
	private static void RemovePages(string folder)
	{
		if (!Directory.Exists(folder))
		{
			return;
		}

		foreach (string file in Directory.EnumerateFiles(folder, "*.md", SearchOption.AllDirectories))
		{
			File.Delete(file);
		}

		foreach (string directory in Directory.EnumerateDirectories(folder).Where(directory => !Directory.EnumerateFileSystemEntries(directory).Any()))
		{
			Directory.Delete(directory);
		}
	}

	/// <summary>
	/// Writes a page in UTF-8, with LF line endings, so the result is the same on every platform.
	/// </summary>
	/// <param name="path">The file.</param>
	/// <param name="content">The page.</param>
	private static void WriteFile(string path, string content)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content.ReplaceLineEndings("\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	/// <summary>
	/// Writes the index page: each package, what it is for, and its types with their summaries.
	/// </summary>
	/// <param name="pages">The type pages.</param>
	/// <param name="markdown">Turns documentation into Markdown.</param>
	/// <returns>The page's Markdown.</returns>
	private string WriteIndex(List<TypePage> pages, DocMarkdown markdown)
	{
		StringBuilder output = new StringBuilder();

		TypePageWriter.WriteFrontMatter(output, "API reference", "Every public type in the Latiprimere packages, with its members, from the documentation in the source.");

		output.Append("Every public type in the Latiprimere packages, with its members, generated from the documentation comments in the source. The [guides](/docs/guides/print-options-and-jobs/) show how they fit together.\n\n");
		output.Append("Most types are in the `Latiprimere` namespace, whichever package they come from. Each package's methods for dependency injection, such as `AddLatiprimereIpp()`, are in `Microsoft.Extensions.DependencyInjection`.\n\n");

		foreach (ApiPackage package in this.packages)
		{
			output.Append("## ").Append(package.Name).Append("\n\n");
			output.Append(package.Description).Append("\n\n");

			foreach (TypePage page in pages.Where(page => page.Package == package))
			{
				string summary = markdown.ToMarkdown(this.documentation.ForType(page.Type)?.Element("summary")).Replace("\n", " ", StringComparison.Ordinal);

				output.Append("- [").Append(page.Type.Name).Append("](").Append(page.Url).Append(')');
				output.Append(summary.Length > 0 ? ": " + summary : string.Empty).Append('\n');
			}

			output.Append('\n');
		}

		return output.ToString().TrimEnd('\n') + "\n";
	}
}
