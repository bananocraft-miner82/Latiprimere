using Latiprimere;
using Latiprimere.Rendering;
using Latiprimere.Windows;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the Windows print system with a dependency injection container.
/// </summary>
public static class LatiprimereWindowsServiceCollectionExtensions
{
	/// <summary>
	/// Registers the Windows print system. Use together with <c>AddLatiprimere()</c> from the Latiprimere package, and
	/// <c>AddLatiprimerePdfium()</c> from Latiprimere.Rendering.Pdfium to print PDF documents. Safe to call on every
	/// platform: elsewhere the print system reports itself as unsupported and is ignored.
	/// </summary>
	/// <param name="services">The service collection to add to.</param>
	/// <param name="configure">Configures how to print, or <see langword="null"/> for the defaults.</param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	public static IServiceCollection AddLatiprimereWindows(this IServiceCollection services, Action<WindowsOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Capture the options now, so later changes to the delegate's state don't affect the registration
		WindowsOptions options = new WindowsOptions();
		configure?.Invoke(options);

		// Register once, even if called more than once
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrintSystem, WindowsPrintSystem>
		(
			serviceProvider => (WindowsPrintSystem)WindowsPrintSystemFactory.Create
			(
				serviceProvider.GetService<IPdfRasterizer>(),
				options
			))
		);

		return services;
	}
}
