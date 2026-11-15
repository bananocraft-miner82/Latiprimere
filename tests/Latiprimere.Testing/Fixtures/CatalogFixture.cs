using Microsoft.Extensions.DependencyInjection;

namespace Latiprimere.Testing.Fixtures;

/// <summary>
/// A printer catalog built through dependency injection, the way applications build it.
/// </summary>
/// <remarks>
/// Disposing the fixture disposes the service provider, and with it the print systems it created.
/// </remarks>
public sealed class CatalogFixture : IAsyncDisposable
{
	private readonly ServiceProvider serviceProvider; // Owns the catalog and the print systems

	/// <summary>
	/// Initialises a new instance of the <see cref="CatalogFixture"/> class.
	/// </summary>
	/// <param name="registerPrintSystems">Registers the print systems under test, for example
	/// <c>services =&gt; services.AddLatiprimereCups()</c>.</param>
	/// <exception cref="ArgumentNullException"><paramref name="registerPrintSystems"/> is <see langword="null"/>.</exception>
	public CatalogFixture(Action<IServiceCollection> registerPrintSystems)
	{
		ArgumentNullException.ThrowIfNull(registerPrintSystems);

		ServiceCollection services = new ServiceCollection();
		services.AddLatiprimere();
		registerPrintSystems(services);

		this.serviceProvider = services.BuildServiceProvider();
		this.Catalog = this.serviceProvider.GetRequiredService<IPrinterCatalog>();
	}

	/// <summary>
	/// Gets the catalog over the registered print systems.
	/// </summary>
	public IPrinterCatalog Catalog { get; }

	/// <summary>
	/// Disposes the service provider and the print systems it created.
	/// </summary>
	/// <returns>A task that completes when everything is disposed.</returns>
	public ValueTask DisposeAsync()
	{
		return this.serviceProvider.DisposeAsync();
	}
}
