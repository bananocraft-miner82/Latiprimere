using Xunit;

namespace Latiprimere.ApiReference.Tests;

public sealed class ReferenceGeneratorTests : IDisposable
{
	private readonly string folder = Path.Combine(Path.GetTempPath(), "latiprimere-tests", Guid.NewGuid().ToString("N")); // Where the pages go

	[Fact]
	public void GivenTheSameCodeWhenWrittenTwiceThenThePagesAreTheSameWithLfLineEndings()
	{
		// Arrange — a stale page from a type that has gone, which the second run removes
		ReferenceGenerator generator = new ReferenceGenerator(ApiPackages.All, XmlDocumentation.Load(ApiPackages.All));
		string first = Path.Combine(this.folder, "first");
		string second = Path.Combine(this.folder, "second");
		Directory.CreateDirectory(Path.Combine(second, "gone"));
		File.WriteAllText(Path.Combine(second, "gone", "removedtype.md"), "stale");

		// Act
		int count = generator.Write(first);
		generator.Write(second);

		// Assert
		Dictionary<string, string> firstPages = ReferenceGeneratorTests.ReadPages(first);
		Dictionary<string, string> secondPages = ReferenceGeneratorTests.ReadPages(second);

		Assert.Equal(count, firstPages.Count);
		Assert.Equal(firstPages, secondPages);
		Assert.Contains(Path.Combine("latiprimere-abstractions", "printoptions.md"), firstPages.Keys);
		Assert.DoesNotContain(firstPages.Values, page => page.Contains('\r', StringComparison.Ordinal));
		Assert.All(firstPages.Values, page => Assert.StartsWith("---\ntitle: ", page, StringComparison.Ordinal));
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (Directory.Exists(this.folder))
		{
			Directory.Delete(this.folder, recursive: true);
		}
	}

	/// <summary>
	/// Reads every page under a folder.
	/// </summary>
	/// <param name="folder">The folder.</param>
	/// <returns>Each page's text, by its path in the folder.</returns>
	private static Dictionary<string, string> ReadPages(string folder)
	{
		return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
			.ToDictionary(path => Path.GetRelativePath(folder, path), File.ReadAllText);
	}
}
