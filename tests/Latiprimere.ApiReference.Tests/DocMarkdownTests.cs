using System.Xml.Linq;
using Xunit;

namespace Latiprimere.ApiReference.Tests;

public sealed class DocMarkdownTests
{
	/// <summary>The converter under test, linking to a page for <see cref="PrintOptions"/> only.</summary>
	private readonly DocMarkdown markdown = new DocMarkdown(new ReferenceLinks([new TypePage(ApiPackages.All[0], typeof(PrintOptions))]));

	[Fact]
	public void GivenInlineElementsWhenConvertedThenTheyBecomeCodeAndLinks()
	{
		// Arrange
		XElement summary = XElement.Parse
		(
			"""
			<summary>
			    Sets <paramref name="copies"/> to <c>2</c> with <see cref="P:Latiprimere.PrintOptions.Copies"/>, or
			    <see langword="null"/>; see <see cref="T:System.IO.Stream">streams</see> and <see cref="T:Latiprimere.Internal"/>.
			</summary>
			"""
		);

		// Act
		string result = this.markdown.ToMarkdown(summary);

		// Assert
		Assert.Equal
		(
			"Sets `copies` to `2` with [PrintOptions.Copies](/docs/reference/latiprimere-abstractions/printoptions/#copies), or `null`; "
			+ "see [streams](https://learn.microsoft.com/dotnet/api/system.io.stream) and `Internal`.",
			result
		);
	}

	[Fact]
	public void GivenParagraphsAListAndCodeWhenConvertedThenTheyAreMarkdownBlocks()
	{
		// Arrange
		XElement remarks = XElement.Parse
		(
			"""
			<remarks>
			    <para>First paragraph.</para>
			    <list type="bullet">
			        <item><description>One <b>bold</b> item;</description></item>
			        <item><description>another.</description></item>
			    </list>
			    <code>
			    if (ready)
			    {
			        Print();
			    }
			    </code>
			</remarks>
			"""
		);

		// Act
		string result = this.markdown.ToMarkdown(remarks);

		// Assert
		Assert.Equal
		(
			"First paragraph.\n\n- One **bold** item;\n- another.\n\n```csharp\nif (ready)\n{\n    Print();\n}\n```",
			result
		);
	}

	[Fact]
	public void GivenTextMarkdownWouldReadAsFormattingWhenConvertedThenItIsEscaped()
	{
		// Arrange
		XElement summary = XElement.Parse("<summary>Keys such as ipp_* and [x] in List&lt;T&gt;.</summary>");

		// Act
		string result = this.markdown.ToMarkdown(summary);
		string plain = this.markdown.ToPlainText(summary);

		// Assert
		Assert.Equal("Keys such as ipp\\_\\* and \\[x\\] in List&lt;T>.", result);
		Assert.Equal("Keys such as ipp_* and [x] in List<T>.", plain);
	}

	[Fact]
	public void GivenInlineCodeAndALinkWhenConvertedToPlainTextThenOnlyTheirTextIsLeft()
	{
		// Arrange
		XElement summary = XElement.Parse("<summary>Gets <c>Copies</c>, as <see cref=\"T:Latiprimere.PrintOptions\"/> says.</summary>");

		// Act
		string result = this.markdown.ToPlainText(summary);

		// Assert
		Assert.Equal("Gets Copies, as PrintOptions says.", result);
	}
}
