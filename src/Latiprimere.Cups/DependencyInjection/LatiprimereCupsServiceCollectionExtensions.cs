using Latiprimere;
using Latiprimere.Cups;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the CUPS print system with a dependency injection container.
/// </summary>
public static class LatiprimereCupsServiceCollectionExtensions
{
	/// <summary>
	/// Registers the CUPS print system. Use together with <c>AddLatiprimere()</c> from the Latiprimere package.
	/// </summary>
	/// <param name="services">The service collection to add to.</param>
	/// <param name="configure">Configures how to connect to CUPS, or <see langword="null"/> for the defaults.</param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	public static IServiceCollection AddLatiprimereCups(this IServiceCollection services, Action<CupsOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Capture the options now, so later changes to the delegate's state don't affect the registration
		CupsOptions options = new CupsOptions();
		configure?.Invoke(options);

		// Register once, even if called more than once; the container disposes the print system on shutdown
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrintSystem, CupsPrintSystem>
		(
			serviceProvider => (CupsPrintSystem)CupsPrintSystemFactory.Create
			(
				options,
				Environment.GetEnvironmentVariable(CupsEndpoint.ServerEnvironmentVariable),
				serviceProvider.GetService<ILoggerFactory>()
			))
		);

		return services;
	}
}
