namespace Latiprimere.Tests.Fakes;

/// <summary>
/// A printer whose status is read from a sequence, the last repeated, and which leaves following it to
/// <see cref="IPrinter.WatchStatusAsync"/>'s default. A <see langword="null"/> in the sequence fails that read.
/// </summary>
internal sealed class SequencePrinter : IPrinter
{
	/// <summary>The message of a read that fails.</summary>
	internal const string FailureMessage = "The printer can't be reached.";

	private readonly PrinterStatus?[] statuses; // Given by each read, in turn; null fails it
	private int reads;                         // Reads so far

	/// <summary>
	/// Initialises a new instance of the <see cref="SequencePrinter"/> class.
	/// </summary>
	/// <param name="statuses">The statuses, in the order they are read; <see langword="null"/> for a read that
	/// fails.</param>
	public SequencePrinter(params PrinterStatus?[] statuses)
	{
		this.statuses = statuses;
	}

	/// <inheritdoc/>
	public PrinterInfo Info { get; } = new PrinterInfo { Id = "fake:Office", Name = "Office" };

	/// <summary>Gets how many times the status was read.</summary>
	public int Reads
	{
		get
		{
			return Volatile.Read(ref this.reads);
		}
	}

	/// <inheritdoc/>
	public Task<PrinterStatus> GetStatusAsync(CancellationToken cancellationToken = default)
	{
		int read = Interlocked.Increment(ref this.reads) - 1;
		PrinterStatus? status = this.statuses[Math.Min(read, this.statuses.Length - 1)];

		return status is null
			? Task.FromException<PrinterStatus>(new PrintException(SequencePrinter.FailureMessage))
			: Task.FromResult(status);
	}

	/// <inheritdoc/>
	public Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException();
	}

	/// <inheritdoc/>
	public Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IPrintJob?>(null);
	}

	/// <inheritdoc/>
	public Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		return Task.FromResult(new PrinterCapabilities());
	}
}
