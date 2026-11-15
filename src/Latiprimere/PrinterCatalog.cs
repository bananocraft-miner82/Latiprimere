using System.Runtime.ExceptionServices;
using Latiprimere.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Latiprimere;

/// <summary>
/// Finds printers across every print system supported on the current platform.
/// </summary>
/// <remarks>
/// <para>
/// Print systems are supplied by Latiprimere platform adapters. Those that report themselves as unsupported on the
/// current platform are ignored. When several print systems are supported, their printers are combined, and a print
/// system that fails while listing printers or finding the default is skipped with a logged warning, so one
/// unreachable print system doesn't hide the printers of the others. The call fails only if every print system fails.
/// </para>
/// <para>
/// The catalog doesn't own the print systems it is given and never disposes them. A dependency injection container
/// disposes the ones it created; an application that created them with a factory disposes them itself, after it has
/// finished with the catalog.
/// </para>
/// </remarks>
public sealed partial class PrinterCatalog : IPrinterCatalog
{
	private readonly IReadOnlyList<IPrintSystem> printSystems; // Print systems supported on this platform
	private readonly ILogger<PrinterCatalog> logger;           // Diagnostic logger

	/// <summary>
	/// Initialises a new instance of the <see cref="PrinterCatalog"/> class.
	/// </summary>
	/// <param name="printSystems">The print systems registered by platform adapters.</param>
	/// <param name="logger">The diagnostic logger, or <see langword="null"/> to disable logging.</param>
	/// <exception cref="ArgumentNullException"><paramref name="printSystems"/> is <see langword="null"/>.</exception>
	public PrinterCatalog(IEnumerable<IPrintSystem> printSystems, ILogger<PrinterCatalog>? logger = null)
	{
		// Validate the arguments, falling back to a logger that discards everything
		ArgumentNullException.ThrowIfNull(printSystems);
		this.logger = logger ?? NullLogger<PrinterCatalog>.Instance;

		// Keep only the print systems that can run on this platform
		this.printSystems = printSystems.Where(printSystem => printSystem.IsSupported).ToList();

		this.LogAvailablePrintSystems();
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A print system that fails is skipped and a warning is logged. If every print system fails, the single failure
	/// is rethrown, or, when there are several, a <see cref="PrintException"/> wrapping them in an
	/// <see cref="AggregateException"/> is thrown.
	/// </remarks>
	public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
	{
		// Fail clearly when nothing can print on this platform
		this.EnsurePrintSystemAvailable();

		// Ask every print system at once, so listing takes as long as the slowest, such as network discovery, rather
		// than all of them added up
		Task<(IReadOnlyList<PrinterInfo>? Printers, PrintException? Failure)>[] listings =
			new Task<(IReadOnlyList<PrinterInfo>? Printers, PrintException? Failure)>[this.printSystems.Count];

		for (int index = 0; index < this.printSystems.Count; index++)
		{
			listings[index] = PrinterCatalog.ListPrintersAsync(this.printSystems[index], cancellationToken);
		}

		(IReadOnlyList<PrinterInfo>? Printers, PrintException? Failure)[] results = await Task.WhenAll(listings).ConfigureAwait(false);

		// Combine the printers from every print system that answered, in the order the print systems were registered
		List<PrinterInfo> printers = new List<PrinterInfo>();
		List<PrintException> failures = new List<PrintException>();

		for (int index = 0; index < results.Length; index++)
		{
			if (results[index].Failure is PrintException failure)
			{
				this.RecordFailure(this.printSystems[index], failure, failures);
			}
			else
			{
				printers.AddRange(results[index].Printers ?? []);
			}
		}

		this.ThrowIfEveryPrintSystemFailed(failures);

		return printers;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A printer identifier belongs to one print system, so a failure while looking it up is not skipped.
	/// </remarks>
	public async Task<IPrinter> GetPrinterAsync(string printerId, CancellationToken cancellationToken = default)
	{
		// Validate the request before asking any print system
		ArgumentException.ThrowIfNullOrWhiteSpace(printerId);
		this.EnsurePrintSystemAvailable();

		// Ask each print system in turn; the first one that knows the printer wins
		IPrinter? foundPrinter = null;
		using IEnumerator<IPrintSystem> printSystemEnumerator = this.printSystems.GetEnumerator();

		while (foundPrinter is null && printSystemEnumerator.MoveNext())
		{
			foundPrinter = await printSystemEnumerator.Current
				.FindPrinterAsync(printerId, cancellationToken)
				.ConfigureAwait(false);
		}

		// Report a missing printer with its identifier so callers can show it
		if (foundPrinter is null)
		{
			throw PrinterNotFoundException.FromPrinterId(printerId);
		}

		return foundPrinter;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A print system that fails is skipped and a warning is logged. The call fails only if no default printer is
	/// found and every print system failed.
	/// </remarks>
	public async Task<IPrinter?> GetDefaultPrinterAsync(CancellationToken cancellationToken = default)
	{
		// Fail clearly when nothing can print on this platform
		this.EnsurePrintSystemAvailable();

		// Use the default printer of the first print system that answers with one
		IPrinter? defaultPrinter = null;
		List<PrintException> failures = new List<PrintException>();
		using IEnumerator<IPrintSystem> printSystemEnumerator = this.printSystems.GetEnumerator();

		while (defaultPrinter is null && printSystemEnumerator.MoveNext())
		{
			try
			{
				defaultPrinter = await printSystemEnumerator.Current
					.FindDefaultPrinterAsync(cancellationToken)
					.ConfigureAwait(false);
			}
			catch (PrintException exception)
			{
				this.RecordFailure(printSystemEnumerator.Current, exception, failures);
			}
		}

		if (defaultPrinter is null)
		{
			this.ThrowIfEveryPrintSystemFailed(failures);
		}

		return defaultPrinter;
	}

	/// <summary>
	/// Logs that a print system is available and will be used.
	/// </summary>
	/// <param name="logger">The logger to write to.</param>
	/// <param name="printSystemName">The name of the print system.</param>
	[LoggerMessage(Level = LogLevel.Debug, Message = "Print system {PrintSystemName} is available.")]
	private static partial void LogPrintSystemAvailable(ILogger logger, string printSystemName);

	/// <summary>
	/// Logs that no supported print system is available.
	/// </summary>
	/// <param name="logger">The logger to write to.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "No supported print system is available on this platform.")]
	private static partial void LogNoPrintSystemAvailable(ILogger logger);

	/// <summary>
	/// Logs that a print system failed and was skipped.
	/// </summary>
	/// <param name="logger">The logger to write to.</param>
	/// <param name="printSystemName">The name of the print system.</param>
	/// <param name="exception">The failure.</param>
	[LoggerMessage(Level = LogLevel.Warning, Message = "Print system {PrintSystemName} failed and was skipped.")]
	private static partial void LogPrintSystemSkipped(ILogger logger, string printSystemName, Exception exception);

	/// <summary>
	/// Logs which print systems the catalog will use.
	/// </summary>
	private void LogAvailablePrintSystems()
	{
		// Warn once at construction if printing cannot work, rather than on every call
		if (this.printSystems.Count == 0)
		{
			PrinterCatalog.LogNoPrintSystemAvailable(this.logger);
		}

		foreach (IPrintSystem printSystem in this.printSystems)
		{
			PrinterCatalog.LogPrintSystemAvailable(this.logger, printSystem.Name);
		}
	}

	/// <summary>
	/// Lists one print system's printers, catching its failure so the others' printers are still returned.
	/// </summary>
	/// <param name="printSystem">The print system.</param>
	/// <param name="cancellationToken">A token to cancel the listing.</param>
	/// <returns>Its printers, or its failure.</returns>
	/// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
	private static async Task<(IReadOnlyList<PrinterInfo>? Printers, PrintException? Failure)> ListPrintersAsync
	(
		IPrintSystem      printSystem,
		CancellationToken cancellationToken
	)
	{
		try
		{
			IReadOnlyList<PrinterInfo> printers = await printSystem.GetPrintersAsync(cancellationToken).ConfigureAwait(false);

			return (printers, null);
		}
		catch (PrintException exception)
		{
			return (null, exception);
		}
	}

	/// <summary>
	/// Logs a print system's failure and keeps it, in case every print system fails.
	/// </summary>
	/// <param name="printSystem">The print system that failed.</param>
	/// <param name="exception">The failure.</param>
	/// <param name="failures">The failures so far, which this one is added to.</param>
	private void RecordFailure(IPrintSystem printSystem, PrintException exception, List<PrintException> failures)
	{
		PrinterCatalog.LogPrintSystemSkipped(this.logger, printSystem.Name, exception);
		failures.Add(exception);
	}

	/// <summary>
	/// Throws if every print system failed, so callers never mistake a total failure for "no printers".
	/// </summary>
	/// <param name="failures">The failures recorded during the call.</param>
	/// <exception cref="PrintException">Every print system failed.</exception>
	private void ThrowIfEveryPrintSystemFailed(List<PrintException> failures)
	{
		if (failures.Count < this.printSystems.Count)
		{
			return;
		}

		// A single failure is rethrown as it was, keeping its stack trace and native error code
		if (failures.Count == 1)
		{
			ExceptionDispatchInfo.Throw(failures[0]);
		}

		throw new PrintException(ExceptionMessages.AllPrintSystemsFailed, new AggregateException(failures));
	}

	/// <summary>
	/// Ensures at least one print system is available.
	/// </summary>
	/// <exception cref="PrintSystemUnavailableException">No supported print system is available.</exception>
	private void EnsurePrintSystemAvailable()
	{
		if (this.printSystems.Count == 0)
		{
			throw new PrintSystemUnavailableException();
		}
	}
}
