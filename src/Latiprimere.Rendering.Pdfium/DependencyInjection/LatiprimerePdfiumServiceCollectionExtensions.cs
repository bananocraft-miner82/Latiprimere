using Latiprimere.Rendering;
using Latiprimere.Rendering.Pdfium;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the PDFium rasteriser with a dependency injection container.
/// </summary>
public static class LatiprimerePdfiumServiceCollectionExtensions
{
	/// <summary>
	/// Registers PDFium as the PDF rasteriser, which lets the Windows adapter print PDF documents.
	/// </summary>
	/// <param name="services">The service collection to add to.</param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
	public static IServiceCollection AddLatiprimerePdfium(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.TryAddSingleton<IPdfRasterizer, PdfiumRasterizer>();

		return services;
	}
}
