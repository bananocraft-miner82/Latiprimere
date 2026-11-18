using System.Diagnostics.CodeAnalysis;
using Latiprimere.Ipp.Discovery;
using Latiprimere.Rendering;
using Microsoft.Extensions.Logging;

namespace Latiprimere.Ipp;

/// <summary>
/// Creates the IPP network print system, for applications that don't use dependency injection.
/// </summary>
public static class IppNetworkPrintSystemFactory
{
	/// <summary>
	/// Creates a print system that prints directly to IPP printers on the network.
	/// </summary>
	/// <param name="options">The configuration, or <see langword="null"/> for discovery only.</param>
	/// <returns>
	/// The print system. It holds an HTTP connection, so dispose it, for example with <c>using</c>, when it
	/// is no longer needed.
	/// </returns>
	[SuppressMessage
	(
		"ApiDesign",
		"RS0027:API with optional parameter(s) should have the most parameters amongst its public overloads",
		Justification = "Shipped before the overload with a rasteriser, whose parameters are both required so that Create(null) stays unambiguous."
	)]
	public static IPrintSystem Create(IppNetworkOptions? options = null)
	{
		return IppNetworkPrintSystemFactory.Create(options, pdfRasterizer: null, loggerFactory: null);
	}

	/// <summary>
	/// Creates a print system that prints directly to IPP printers on the network, and renders PDFs as raster for
	/// printers that can't take PDF.
	/// </summary>
	/// <param name="pdfRasterizer">
	/// The rasteriser that renders PDFs for printers that don't take PDF, such as <c>PdfiumRasterizer</c> from
	/// Latiprimere.Rendering.Pdfium; or <see langword="null"/> to refuse PDFs to such printers.
	/// </param>
	/// <param name="options">The configuration, or <see langword="null"/> for discovery only.</param>
	/// <returns>
	/// The print system. It holds an HTTP connection, so dispose it, for example with <c>using</c>, when it
	/// is no longer needed.
	/// </returns>
	public static IPrintSystem Create(IPdfRasterizer? pdfRasterizer, IppNetworkOptions? options)
	{
		return IppNetworkPrintSystemFactory.Create(options, pdfRasterizer, loggerFactory: null);
	}

	/// <summary>
	/// Creates the direct IPP print system, logging through a logger factory when there is one.
	/// </summary>
	/// <param name="options">How to find and reach printers, or <see langword="null"/> for the defaults.</param>
	/// <param name="pdfRasterizer">The PDF rasteriser for printers that can't take PDF, or <see langword="null"/>.</param>
	/// <param name="loggerFactory">The application's logger factory, or <see langword="null"/> to log nothing.</param>
	/// <returns>The print system.</returns>
	internal static IPrintSystem Create(IppNetworkOptions? options, IPdfRasterizer? pdfRasterizer, ILoggerFactory? loggerFactory)
	{
		options ??= new IppNetworkOptions();

		SharpIppClientAdapter client = new SharpIppClientAdapter(IppHttpClientFactory.CreateForTcp(options.ConnectTimeout));

		IppPrintContext context = new IppPrintContext
		{
			Client             = client,
			RequestingUserName = options.UserName ?? Environment.UserName,
			DocumentFormats    = IppDocumentFormats.Standard,
			// Network printers that take one job at a time turn others away; CUPS queues them instead
			BusyRetryDuration  = options.BusyRetryDuration,
			ValidateJobs       = options.ValidateJobs,
			PdfRasterizer      = pdfRasterizer,
			MaxRenderDpi       = options.MaxRenderDpi,
			Logger             = IppLog.CreateLogger(loggerFactory)
		};

		return new IppNetworkPrintSystem(context, new MdnsIppPrinterDiscovery(), options, client);
	}
}
