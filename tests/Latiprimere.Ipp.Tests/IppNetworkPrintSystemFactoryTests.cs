using Xunit;

namespace Latiprimere.Ipp.Tests;

public sealed class IppNetworkPrintSystemFactoryTests
{
	[Fact]
	public void GivenNoOptionsWhenCreateThenPrintSystemIsSupportedAndDisposable()
	{
		// Arrange — nothing to arrange: the defaults use discovery only

		// Act
		IPrintSystem printSystem = IppNetworkPrintSystemFactory.Create();

		// Assert
		Assert.Equal("IPP", printSystem.Name);
		Assert.True(printSystem.IsSupported);
		Assert.IsAssignableFrom<IDisposable>(printSystem);
		((IDisposable)printSystem).Dispose();
	}

	[Fact]
	public async Task GivenConfiguredPrinterWithoutDiscoveryWhenGetPrintersThenListsItWithoutNetworkAccess()
	{
		// Arrange
		IppNetworkOptions options = new IppNetworkOptions { EnableDiscovery = false };
		options.PrinterUris.Add(new Uri("ipp://192.0.2.10/ipp/print"));
		IPrintSystem printSystem = IppNetworkPrintSystemFactory.Create(options);

		// Act
		IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(TestContext.Current.CancellationToken);

		// Assert
		PrinterInfo printer = Assert.Single(printers);
		Assert.Equal("ipp:ipp://192.0.2.10/ipp/print", printer.Id);
		((IDisposable)printSystem).Dispose();
	}
}
