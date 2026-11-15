using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Ipp.Tests.DependencyInjection;

public sealed class LatiprimereIppServiceCollectionExtensionsTests
{
	[Fact]
	public async Task GivenConfiguredPrinterWhenAddLatiprimereIppThenResolvesPrintSystemUsingTheOptions()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereIpp
		(
			options =>
			{
				options.EnableDiscovery = false;
				options.PrinterUris.Add(new Uri("ipp://192.0.2.10/ipp/print"));
			}
		);

		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		Assert.Equal("IPP", printSystem.Name);
		Assert.Equal("ipp:ipp://192.0.2.10/ipp/print", Assert.Single(printers).Id);
	}

	[Fact]
	public void GivenAddLatiprimereIppCalledTwiceWhenRegisteredThenPrintSystemIsRegisteredOnce()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereIpp();
		services.AddLatiprimereIpp();

		// Assert
		int registrationCount = services.Count(service => service.ServiceType == typeof(IPrintSystem));
		Assert.Equal(1, registrationCount);
	}

	[Fact]
	public void GivenNullServicesWhenAddLatiprimereIppThenThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act
		Action addIpp = () => services!.AddLatiprimereIpp(); // Deliberately null to test the guard

		// Assert
		Assert.Throws<ArgumentNullException>(addIpp);
	}
}
