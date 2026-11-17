using System.Runtime.Versioning;

namespace Latiprimere.EndToEnd.Support;

/// <summary>
/// Gives one job at a time, across every test process on this machine, use of a Windows printer that can't share.
/// </summary>
/// <remarks>
/// Microsoft Print to PDF can stall when two processes print to it at once, and a printer on a file port overwrites
/// one file with each job. The harness can run beside <c>dotnet test</c>, and xUnit runs each target framework in its
/// own process, so scenarios hold this lock around such jobs. It uses the same named semaphores as
/// <c>PrinterLock</c> in <c>Latiprimere.Windows.Tests</c>, so the integration tests wait for the harness and the other
/// way round.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class SharedPrinterLock : IDisposable
{
	private const string NamePrefix = @"Local\Latiprimere.Tests.Printer."; // The integration tests' prefix too

	private static readonly TimeSpan AcquireTimeout = TimeSpan.FromMinutes(2); // Longest to wait for another job

	private readonly Semaphore semaphore; // Held from construction until disposal

	/// <summary>
	/// Initialises a new instance of the <see cref="SharedPrinterLock"/> class, waiting until the printer is free.
	/// </summary>
	/// <param name="printerName">The printer's name in Windows, such as <c>Microsoft Print to PDF</c>.</param>
	/// <exception cref="ArgumentException"><paramref name="printerName"/> is <see langword="null"/>, empty or
	/// whitespace.</exception>
	/// <exception cref="TimeoutException">Another process held the printer for longer than two minutes.</exception>
	public SharedPrinterLock(string printerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

		// Kernel object names can't contain a backslash after the namespace prefix
		this.semaphore = new Semaphore(1, 1, SharedPrinterLock.NamePrefix + printerName.Replace('\\', '/'));

		if (!this.semaphore.WaitOne(SharedPrinterLock.AcquireTimeout))
		{
			this.semaphore.Dispose();

			throw new TimeoutException($"Another process held {printerName} for too long.");
		}
	}

	/// <summary>
	/// Releases the printer.
	/// </summary>
	public void Dispose()
	{
		this.semaphore.Release();
		this.semaphore.Dispose();
	}
}
