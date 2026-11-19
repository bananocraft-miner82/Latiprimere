using System.Reflection;
using Latiprimere.Ipp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.ApiReference.Tests;

public sealed class SignaturesTests
{
	[Theory]
	[InlineData(typeof(PrintOptions), "public sealed record PrintOptions")]
	[InlineData(typeof(PrinterCatalog), "public sealed class PrinterCatalog : IPrinterCatalog")]
	[InlineData(typeof(PrintAuthenticationException), "public sealed class PrintAuthenticationException : PrintException")]
	[InlineData(typeof(DuplexMode), "public enum DuplexMode")]
	[InlineData(typeof(LatiprimereIppServiceCollectionExtensions), "public static class LatiprimereIppServiceCollectionExtensions")]
	public void GivenATypeWhenDeclaredThenItReadsAsInTheSource(Type type, string expected)
	{
		// Arrange (provided by InlineData above)

		// Act
		string declaration = Signatures.DeclareType(type);

		// Assert
		Assert.Equal(expected, declaration);
	}

	[Fact]
	public void GivenMembersWithNullableTypesDefaultsAndExtensionsWhenDeclaredThenTheyReadAsInTheSource()
	{
		// Arrange
		Signatures signatures = new Signatures();
		ConstructorInfo constructor = typeof(PrinterCatalog).GetConstructors().Single();
		MethodInfo extension = typeof(LatiprimereIppServiceCollectionExtensions).GetMethod(nameof(LatiprimereIppServiceCollectionExtensions.AddLatiprimereIpp))!;
		PropertyInfo initOnly = typeof(PrintOptions).GetProperty(nameof(PrintOptions.JobName))!;
		PropertyInfo settable = typeof(IppNetworkOptions).GetProperty(nameof(IppNetworkOptions.AddressStorePath))!;

		// Act
		string[] declarations = [signatures.Declare(constructor), signatures.Declare(extension), signatures.Declare(initOnly), signatures.Declare(settable)];

		// Assert
		Assert.Equal
		(
			[
				"public PrinterCatalog(IEnumerable<IPrintSystem> printSystems, ILogger<PrinterCatalog>? logger = null)",
				"public static IServiceCollection AddLatiprimereIpp(this IServiceCollection services, Action<IppNetworkOptions>? configure = null)",
				"public string? JobName { get; init; }",
				"public string? AddressStorePath { get; set; }",
			],
			declarations
		);
	}
}
