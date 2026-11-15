namespace Latiprimere.Tests.Fakes;

/// <summary>
/// An in-memory print system with a fixed set of printers.
/// </summary>
internal sealed class FakePrintSystem : IPrintSystem
{
	private readonly List<FakePrinter> printers; // Printers this print system reports
	private readonly TaskCompletionSource listingStarted =
		new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); // Completes when listing starts

	/// <summary>
	/// Initialises a new instance of the <see cref="FakePrintSystem"/> class.
	/// </summary>
	/// <param name="name">The print system name.</param>
	/// <param name="isSupported">Whether the print system reports itself as supported on this platform.</param>
	/// <param name="printers">The printers the print system reports.</param>
	public FakePrintSystem(string name, bool isSupported, params FakePrinter[] printers)
	{
		this.Name = name;
		this.IsSupported = isSupported;
		this.printers = printers.ToList();
	}

	/// <summary>
	/// Gets the print system name.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets a value indicating whether the print system reports itself as supported on this platform.
	/// </summary>
	public bool IsSupported { get; }

	/// <summary>
	/// Gets the number of times any lookup method has been called.
	/// </summary>
	public int RequestCount { get; private set; }

	/// <summary>
	/// Gets or sets an exception every lookup method throws, simulating an unreachable print system.
	/// </summary>
	public Exception? FailureToThrow { get; set; }

	/// <summary>
	/// Gets or sets a task that listing waits for before answering, as a slow print system would; none by default.
	/// </summary>
	public Task? ListingGate { get; set; }

	/// <summary>
	/// Gets a task that completes when listing starts, before it waits for <see cref="ListingGate"/>.
	/// </summary>
	public Task ListingStarted
	{
		get
		{
			return this.listingStarted.Task;
		}
	}

	/// <summary>
	/// Returns information about every printer, after <see cref="ListingGate"/> if one is set.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The printers' information.</returns>
	public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken)
	{
		this.RequestCount++;
		this.listingStarted.TrySetResult();

		if (this.ListingGate is not null)
		{
			await this.ListingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		this.ThrowIfFailing();

		return this.printers
			.Select(printer => printer.Info)
			.ToList();
	}

	/// <summary>
	/// Returns the printer with the given identifier, if this print system has one.
	/// </summary>
	/// <param name="printerId">The printer identifier.</param>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The printer, or <see langword="null"/> if not found.</returns>
	public Task<IPrinter?> FindPrinterAsync(string printerId, CancellationToken cancellationToken)
	{
		this.RequestCount++;
		this.ThrowIfFailing();
		IPrinter? printer = this.printers.FirstOrDefault(candidate => candidate.Info.Id == printerId);

		return Task.FromResult(printer);
	}

	/// <summary>
	/// Returns the printer marked as default, if any.
	/// </summary>
	/// <param name="cancellationToken">Not used.</param>
	/// <returns>The default printer, or <see langword="null"/> if none is marked as default.</returns>
	public Task<IPrinter?> FindDefaultPrinterAsync(CancellationToken cancellationToken)
	{
		this.RequestCount++;
		this.ThrowIfFailing();
		IPrinter? printer = this.printers.FirstOrDefault(candidate => candidate.Info.IsDefault);

		return Task.FromResult(printer);
	}

	/// <summary>
	/// Does nothing: the fake holds no resources.
	/// </summary>
	public void Dispose()
	{
	}

	/// <summary>
	/// Throws the configured failure, if any.
	/// </summary>
	/// <exception cref="Exception">A failure has been configured.</exception>
	private void ThrowIfFailing()
	{
		if (this.FailureToThrow is not null)
		{
			throw this.FailureToThrow;
		}
	}
}
