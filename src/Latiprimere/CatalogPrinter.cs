using Latiprimere.Resources;

namespace Latiprimere;

/// <summary>
/// The catalog's own printer, around a print system's: one place for what holds for every printer, whichever print
/// system it is from (ADR 0055).
/// </summary>
/// <remarks>
/// It refuses a job that sets a <see cref="PrintOptions"/> setting the print system's printer doesn't apply, and
/// wraps the jobs it returns. Everything else is the print system's printer's.
/// </remarks>
internal sealed class CatalogPrinter : IPrinter
{
	private readonly IPrinter printer; // The print system's printer

	/// <summary>
	/// Initialises a new instance of the <see cref="CatalogPrinter"/> class.
	/// </summary>
	/// <param name="printer">The print system's printer.</param>
	private CatalogPrinter(IPrinter printer)
	{
		this.printer = printer;
	}

	/// <inheritdoc/>
	public PrinterInfo Info
	{
		get
		{
			return this.printer.Info;
		}
	}

	/// <inheritdoc/>
	public PrintOptionKinds AppliedOptions
	{
		get
		{
			return this.printer.AppliedOptions;
		}
	}

	/// <summary>
	/// Wraps a print system's printer, unless it is wrapped already.
	/// </summary>
	/// <param name="printer">The printer, or <see langword="null"/>.</param>
	/// <returns>The catalog's printer, or <see langword="null"/> for none.</returns>
	internal static IPrinter? Wrap(IPrinter? printer)
	{
		return printer is null or CatalogPrinter ? printer : new CatalogPrinter(printer);
	}

	/// <inheritdoc/>
	public async Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(options);

		this.ThrowIfNotApplied(options);

		IPrintJob job = await this.printer.SubmitAsync(content, options, cancellationToken).ConfigureAwait(false);

		return CatalogPrintJob.Wrap(job);
	}

	/// <inheritdoc/>
	public async Task<IPrintJob> PrintTestPageAsync(PrintOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(options);

		// A test page is a job like any other, so its settings are held to the same check (ADR 0059)
		this.ThrowIfNotApplied(options);

		IPrintJob job = await this.printer.PrintTestPageAsync(options, cancellationToken).ConfigureAwait(false);

		return CatalogPrintJob.Wrap(job);
	}

	/// <inheritdoc/>
	public Task<PrinterDetails> GetDetailsAsync(CancellationToken cancellationToken = default)
	{
		return this.printer.GetDetailsAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public Task IdentifyAsync(string? message = null, CancellationToken cancellationToken = default)
	{
		return this.printer.IdentifyAsync(message, cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
	{
		IPrintJob? job = await this.printer.GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);

		return job is null ? null : CatalogPrintJob.Wrap(job);
	}

	/// <inheritdoc/>
	public Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		return this.printer.GetCapabilitiesAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public Task<PrinterStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		return this.printer.GetStatusAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public IAsyncEnumerable<PrinterStatus> WatchStatusAsync(TimeSpan pollInterval, CancellationToken cancellationToken = default)
	{
		return this.printer.WatchStatusAsync(pollInterval, cancellationToken);
	}

	/// <inheritdoc/>
	public T? GetFeature<T>()
		where T : class
	{
		// This one for IPrinter itself, so the checks here are never skipped; the print system's for its own interfaces
		return this as T ?? this.printer.GetFeature<T>();
	}

	/// <summary>
	/// Gets the settings a job sets to other than their defaults.
	/// </summary>
	/// <param name="options">The job's options.</param>
	/// <returns>Those settings.</returns>
	internal static PrintOptionKinds GetSetOptions(PrintOptions options)
	{
		PrintOptions defaults = PrintOptions.Default;
		PrintOptionKinds set = PrintOptionKinds.None;

		set |= options.JobName is not null ? PrintOptionKinds.JobName : PrintOptionKinds.None;
		set |= options.Copies != defaults.Copies ? PrintOptionKinds.Copies : PrintOptionKinds.None;
		set |= options.Duplex != defaults.Duplex ? PrintOptionKinds.Duplex : PrintOptionKinds.None;
		set |= options.Orientation != defaults.Orientation ? PrintOptionKinds.Orientation : PrintOptionKinds.None;
		set |= options.MediaSize is not null ? PrintOptionKinds.MediaSize : PrintOptionKinds.None;
		set |= options.ColorMode != defaults.ColorMode ? PrintOptionKinds.ColorMode : PrintOptionKinds.None;
		set |= options.Scaling != defaults.Scaling ? PrintOptionKinds.Scaling : PrintOptionKinds.None;
		set |= options.InputTray is not null ? PrintOptionKinds.InputTray : PrintOptionKinds.None;
		set |= options.VendorOptions.Count > 0 ? PrintOptionKinds.VendorOptions : PrintOptionKinds.None;
		set |= options.OutputFile is not null ? PrintOptionKinds.OutputFile : PrintOptionKinds.None;
		set |= options.PageRanges.Count > 0 ? PrintOptionKinds.PageRanges : PrintOptionKinds.None;
		set |= options.Collate is not null ? PrintOptionKinds.Collate : PrintOptionKinds.None;
		set |= options.Quality != defaults.Quality ? PrintOptionKinds.Quality : PrintOptionKinds.None;
		set |= options.Finishings.Count > 0 ? PrintOptionKinds.Finishings : PrintOptionKinds.None;
		set |= options.OutputBin is not null ? PrintOptionKinds.OutputBin : PrintOptionKinds.None;
		set |= options.MediaType is not null ? PrintOptionKinds.MediaType : PrintOptionKinds.None;

		return set;
	}

	/// <summary>
	/// Refuses a job that sets a <see cref="PrintOptions"/> setting the print system's printer doesn't apply, before
	/// anything is sent: the setting would be dropped without a word.
	/// </summary>
	/// <param name="options">The job's options.</param>
	/// <exception cref="NotSupportedException">It sets one.</exception>
	private void ThrowIfNotApplied(PrintOptions options)
	{
		PrintOptionKinds notApplied = CatalogPrinter.GetSetOptions(options) & ~this.printer.AppliedOptions;

		if (notApplied != PrintOptionKinds.None)
		{
			string message = ExceptionMessages.FormatOptionsNotApplied(this.printer.Info.Id, notApplied);

			throw new NotSupportedException(message);
		}
	}
}
