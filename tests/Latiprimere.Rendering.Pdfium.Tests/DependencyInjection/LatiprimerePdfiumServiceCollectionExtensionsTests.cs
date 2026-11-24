using Latiprimere.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Rendering.Pdfium.Tests.DependencyInjection;

public sealed class LatiprimerePdfiumServiceCollectionExtensionsTests
{
	[Fact]
	public void GivenNoLimitsWhenRegisteredThenResolvesThePdfiumRasteriser()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimerePdfium();
		using ServiceProvider provider = services.BuildServiceProvider();

		// Assert
		Assert.IsType<PdfiumRasterizer>(provider.GetRequiredService<IPdfRasterizer>());
	}

	[Fact]
	public void GivenLimitsWhenRegisteredThenTheResolvedRasteriserKeepsToThem()
	{
		// Arrange — a limit one byte short of the document
		using FileStream content = TestDocuments.Open(TestDocuments.MixedOnePage);
		int limit = (int)content.Length - 1;
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimerePdfium(options => options.MaxDocumentBytes = limit);
		using ServiceProvider provider = services.BuildServiceProvider();
		IPdfRasterizer rasterizer = provider.GetRequiredService<IPdfRasterizer>();

		// Assert
		Assert.Throws<PrintException>(() => rasterizer.Open(content));
	}

	[Fact]
	public void GivenLimitOutOfRangeWhenRegisteredThenThrowsArgumentOutOfRangeExceptionAtOnce()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		Action register = () => services.AddLatiprimerePdfium(options => options.RenderTimeout = TimeSpan.Zero);

		// Assert
		Assert.Throws<ArgumentOutOfRangeException>(register);
	}

	[Fact]
	public void GivenNullConfigureWhenRegisteredThenThrowsArgumentNullException()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();
		Action<PdfiumOptions>? configure = null;

		// Act
		Action register = () => services.AddLatiprimerePdfium(configure!);

		// Assert
		Assert.Throws<ArgumentNullException>(register);
	}
}
