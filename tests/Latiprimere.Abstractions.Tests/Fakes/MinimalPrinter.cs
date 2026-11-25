namespace Latiprimere.Abstractions.Tests.Fakes;

/// <summary>
/// A printer that implements only what <see cref="IPrinter"/> requires, as one written for an earlier version would,
/// so the default implementations of the rest can be tested.
/// </summary>
internal sealed class MinimalPrinter : IPrinter
{
	/// <summary>Gets the printer's information.</summary>
	public PrinterInfo Info { get; } = new PrinterInfo { Id = "own:office", Name = "Office" };

	/// <summary>Not used.</summary>
	/// <param name="content">The content.</param>
	/// <param name="options">The options.</param>
	/// <param name="cancellationToken">The token.</param>
	/// <returns>Never returns.</returns>
	public Task<IPrintJob> SubmitAsync(PrintContent content, PrintOptions options, CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException();
	}

	/// <summary>Not used.</summary>
	/// <param name="jobId">The job's ID.</param>
	/// <param name="cancellationToken">The token.</param>
	/// <returns>Never returns.</returns>
	public Task<IPrintJob?> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException();
	}

	/// <summary>Not used.</summary>
	/// <param name="cancellationToken">The token.</param>
	/// <returns>Never returns.</returns>
	public Task<PrinterCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
	{
		throw new NotImplementedException();
	}
}
