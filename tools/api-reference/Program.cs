namespace Latiprimere.ApiReference;

/// <summary>
/// Writes the API reference in docs/site/reference. Run it with <c>dotnet run --project tools/api-reference</c> after
/// changing a public type or its documentation; CI fails while the committed pages don't match.
/// </summary>
internal static class Program
{
	/// <summary>
	/// Writes the reference.
	/// </summary>
	/// <param name="args">The folder to write to, or nothing for docs/site/reference in this repository.</param>
	/// <returns>0 once the pages are written.</returns>
	private static int Main(string[] args)
	{
		string folder = args.Length > 0
			? Path.GetFullPath(args[0])
			: Path.Combine(Program.FindRepository(), "docs", "site", "reference");

		ReferenceGenerator generator = new ReferenceGenerator(ApiPackages.All, XmlDocumentation.Load(ApiPackages.All));
		int count = generator.Write(folder);

		Console.WriteLine($"Wrote {count} pages to {folder}");

		return 0;
	}

	/// <summary>
	/// Finds the repository this program was built in, by its solution file, from where the program runs.
	/// </summary>
	/// <returns>The repository's folder.</returns>
	/// <exception cref="InvalidOperationException">No folder above the program has the solution file.</exception>
	private static string FindRepository()
	{
		DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);

		while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Latiprimere.slnx")))
		{
			folder = folder.Parent;
		}

		return folder?.FullName ?? throw new InvalidOperationException("Run this from the repository's build output, or name the folder to write to.");
	}
}
