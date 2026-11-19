using Latiprimere;
using Latiprimere.Ipp;
using Latiprimere.Rendering;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the IPP network print system with a dependency injection container.
/// </summary>
public static class LatiprimereIppServiceCollectionExtensions
{
	/// <summary>
	/// Registers direct printing to IPP network printers. Use together with <c>AddLatiprimere()</c> from the Latiprimere
	/// package, and <c>AddLatiprimerePdfium()</c> from Latiprimere.Rendering.Pdfium to print PDFs as raster to
	/// printers that can't take PDF.
	/// </summary>
	/// <param name="services">The service collection to add to.</param>
	/// <param name="configure">
	/// Configures discovery and printer URIs, or <see langword="null"/> for discovery only.
	/// </param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">A fingerprint in <see cref="IppNetworkOptions.TrustedCertificateFingerprints"/>
	/// isn't 32 bytes in hexadecimal; a credential has no user name, or one containing a colon; or a URI in
	/// <see cref="IppNetworkOptions.PrinterCredentials"/> isn't an absolute ipp or ipps URI.</exception>
	public static IServiceCollection AddLatiprimereIpp(this IServiceCollection services, Action<IppNetworkOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Capture the options now; the print system copies them again when created
		IppNetworkOptions options = new IppNetworkOptions();
		configure?.Invoke(options);

		// Checked now, where the mistake was made, rather than when the catalog is first resolved
		IppNetworkPrintSystemFactory.ValidateOptions(options);

		// Register once, even if called more than once; the container disposes the print system on shutdown
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrintSystem, IppNetworkPrintSystem>
		(
			serviceProvider => (IppNetworkPrintSystem)IppNetworkPrintSystemFactory.Create
			(
				serviceProvider.GetService<IPdfRasterizer>(),
				options,
				serviceProvider.GetService<ILoggerFactory>()
			))
		);

		return services;
	}
}
