using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Latiprimere;

namespace LatiprimereExamples.Tests.Fakes;

/// <summary>
/// A printer that keeps what it is sent, so a test can check it, and hands back jobs that report scripted states.
/// </summary>
internal sealed class FakePrinter : IPrinter
{
	private readonly List<SubmittedJob> submittedJobs = new List<SubmittedJob>(); // Everything sent, in order
	private readonly List<FakePrintJob> jobs = new List<FakePrintJob>();            // The jobs handed back, to find again

	/// <summary>
	/// Initialises a new instance of the <see cref="FakePrinter"/> class.
	/// </summary>
	/// <param name="id">The printer's ID.</param>
	/// <param name="name">The printer's display name.</param>
	public FakePrinter(string id, string name)
	{
		this.Info = new PrinterInfo { Id = id, Name = name, State = PrinterState.Idle };
	}

	/// <summary>
	/// Gets the printer's description.
	/// </summary>
	public PrinterInfo Info { get; }

	/// <summary>
	/// Gets the states each job reports, one for each status check, staying on the last. By default a job has
	/// completed by the first check.
	/// </summary>
	public IReadOnlyList<PrintJobState> JobStates { get; init; } = [PrintJobState.Completed];

	/// <summary>
	/// Gets an exception to turn every job away with, as a print system does when it can't accept a job, or
	/// <see langword="null"/> to accept them.
	/// </summary>
	public PrintException? Rejection { get; init; }

	/// <summary>
	/// Gets what the printer reports it can do. By default it reports nothing, as some print systems do.
	/// </summary>
	public PrinterCapabilities Capabilities { get; init; } = new PrinterCapabilities();

	/// <summary>
	/// Gets the jobs the printer has been sent, in order.
	/// </summary>
	public IReadOnlyList<SubmittedJob> SubmittedJobs
	{
		get
		{
			return this.submittedJobs;
		}
	}

	/// <summary>
	/// Keeps a copy of the document and its options, and returns a job that reports <see cref="JobStates"/>.
	/// </summary>
	/// <param name="content">The document.</param>
	/// <param name="options">The print options.</param>
	/// <param name="cancellationToken">A token to cancel reading the document.</param>
	/// <returns>The job.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="content"/> or <paramref name="options"/> is
	/// <see langword="null"/>.</exception>
	/// <exception cref="PrintException"><see cref="Rejection"/> is set.</exception>
	public async Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(options);

		if (this.Rejection is not null)
		{
			throw this.Rejection;
		}

		// Read the document now, as a real printer would: the caller may close its stream once the job is sent
		using MemoryStream document = new MemoryStream();
		await content.Content.CopyToAsync(document, cancellationToken);

		this.submittedJobs.Add(new SubmittedJob { Format = content.Format, Options = options, Document = document.ToArray() });

		string jobId = this.submittedJobs.Count.ToString(CultureInfo.InvariantCulture);
		FakePrintJob job = new FakePrintJob(jobId, this.Info.Id, this.JobStates);
		this.jobs.Add(job);

		return job;
	}

	/// <summary>
	/// Finds a job this printer handed back, by its ID.
	/// </summary>
	/// <param name="jobId">The job's ID.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The job, or <see langword="null"/> if this printer wasn't sent it.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="jobId"/> is <see langword="null"/>.</exception>
	public Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(jobId);

		return Task.FromResult<IPrintJob?>(this.jobs.Find(job => job.Id == jobId));
	}

	/// <summary>
	/// Returns <see cref="Capabilities"/>.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The capabilities.</returns>
	public Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult(this.Capabilities);
	}
}
