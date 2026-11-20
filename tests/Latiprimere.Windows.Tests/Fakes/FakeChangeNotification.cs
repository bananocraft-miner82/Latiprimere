using Latiprimere.Windows.Spooler;

namespace Latiprimere.Windows.Tests.Fakes;

/// <summary>
/// A change notification a test signals, counting its waits.
/// </summary>
internal sealed class FakeChangeNotification : IWindowsChangeNotification
{
	private readonly SemaphoreSlim signals = new SemaphoreSlim(0); // One count for each change signalled
	private readonly SemaphoreSlim waiting = new SemaphoreSlim(0); // One count each time a wait starts

	/// <summary>Gets the changes it was opened for.</summary>
	public required WindowsChanges Changes { get; init; }

	/// <summary>Gets the timeout of the last wait.</summary>
	public TimeSpan? LastTimeout { get; private set; }

	/// <summary>Gets a value indicating whether it was disposed.</summary>
	public bool IsDisposed { get; private set; }

	/// <inheritdoc/>
	public bool HasFailed { get; private set; }

	/// <summary>Gets how many waits there have been.</summary>
	public int Waits { get; private set; }

	/// <summary>
	/// Signals a change it then fails to collect, as when a print server's connection is lost.
	/// </summary>
	public void Fail()
	{
		this.HasFailed = true;
		this.signals.Release();
	}

	/// <summary>
	/// Signals a change, as the spooler would.
	/// </summary>
	public void Signal()
	{
		this.signals.Release();
	}

	/// <summary>
	/// Waits until the watcher is waiting for a change, so a test signals one after the status before it was read.
	/// </summary>
	/// <param name="cancellationToken">A token to stop waiting.</param>
	/// <returns>A task that completes when a wait has started.</returns>
	public Task WaitUntilWaitingAsync(CancellationToken cancellationToken)
	{
		return this.waiting.WaitAsync(cancellationToken);
	}

	/// <inheritdoc/>
	public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
	{
		this.LastTimeout = timeout;
		this.Waits++;
		this.waiting.Release();

		return await this.signals.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.IsDisposed = true;
		this.signals.Dispose();
		this.waiting.Dispose();
	}
}
