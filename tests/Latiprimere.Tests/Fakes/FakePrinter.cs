using Latiprimere.Tests.Builders;

namespace Latiprimere.Tests.Fakes;

/// <summary>
/// An in-memory printer that accepts every job.
/// </summary>
internal sealed class FakePrinter : IPrinter
{
	/// <summary>
	/// Initialises a new instance of the <see cref="FakePrinter"/> class.
	/// </summary>
	/// <param name="info">The information the printer reports about itself.</param>
	public FakePrinter(PrinterInfo info)
	{
		this.Info = info;
	}

	/// <summary>
	/// Gets the information the printer reports about itself.
	/// </summary>
	public PrinterInfo Info { get; }

	/// <summary>
	/// Gets the settings this printer says it applies; by default every setting <see cref="PrintOptions"/> has.
	/// </summary>
	public PrintOptionKinds AppliedOptions { get; init; } = PrintOptionKinds.JobName
															| PrintOptionKinds.Copies
															| PrintOptionKinds.Duplex
															| PrintOptionKinds.Orientation
															| PrintOptionKinds.MediaSize
															| PrintOptionKinds.ColorMode
															| PrintOptionKinds.Scaling
															| PrintOptionKinds.InputTray
															| PrintOptionKinds.VendorOptions
															| PrintOptionKinds.OutputFile
															| PrintOptionKinds.PageRanges
															| PrintOptionKinds.Collate
															| PrintOptionKinds.Quality
															| PrintOptionKinds.Finishings
															| PrintOptionKinds.OutputBin
															| PrintOptionKinds.MediaType;

	/// <summary>Gets the number of jobs submitted.</summary>
	public int SubmitCount { get; private set; }

	/// <summary>
	/// Accepts the document and returns a job that has already completed.
	/// </summary>
	/// <param name="content">The document to print. Not used.</param>
	/// <param name="options">The print options. Not used.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>A job that reports completion on its first status request.</returns>
	public Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken)
	{
		// Hand back a job that has already finished
		this.SubmitCount++;
		PrintJobStatus completedStatus = new PrintJobStatusBuilder()
			.WithState(PrintJobState.Completed)
			.Build();

		IPrintJob job = new FakePrintJob(completedStatus)
		{
			PrinterId = this.Info.Id
		};

		return Task.FromResult(job);
	}

	/// <summary>
	/// Finds no job, as a printer that has forgotten its jobs.
	/// </summary>
	/// <param name="jobId">The job's identifier. Not used.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns><see langword="null"/>.</returns>
	public Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken)
	{
		return Task.FromResult<IPrintJob?>(null);
	}

	/// <summary>
	/// Returns capabilities that report nothing.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>Empty capabilities.</returns>
	public Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken)
	{
		return Task.FromResult(new PrinterCapabilities());
	}
}
