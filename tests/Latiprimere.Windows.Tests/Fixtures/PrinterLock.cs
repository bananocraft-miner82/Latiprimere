using System.Runtime.Versioning;

namespace Latiprimere.Windows.Tests.Fixtures;

/// <summary>
/// Gives one test at a time, across all test processes on this machine, use of a printer.
/// </summary>
/// <remarks>
/// <para>
/// <c>dotnet test</c> runs each target framework's tests in its own process at the same time. Some printers can't
/// take that: when two processes print to Microsoft Print to PDF at once, one job can stall and leave an empty file,
/// and a printer on a file port overwrites one file with each job. Tests that print there hold this lock until they
/// have checked the output.
/// </para>
/// <para>
/// The lock is a named semaphore rather than a mutex: async tests resume on other threads after an <c>await</c>, and
/// only the thread that took a mutex may release it. Named semaphores are Windows-only, as are these printers.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class PrinterLock : IDisposable
{
	/// <summary>
	/// The name of Microsoft Print to PDF, the virtual printer that writes PDF files.
	/// </summary>
	public const string MicrosoftPrintToPdf = "Microsoft Print to PDF";

	private const string NamePrefix = @"Local\Latiprimere.Tests.Printer."; // Shared by every test process

	private static readonly TimeSpan AcquireTimeout = TimeSpan.FromMinutes(2); // Longest to wait for another test

	private readonly Semaphore semaphore; // The cross-process lock, held from construction until disposal

	/// <summary>
	/// Initialises a new instance of the <see cref="PrinterLock"/> class, waiting until the lock is free.
	/// </summary>
	/// <param name="printerName">The printer to lock.</param>
	/// <exception cref="ArgumentException"><paramref name="printerName"/> is <see langword="null"/>, empty or
	/// whitespace.</exception>
	/// <exception cref="TimeoutException">Another test held the lock for longer than two minutes.</exception>
	public PrinterLock(string printerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(printerName);

		// Kernel object names can't contain a backslash after the namespace prefix
		this.semaphore = new Semaphore(1, 1, PrinterLock.NamePrefix + printerName.Replace('\\', '/'));

		if (!this.semaphore.WaitOne(PrinterLock.AcquireTimeout))
		{
			this.semaphore.Dispose();

			throw new TimeoutException($"Another test held {printerName} for too long.");
		}
	}

	/// <summary>
	/// Releases the lock.
	/// </summary>
	public void Dispose()
	{
		this.semaphore.Release();
		this.semaphore.Dispose();
	}
}
