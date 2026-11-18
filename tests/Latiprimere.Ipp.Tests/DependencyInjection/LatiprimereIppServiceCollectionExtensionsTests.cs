using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Ipp.Tests.DependencyInjection;

public sealed class LatiprimereIppServiceCollectionExtensionsTests
{
	[Fact]
	public void GivenLoggerFactoryInServicesWhenAddLatiprimereIppThenThePrintSystemLogsThroughIt()
	{
		// Arrange — the print systems log what they carry on without, such as vendor options that couldn't be read
		ServiceCollection services = new ServiceCollection();
		Latiprimere.Testing.Fakes.RecordingLoggerFactory loggerFactory = new Latiprimere.Testing.Fakes.RecordingLoggerFactory();
		services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(loggerFactory);

		// Act
		services.AddLatiprimereIpp();
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());

		// Assert
		Assert.Same(loggerFactory.Logger, Assert.IsType<IppNetworkPrintSystem>(printSystem).Logger);
		Assert.Contains("Latiprimere.Ipp", loggerFactory.Categories);
	}

	[Fact]
	public void GivenNoLoggerFactoryWhenAddLatiprimereIppThenThePrintSystemLogsNothing()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereIpp();
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());

		// Assert
		Assert.Same(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, Assert.IsType<IppNetworkPrintSystem>(printSystem).Logger);
	}

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
