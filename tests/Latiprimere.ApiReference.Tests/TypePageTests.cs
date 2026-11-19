using System.Reflection;
using Xunit;

namespace Latiprimere.ApiReference.Tests;

public sealed class TypePageTests
{
	[Fact]
	public void GivenMembersOfEachKindWhenTheirAnchorsAreMadeThenEachMatchesAHeadingOnThePage()
	{
		// Arrange — enum values are listed under "Values" rather than having headings of their own
		MemberInfo constructor = typeof(PrinterCatalog).GetConstructors().Single();
		MemberInfo property = typeof(PrintOptions).GetProperty(nameof(PrintOptions.Copies))!;
		MemberInfo enumValue = typeof(DuplexMode).GetField(nameof(DuplexMode.LongEdge))!;

		// Act
		string[] anchors = [TypePage.Anchor(constructor), TypePage.Anchor(property), TypePage.Anchor(enumValue)];

		// Assert
		Assert.Equal(["constructors", "copies", "values"], anchors);
	}
}
