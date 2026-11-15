using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Latiprimere.Windows.Tests.DependencyInjection;

public sealed class LatiprimereWindowsServiceCollectionExtensionsTests
{
	[Fact]
	public void GivenServicesWhenAddLatiprimereWindowsThenResolvesWindowsPrintSystem()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereWindows();
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());

		// Assert
		Assert.Equal("Windows", printSystem.Name);
		Assert.Equal(OperatingSystem.IsWindows(), printSystem.IsSupported);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void GivenAllowAnyPrinterNameSettingWhenAddLatiprimereWindowsThenPrintSystemUsesIt(bool allowAnyPrinterName)
	{
		// Arrange — allowAnyPrinterName is the setting under test (provided by InlineData above)
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereWindows(options => options.AllowAnyPrinterName = allowAnyPrinterName);
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());

		// Assert
		Assert.Equal(allowAnyPrinterName, Assert.IsType<WindowsPrintSystem>(printSystem).AllowsAnyPrinterName);
	}

	[Fact]
	public void GivenNoConfigurationWhenAddLatiprimereWindowsThenOnlyListedNamesReachBeyondThisComputer()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereWindows();
		using ServiceProvider serviceProvider = services.BuildServiceProvider();
		IPrintSystem printSystem = Assert.Single(serviceProvider.GetServices<IPrintSystem>());

		// Assert — safe by default
		Assert.False(Assert.IsType<WindowsPrintSystem>(printSystem).AllowsAnyPrinterName);
	}

	[Fact]
	public void GivenAddLatiprimereWindowsCalledTwiceWhenRegisteredThenPrintSystemIsRegisteredOnce()
	{
		// Arrange
		ServiceCollection services = new ServiceCollection();

		// Act
		services.AddLatiprimereWindows();
		services.AddLatiprimereWindows();

		// Assert
		int registrationCount = services.Count(service => service.ServiceType == typeof(IPrintSystem));
		Assert.Equal(1, registrationCount);
	}

	[Fact]
	public void GivenNullServicesWhenAddLatiprimereWindowsThenThrowsArgumentNullException()
	{
		// Arrange
		IServiceCollection? services = null;

		// Act
		Action addWindows = () => services!.AddLatiprimereWindows(); // Deliberately null to test the guard

		// Assert
		Assert.Throws<ArgumentNullException>(addWindows);
	}
}
