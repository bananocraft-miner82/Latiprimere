using Latiprimere.Rendering;
using Latiprimere.Windows.Gdi;
using Latiprimere.Windows.Spooler;
using Latiprimere.Windows.Tests.Fakes;

namespace Latiprimere.Windows.Tests.Builders;

/// <summary>
/// Builds <see cref="WindowsPrintContext"/> instances with fakes by default.
/// </summary>
internal sealed class WindowsPrintContextBuilder
{
	private IWinSpool spool = new FakeWinSpool();           // The spooler
	private IGdiPrinter gdi = new FakeGdiPrinter();         // The GDI printer
	private IPdfRasterizer? pdfRasterizer = null;           // The PDF rasteriser, if any
	private Latiprimere.Windows.DirectIpp.WindowsIppRoute? ippRoute = null; // The route to IPP printers, if on
	private Microsoft.Extensions.Logging.ILogger logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance; // Where to log

	/// <summary>Sets the spooler.</summary>
	/// <param name="spool">The spooler.</param>
	/// <returns>This builder, for chaining.</returns>
	public WindowsPrintContextBuilder WithSpool(IWinSpool spool)
	{
		this.spool = spool;

		return this;
	}

	/// <summary>Sets the GDI printer.</summary>
	/// <param name="gdi">The GDI printer.</param>
	/// <returns>This builder, for chaining.</returns>
	public WindowsPrintContextBuilder WithGdi(IGdiPrinter gdi)
	{
		this.gdi = gdi;

		return this;
	}

	/// <summary>Turns on sending PDF jobs straight to IPP printers.</summary>
	/// <param name="ippRoute">The route.</param>
	/// <returns>This builder, for chaining.</returns>
	public WindowsPrintContextBuilder WithIppRoute(Latiprimere.Windows.DirectIpp.WindowsIppRoute ippRoute)
	{
		this.ippRoute = ippRoute;

		return this;
	}

	/// <summary>Sets the PDF rasteriser.</summary>
	/// <param name="pdfRasterizer">The rasteriser.</param>
	/// <returns>This builder, for chaining.</returns>
	public WindowsPrintContextBuilder WithPdfRasterizer(IPdfRasterizer pdfRasterizer)
	{
		this.pdfRasterizer = pdfRasterizer;

		return this;
	}

	/// <summary>Sets the logger.</summary>
	/// <param name="logger">The logger.</param>
	/// <returns>This builder, for chaining.</returns>
	public WindowsPrintContextBuilder WithLogger(Microsoft.Extensions.Logging.ILogger logger)
	{
		this.logger = logger;

		return this;
	}

	/// <summary>Builds the context.</summary>
	/// <returns>The context.</returns>
	public WindowsPrintContext Build()
	{
		return new WindowsPrintContext
		{
			Spool         = this.spool,
			Gdi           = this.gdi,
			PdfRasterizer = this.pdfRasterizer,
			IppRoute      = this.ippRoute,
			Logger        = this.logger
		};
	}
}
