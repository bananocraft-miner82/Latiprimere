using Latiprimere;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers Latiprimere services with a dependency injection container.
/// </summary>
public static class LatiprimereServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Latiprimere printer catalog. Register at least one platform adapter as well, or the catalog
	/// will report that no print system is available.
	/// </summary>
	/// <param name="services">The service collection to add to.</param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	public static IServiceCollection AddLatiprimere(this IServiceCollection services)
	{
		// Register the catalog once, even if called more than once
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton<IPrinterCatalog, PrinterCatalog>();

		return services;
	}
}
