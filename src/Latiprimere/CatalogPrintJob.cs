namespace Latiprimere;

/// <summary>
/// The catalog's own print job, around a print system's, as <see cref="CatalogPrinter"/> is around its printer
/// (ADR 0055). It adds nothing yet; it is where what should hold for every job will go.
/// </summary>
internal sealed class CatalogPrintJob : IPrintJob
{
	private readonly IPrintJob job; // The print system's job

	/// <summary>
	/// Initialises a new instance of the <see cref="CatalogPrintJob"/> class.
	/// </summary>
	/// <param name="job">The print system's job.</param>
	private CatalogPrintJob(IPrintJob job)
	{
		this.job = job;
	}

	/// <inheritdoc/>
	public string Id
	{
		get
		{
			return this.job.Id;
		}
	}

	/// <inheritdoc/>
	public string PrinterId
	{
		get
		{
			return this.job.PrinterId;
		}
	}

	/// <summary>
	/// Wraps a print system's job, unless it is wrapped already.
	/// </summary>
	/// <param name="job">The job.</param>
	/// <returns>The catalog's job.</returns>
	internal static IPrintJob Wrap(IPrintJob job)
	{
		return job is CatalogPrintJob ? job : new CatalogPrintJob(job);
	}

	/// <inheritdoc/>
	public Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		return this.job.GetStatusAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public Task<bool> CancelAsync(CancellationToken cancellationToken = default)
	{
		return this.job.CancelAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public IAsyncEnumerable<PrintJobStatus> WatchStatusAsync(TimeSpan pollInterval, CancellationToken cancellationToken = default)
	{
		return this.job.WatchStatusAsync(pollInterval, cancellationToken);
	}

	/// <inheritdoc/>
	public T? GetFeature<T>()
		where T : class
	{
		return this as T ?? this.job.GetFeature<T>();
	}
}
