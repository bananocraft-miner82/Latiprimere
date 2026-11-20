namespace Latiprimere.Windows.Spooler;

/// <summary>
/// A spooler change notification for one printer: signalled when the printer, or a job in its queue, changes
/// (ADR 0049).
/// </summary>
internal interface IWindowsChangeNotification : IDisposable
{
	/// <summary>
	/// Gets a value indicating whether a signalled change couldn't be collected, so the notification can't be waited on
	/// again: it would stay signalled. The caller then polls instead.
	/// </summary>
	bool HasFailed { get; }

	/// <summary>
	/// Waits until a change is signalled, then readies the notification for the next.
	/// </summary>
	/// <param name="timeout">The longest to wait.</param>
	/// <param name="cancellationToken">A token to stop waiting.</param>
	/// <returns><see langword="true"/> if a change was signalled; <see langword="false"/> if the time ran out.</returns>
	/// <exception cref="OperationCanceledException">The token was cancelled.</exception>
	Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
