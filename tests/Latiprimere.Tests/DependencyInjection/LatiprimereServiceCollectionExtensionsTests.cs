using Latiprimere.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Tests.DependencyInjection;

public sealed class LatiprimereServiceCollectionExtensionsTests
{
	[Fact]
	public void GivenPrintSystemRegisteredWhenAddLatiprimereThenCatalogResolvesWithoutLogging()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();
		services.AddSingleton<IPrintSystem>(new FakePrintSystem("Fake", true));

		// Act
		services.AddLatiprimere();
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrinterCatalog catalog = serviceProvider.GetRequiredService<IPrinterCatalog>();

		// Assert
		Assert.IsType<PrinterCatalog>(catalog);
	}

	[Fact]
	public void GivenAddLatiprimereCalledTwiceWhenRegisteredThenCatalogIsRegisteredOnce()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimere();
		services.AddLatiprimere();

		// Assert
		int catalogRegistrationCount = services.Count(service => service.ServiceType == typeof(IPrinterCatalog));
		Assert.Equal(1, catalogRegistrationCount);
	}

	[Fact]
	public void GivenNullServicesWhenAddLatiprimereThenThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act
		Action addLatiprimere = () => services!.AddLatiprimere(); // Deliberately null to test the guard

		// Assert
		Assert.Throws<ArgumentNullException>(addLatiprimere);
	}
}
