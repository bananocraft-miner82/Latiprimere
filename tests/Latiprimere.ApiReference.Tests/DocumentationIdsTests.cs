using System.Reflection;
using Xunit;

namespace Latiprimere.ApiReference.Tests;

public sealed class DocumentationIdsTests
{
	[Fact]
	public void GivenEveryPublicTypeAndMemberWhenTheirIdsAreMadeThenTheCompilersDocumentationHasThem()
	{
		// Arrange — the compiler documents every public member, except constructors it adds itself
		XmlDocumentation documentation = XmlDocumentation.Load(ApiPackages.All);
		List<TypePage> pages = ApiPackages.All.SelectMany(package => TypePage.FindTypes(package).Select(type => new TypePage(package, type))).ToList();

		// Act
		List<string> missing = pages
			.Select(page => DocumentationIds.ForType(page.Type))
			.Concat(pages.SelectMany(page => page.Members.Where(member => member is not ConstructorInfo { IsPublic: true } constructor || constructor.GetParameters().Length > 0)).Select(DocumentationIds.ForMember))
			.Where(id => !documentation.Contains(id))
			.ToList();

		// Assert
		Assert.NotEmpty(pages);
		Assert.Empty(missing);
	}

	[Fact]
	public void GivenAMethodWithGenericNullableAndDefaultParametersWhenItsIdIsMadeThenItIsTheCompilers()
	{
		// Arrange
		MethodInfo method = typeof(IPrinter).GetMethod(nameof(IPrinter.SubmitAsync))!;

		// Act
		string id = DocumentationIds.ForMember(method);

		// Assert
		Assert.Equal("M:Latiprimere.IPrinter.SubmitAsync(Latiprimere.PrintContent,Latiprimere.PrintOptions,System.Threading.CancellationToken)", id);
	}
}
