using Latiprimere.Ipp.Testing.Fakes;

namespace Latiprimere.Ipp.Testing.Builders;

/// <summary>
/// Builds <see cref="IppPrintContext"/> instances with a fake client and CUPS document formats by default.
/// </summary>
internal sealed class IppPrintContextBuilder
{
	private IIppClient client = new FakeIppClient();                         // The client requests go to
	private IppDocumentFormats documentFormats = IppDocumentFormats.Cups;    // The MIME types documents are sent as
	private TimeSpan busyRetryDuration = TimeSpan.Zero;                      // How long jobs turned away as busy are resent
	private IReadOnlyList<string> vendorOptionPrefixes = Mapping.IppVendorAttributeMapper.NetworkPrefixes; // Keys read
	private Microsoft.Extensions.Logging.ILogger logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance; // Where to log
	private bool validateJobs = false;                                       // Whether to send Validate-Job first
	private Latiprimere.Rendering.IPdfRasterizer? pdfRasterizer;             // Renders PDFs for raster jobs
	private int? maxRenderDpi;                                               // The raster resolution limit
	private TimeProvider timeProvider = TimeProvider.System;                 // The clock retries are timed by
	private IReadOnlyList<Raster.RasterFormat> rasterFormats = Raster.RasterJobPlanner.DefaultFormatOrder; // Tried in order

	/// <summary>Sets how long to resend jobs turned away as busy, with millisecond waits between attempts.</summary>
	/// <param name="busyRetryDuration">How long to keep resending.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithBusyRetryDuration(TimeSpan busyRetryDuration)
	{
		this.busyRetryDuration = busyRetryDuration;

		return this;
	}

	/// <summary>Sets the clock, which times the resends of busy jobs.</summary>
	/// <param name="timeProvider">The clock.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithTimeProvider(TimeProvider timeProvider)
	{
		this.timeProvider = timeProvider;

		return this;
	}

	/// <summary>Sets the vendor option prefixes the print system reads.</summary>
	/// <param name="prefixes">The prefixes, such as <c>ipp:</c> and <c>cups:</c>.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithVendorOptionPrefixes(IReadOnlyList<string> prefixes)
	{
		this.vendorOptionPrefixes = prefixes;

		return this;
	}

	/// <summary>Sets the client.</summary>
	/// <param name="client">The client.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithClient(IIppClient client)
	{
		this.client = client;

		return this;
	}

	/// <summary>Sets the document formats.</summary>
	/// <param name="documentFormats">The document formats.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithDocumentFormats(IppDocumentFormats documentFormats)
	{
		this.documentFormats = documentFormats;

		return this;
	}

	/// <summary>Sets whether jobs are validated with Validate-Job before they are sent.</summary>
	/// <param name="validateJobs">Whether to validate.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithValidateJobs(bool validateJobs)
	{
		this.validateJobs = validateJobs;

		return this;
	}

	/// <summary>Sets the logger.</summary>
	/// <param name="logger">The logger.</param>
	/// <returns>This builder, for chaining.</returns>
	public IppPrintContextBuilder WithLogger(Microsoft.Extensions.Logging.ILogger logger)
	{
		this.logger = logger;

		return this;
	}

	/// <summary>
	/// Sets the rasteriser that renders PDFs for printers that can't take PDF.
	/// </summary>
	/// <param name="pdfRasterizer">The rasteriser.</param>
	/// <returns>This builder.</returns>
	public IppPrintContextBuilder WithPdfRasterizer(Latiprimere.Rendering.IPdfRasterizer pdfRasterizer)
	{
		this.pdfRasterizer = pdfRasterizer;

		return this;
	}

	/// <summary>
	/// Sets the highest resolution raster jobs are rendered at.
	/// </summary>
	/// <param name="maxRenderDpi">The limit.</param>
	/// <returns>This builder.</returns>
	public IppPrintContextBuilder WithMaxRenderDpi(int maxRenderDpi)
	{
		this.maxRenderDpi = maxRenderDpi;

		return this;
	}

	/// <summary>
	/// Sets the raster formats a PDF may be rendered into, in the order they're tried.
	/// </summary>
	/// <param name="rasterFormats">The formats.</param>
	/// <returns>This builder.</returns>
	public IppPrintContextBuilder WithRasterFormats(params Raster.RasterFormat[] rasterFormats)
	{
		this.rasterFormats = rasterFormats;

		return this;
	}

	/// <summary>
	/// Builds the context, with "tester" as the requesting user, and waits of a few milliseconds between resends of
	/// busy jobs so tests stay fast.
	/// </summary>
	/// <returns>The context.</returns>
	public IppPrintContext Build()
	{
		return new IppPrintContext
		{
			Client               = this.client,
			RequestingUserName   = "tester",
			DocumentFormats      = this.documentFormats,
			BusyRetryDuration    = this.busyRetryDuration,
			VendorOptionPrefixes = this.vendorOptionPrefixes,
			BusyRetryFirstDelay  = TimeSpan.FromMilliseconds(1),
			BusyRetryMaxDelay    = TimeSpan.FromMilliseconds(4),
			ValidateJobs         = this.validateJobs,
			PdfRasterizer        = this.pdfRasterizer,
			MaxRenderDpi         = this.maxRenderDpi,
			RasterFormats        = this.rasterFormats,
			Logger               = this.logger,
			TimeProvider         = this.timeProvider
		};
	}
}
