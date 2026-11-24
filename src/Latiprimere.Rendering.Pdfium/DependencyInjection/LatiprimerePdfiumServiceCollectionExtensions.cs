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

		// The constructor without limits of its own: PdfiumOptions isn't a service
		services.TryAddSingleton<IPdfRasterizer>(_ => new PdfiumRasterizer());

		return services;
	}

	/// <summary>
	/// Registers PDFium as the PDF rasteriser, with limits on the PDFs it opens and renders, for applications that
	/// print PDFs from anyone.
	/// </summary>
	/// <param name="services">The service collection to add to.</param>
	/// <param name="configure">Sets the limits, such as <see cref="PdfiumOptions.MaxDocumentBytes"/>. Called once,
	/// now.</param>
	/// <returns>The same service collection, for chaining.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is
	/// <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="configure"/> sets a limit out of range.</exception>
	public static IServiceCollection AddLatiprimerePdfium(this IServiceCollection services, Action<PdfiumOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		PdfiumOptions options = new PdfiumOptions();
		configure(options);

		// Made now, from a copy, so a mistake shows at registration and later changes to the options have no effect
		PdfiumRasterizer rasterizer = new PdfiumRasterizer(options);
		services.TryAddSingleton<IPdfRasterizer>(rasterizer);

		return services;
	}
}
