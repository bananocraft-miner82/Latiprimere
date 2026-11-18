using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Cups.Tests.DependencyInjection;

public sealed class LatiprimereCupsServiceCollectionExtensionsTests
{
	[Fact]
	public void GivenLoggerFactoryInServicesWhenAddLatiprimereCupsThenThePrintSystemLogsThroughIt()
	{
		// Arrange — the print systems log what they carry on without, such as vendor options that couldn't be read
		ServiceCollection services = new ServiceCollection();
		Latiprimere.Testing.Fakes.RecordingLoggerFactory loggerFactory = new Latiprimere.Testing.Fakes.RecordingLoggerFactory();
		services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(loggerFactory);

		// Act
		services.AddLatiprimereCups();
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());

		// Assert
		Assert.Same(loggerFactory.Logger, Assert.IsType<Latiprimere.Cups.CupsPrintSystem>(printSystem).Logger);
		Assert.Contains("Latiprimere.Ipp", loggerFactory.Categories);
	}

	[Fact]
	public void GivenNoLoggerFactoryWhenAddLatiprimereCupsThenThePrintSystemLogsNothing()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereCups();
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());

		// Assert
		Assert.Same(Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, Assert.IsType<Latiprimere.Cups.CupsPrintSystem>(printSystem).Logger);
	}

	[Fact]
	public void GivenConfiguredServerWhenAddLatiprimereCupsThenResolvesSupportedCupsPrintSystem()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereCups(options => options.Server = "printserver:631");
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());

		// Assert
		Assert.Equal("CUPS", printSystem.Name);
		Assert.True(printSystem.IsSupported);
	}

	[Fact]
	public void GivenAddLatiprimereCupsCalledTwiceWhenRegisteredThenPrintSystemIsRegisteredOnce()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereCups();
		services.AddLatiprimereCups();

		// Assert
		int registrationCount = services.Count(service => service.ServiceType == typeof(IPrintSystem));
		Assert.Equal(1, registrationCount);
	}

	[Fact]
	public void GivenNullServicesWhenAddLatiprimereCupsThenThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act
		Action addCups = () => services!.AddLatiprimereCups(); // Deliberately null to test the guard

		// Assert
		Assert.Throws<ArgumentNullException>(addCups);
	}
}
